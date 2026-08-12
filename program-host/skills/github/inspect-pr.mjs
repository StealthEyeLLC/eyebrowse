import { resolveGithubContext, fetchGithubApi, bounded } from './lib.mjs';

const compactComment = item => ({
  id: item?.id ?? null,
  author: item?.user?.login ?? null,
  body: bounded(item?.body ?? '', 12000),
  createdAt: item?.created_at ?? null,
  updatedAt: item?.updated_at ?? null,
  url: item?.html_url ?? null
});

export default async function inspectPr(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  const number = Number(args.pullRequest || context.pullRequest);
  if (!Number.isInteger(number) || number <= 0) return { ok: false, context, reason: 'No unambiguous pull request number in current context.' };

  const apiBase = `https://api.github.com/repos/${context.repository}`;
  const prResponse = await fetchGithubApi(browser, context.target, `${apiBase}/pulls/${number}`, 1000000);
  if (!prResponse?.ok || !prResponse.json) return { ok: false, context, pullRequest: number, provider: prResponse, reason: 'GitHub pull-request provider metadata was unavailable.' };
  const pr = prResponse.json;

  const [filesResponse, issueCommentsResponse, reviewCommentsResponse, reviewsResponse, checksResponse, statusResponse] = await Promise.all([
    fetchGithubApi(browser, context.target, `${apiBase}/pulls/${number}/files?per_page=100`, 4000000),
    fetchGithubApi(browser, context.target, `${apiBase}/issues/${number}/comments?per_page=100`, 2000000),
    fetchGithubApi(browser, context.target, `${apiBase}/pulls/${number}/comments?per_page=100`, 2000000),
    fetchGithubApi(browser, context.target, `${apiBase}/pulls/${number}/reviews?per_page=100`, 2000000),
    fetchGithubApi(browser, context.target, `${apiBase}/commits/${encodeURIComponent(pr.head?.sha || '')}/check-runs?per_page=100`, 3000000),
    fetchGithubApi(browser, context.target, `${apiBase}/commits/${encodeURIComponent(pr.head?.sha || '')}/status`, 1000000)
  ]);

  const providerFiles = Array.isArray(filesResponse?.json) ? filesResponse.json : [];
  if (!filesResponse?.ok || !providerFiles.length) return { ok: false, context, pullRequest: number, provider: { pullRequest: prResponse, files: filesResponse }, reason: 'GitHub pull-request file provider metadata was unavailable.' };
  const files = providerFiles.map(file => file?.filename).filter(Boolean);
  const checkRuns = Array.isArray(checksResponse?.json?.check_runs) ? checksResponse.json.check_runs : [];
  const annotations = [];
  const annotationLimit = Math.max(0, Math.min(Number(args.maxAnnotations || 100), 500));
  for (const check of checkRuns) {
    if (annotations.length >= annotationLimit || Number(check?.output?.annotations_count || 0) <= 0) continue;
    const response = await fetchGithubApi(browser, context.target, `${apiBase}/check-runs/${check.id}/annotations?per_page=100`, 2000000);
    if (!response?.ok || !Array.isArray(response.json)) continue;
    for (const annotation of response.json) {
      if (annotations.length >= annotationLimit) break;
      annotations.push({
        checkRunId: check.id,
        checkName: check.name ?? null,
        path: annotation?.path ?? null,
        startLine: annotation?.start_line ?? null,
        endLine: annotation?.end_line ?? null,
        annotationLevel: annotation?.annotation_level ?? null,
        title: annotation?.title ?? null,
        message: bounded(annotation?.message ?? '', 12000),
        rawDetails: bounded(annotation?.raw_details ?? '', 12000)
      });
    }
  }

  const issueComments = Array.isArray(issueCommentsResponse?.json) ? issueCommentsResponse.json.map(compactComment) : [];
  const reviewComments = Array.isArray(reviewCommentsResponse?.json) ? reviewCommentsResponse.json.map(item => ({ ...compactComment(item), path: item?.path ?? null, line: item?.line ?? item?.original_line ?? null, side: item?.side ?? null })) : [];
  const reviews = Array.isArray(reviewsResponse?.json) ? reviewsResponse.json.map(item => ({ id: item?.id ?? null, author: item?.user?.login ?? null, state: item?.state ?? null, body: bounded(item?.body ?? '', 12000), submittedAt: item?.submitted_at ?? null, commitId: item?.commit_id ?? null, url: item?.html_url ?? null })) : [];
  const checks = checkRuns.map(check => ({
    id: check?.id ?? null,
    name: check?.name ?? null,
    status: check?.status ?? null,
    conclusion: check?.conclusion ?? null,
    startedAt: check?.started_at ?? null,
    completedAt: check?.completed_at ?? null,
    detailsUrl: check?.details_url ?? null,
    app: check?.app?.slug ?? check?.app?.name ?? null,
    output: {
      title: check?.output?.title ?? null,
      summary: bounded(check?.output?.summary ?? '', 12000),
      text: bounded(check?.output?.text ?? '', 12000),
      annotationsCount: Number(check?.output?.annotations_count || 0)
    }
  }));
  const statuses = Array.isArray(statusResponse?.json?.statuses) ? statusResponse.json.statuses.map(status => ({
    id: status?.id ?? null,
    context: status?.context ?? null,
    state: status?.state ?? null,
    description: status?.description ?? null,
    targetUrl: status?.target_url ?? null,
    createdAt: status?.created_at ?? null
  })) : [];
  const fileDetails = providerFiles.map(file => ({
    path: file?.filename ?? null,
    status: file?.status ?? null,
    additions: file?.additions ?? null,
    deletions: file?.deletions ?? null,
    changes: file?.changes ?? null,
    previousPath: file?.previous_filename ?? null,
    blobUrl: file?.blob_url ?? null,
    rawUrl: file?.raw_url ?? null,
    patch: args.includeDiff ? bounded(file?.patch ?? '', Number(args.returnDiffChars || 100000)) : undefined
  }));

  return {
    ok: true,
    repository: context.repository,
    pullRequest: number,
    title: pr.title ?? null,
    author: pr.user?.login ?? null,
    state: pr.state ?? null,
    draft: Boolean(pr.draft),
    base: { ref: pr.base?.ref ?? null, sha: pr.base?.sha ?? null, label: pr.base?.label ?? null },
    head: { ref: pr.head?.ref ?? null, sha: pr.head?.sha ?? null, label: pr.head?.label ?? null, repository: pr.head?.repo?.full_name ?? null },
    additions: pr.additions ?? null,
    deletions: pr.deletions ?? null,
    commits: pr.commits ?? null,
    changedFilesReported: pr.changed_files ?? null,
    filesChanged: files,
    fileDetails,
    fileCount: files.length,
    issueComments,
    reviewComments,
    reviews,
    checks,
    statuses,
    annotations,
    provider: {
      pullRequestStatus: prResponse.status,
      filesStatus: filesResponse?.status ?? null,
      issueCommentsStatus: issueCommentsResponse?.status ?? null,
      reviewCommentsStatus: reviewCommentsResponse?.status ?? null,
      reviewsStatus: reviewsResponse?.status ?? null,
      checksStatus: checksResponse?.status ?? null,
      statusStatus: statusResponse?.status ?? null,
      rateRemaining: prResponse.rateRemaining ?? null
    },
    source: 'browser-route+github-rest'
  };
}