import { resolveGithubContext, fetchGithubApi, fetchInPage, bounded } from './lib.mjs';

function parseJsonText(response) {
  if (!response?.text) return null;
  try { return JSON.parse(response.text); } catch { return null; }
}

function workflowStepEvidence(source, stepNames) {
  const lines = String(source || '').split(/\r?\n/);
  const wanted = new Set((stepNames || []).filter(Boolean));
  const matches = [];
  for (let i = 0; i < lines.length; i++) {
    const match = /^\s*-\s+name:\s*["']?(.+?)["']?\s*$/.exec(lines[i]);
    if (!match || !wanted.has(match[1])) continue;
    const snippet = [lines[i]];
    for (let j = i + 1; j < Math.min(lines.length, i + 12); j++) {
      if (/^\s*-\s+name:/.test(lines[j])) break;
      snippet.push(lines[j]);
    }
    matches.push({ step: match[1], line: i + 1, snippet: snippet.join('\n').trimEnd() });
  }
  return matches;
}

export default async function inspectFailedActionsRun(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  const run = Number(args.run || context.workflowRun);
  if (!Number.isInteger(run) || run <= 0) return { ok: false, context, reason: 'No unambiguous workflow run in current context.' };

  const page = await browser.jsValue(context.target, `(()=>{const root=document.querySelector('main,[role=main]')||document.body;return {text:root.innerText,links:Array.from(root.querySelectorAll('a[href]')).map(a=>({text:(a.innerText||'').trim(),href:a.href})).filter(x=>/job|log|step|workflow|actions/i.test(x.text+' '+x.href)).slice(0,200)}})()`);
  const network = await browser.network({ target: context.target, contains: '/actions/', limit: 200 });
  const failureSignals = String(page?.text || '').split(/\r?\n/).map(x=>x.trim()).filter(line => /fail|error|cancel|timed out|exit code|exception/i.test(line)).slice(0,200);

  const runApi = await fetchGithubApi(browser, context.target, `https://api.github.com/repos/${context.repository}/actions/runs/${run}`, Number(args.maxProviderChars || 500000));
  const jobsApi = await fetchGithubApi(browser, context.target, `https://api.github.com/repos/${context.repository}/actions/runs/${run}/jobs?per_page=100`, Number(args.maxProviderChars || 500000));
  const runJson = runApi?.json || parseJsonText(runApi);
  const jobsJson = jobsApi?.json || parseJsonText(jobsApi);

  const runMetadata = runJson ? {
    id: runJson.id,
    name: runJson.name,
    displayTitle: runJson.display_title,
    event: runJson.event,
    status: runJson.status,
    conclusion: runJson.conclusion,
    runNumber: runJson.run_number,
    attempt: runJson.run_attempt,
    headBranch: runJson.head_branch,
    headSha: runJson.head_sha,
    workflowPath: runJson.path,
    htmlUrl: runJson.html_url,
    pullRequests: Array.isArray(runJson.pull_requests) ? runJson.pull_requests.map(pr => ({ number: pr.number, head: pr.head?.ref || null, base: pr.base?.ref || null })).slice(0,20) : []
  } : null;

  const failedJobs = [];
  const jobs = Array.isArray(jobsJson?.jobs) ? jobsJson.jobs : [];
  for (const job of jobs.filter(x => x?.conclusion === 'failure').slice(0, Number(args.maxFailedJobs || 10))) {
    const failedSteps = Array.isArray(job.steps) ? job.steps.filter(step => step?.conclusion === 'failure').map(step => ({
      number: step.number,
      name: step.name,
      status: step.status,
      conclusion: step.conclusion,
      startedAt: step.started_at,
      completedAt: step.completed_at
    })) : [];

    let checkRun = null;
    let annotations = [];
    if (job.check_run_url) {
      const checkApi = await fetchGithubApi(browser, context.target, job.check_run_url, 250000);
      const checkJson = checkApi?.json || parseJsonText(checkApi);
      if (checkJson) {
        checkRun = {
          id: checkJson.id,
          name: checkJson.name,
          status: checkJson.status,
          conclusion: checkJson.conclusion,
          detailsUrl: checkJson.details_url,
          annotationCount: checkJson.output?.annotations_count ?? 0
        };
        const annotationsUrl = checkJson.output?.annotations_url;
        if (annotationsUrl) {
          const annotationApi = await fetchGithubApi(browser, context.target, annotationsUrl, 250000);
          const annotationJson = annotationApi?.json || parseJsonText(annotationApi);
          if (Array.isArray(annotationJson)) annotations = annotationJson.slice(0, 100).map(item => ({
            path: item.path,
            blobHref: item.blob_href,
            startLine: item.start_line,
            endLine: item.end_line,
            level: item.annotation_level,
            title: item.title,
            message: item.message,
            rawDetails: item.raw_details
          }));
        }
      }
    }

    failedJobs.push({
      id: job.id,
      name: job.name,
      status: job.status,
      conclusion: job.conclusion,
      htmlUrl: job.html_url,
      headSha: job.head_sha,
      failedSteps,
      checkRun,
      annotations
    });
  }

  let logsAccess = null;
  if (failedJobs[0]?.id) {
    const logsApi = await fetchGithubApi(browser, context.target, `https://api.github.com/repos/${context.repository}/actions/jobs/${failedJobs[0].id}/logs`, Number(args.logProbeChars || 20000));
    const logsJson = logsApi?.json || parseJsonText(logsApi);
    logsAccess = {
      jobId: failedJobs[0].id,
      ok: Boolean(logsApi?.ok),
      status: logsApi?.status ?? 0,
      statusText: logsApi?.statusText || null,
      message: logsJson?.message || null,
      documentationUrl: logsJson?.documentation_url || null,
      note: logsApi?.ok ? 'Raw job log endpoint was accessible.' : 'Raw job logs were not accessible with the current browser/provider authority; no log content was fabricated.'
    };
  }

  let workflowSource = null;
  let failedStepSource = [];
  if (runMetadata?.workflowPath && runMetadata?.headSha) {
    const rawPath = runMetadata.workflowPath.split('/').map(encodeURIComponent).join('/');
    const rawUrl = `https://raw.githubusercontent.com/${context.repository}/${encodeURIComponent(runMetadata.headSha)}/${rawPath}`;
    const workflow = await fetchInPage(browser, context.target, rawUrl, Number(args.maxWorkflowChars || 300000));
    workflowSource = {
      ok: Boolean(workflow?.ok),
      status: workflow?.status ?? 0,
      url: workflow?.url || rawUrl,
      path: runMetadata.workflowPath,
      ref: runMetadata.headSha,
      chars: workflow?.totalChars ?? 0,
      text: workflow?.ok && args.includeWorkflowSource ? bounded(workflow.text, Number(args.workflowSourceChars || 100000)) : undefined
    };
    if (workflow?.ok) {
      const names = [...new Set(failedJobs.flatMap(job => job.failedSteps.map(step => step.name)).filter(Boolean))];
      failedStepSource = workflowStepEvidence(workflow.text, names);
    }
  }

  const providerAvailable = Boolean(runMetadata && jobsJson);
  return {
    ok: providerAvailable || failureSignals.length > 0,
    repository: context.repository,
    workflowRun: run,
    run: runMetadata,
    failedJobs,
    failedJobCount: failedJobs.length,
    failedStepCount: failedJobs.reduce((sum, job) => sum + job.failedSteps.length, 0),
    failedStepSource,
    logsAccess,
    failureSignals,
    relevantLinks: page?.links || [],
    networkCandidates: (network || []).slice(-100),
    provider: {
      run: { ok: Boolean(runApi?.ok), status: runApi?.status ?? 0, rateRemaining: runApi?.rateRemaining ?? null },
      jobs: { ok: Boolean(jobsApi?.ok), status: jobsApi?.status ?? 0, rateRemaining: jobsApi?.rateRemaining ?? null }
    },
    workflowSource,
    pageText: args.includePageText ? bounded(page?.text, Number(args.pageTextChars || 50000)) : undefined,
    source: providerAvailable ? 'github-rest-actions+check-annotations+workflow-source+browser-fallback' : 'browser+bounded-network-state'
  };
}