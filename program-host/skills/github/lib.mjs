import { targetFromArgs, bounded } from '../common/lib.mjs';

export async function resolveGithubContext(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  const context = await browser.current();

  const pageEvidence = await browser.jsValue(target, `(()=>{
    const meta=name=>document.querySelector('meta[name="'+name+'"]')?.content||null;
    const canonical=document.querySelector('link[rel=canonical]')?.href||location.href;
    const links=Array.from(document.querySelectorAll('a[href]'));
    const raw=links.find(a=>{
      const href=a.getAttribute('href')||'';
      const label=(a.innerText||a.getAttribute('aria-label')||a.title||'');
      return href.includes('/raw/') && /Raw/i.test(label);
    }) || links.find(a=>(a.getAttribute('href')||'').includes('/raw/'));
    return {
      canonical,
      locationHref:location.href,
      origin:location.origin,
      repositoryNwo:meta('octolytics-dimension-repository_nwo'),
      repositoryId:meta('octolytics-dimension-repository_id'),
      defaultBranch:meta('octolytics-dimension-repository_default_branch'),
      repositoryPublic:meta('octolytics-dimension-repository_public'),
      selectedLink:meta('selected-link'),
      rawUrl:raw?.href||null,
      branchName:Array.from(document.querySelectorAll('button,[role=button]')).map(e=>({name:(e.textContent||'').trim(),aria:(e.getAttribute('aria-label')||'').trim()})).find(x=>x.name && / branch$/i.test(x.aria))?.name||null,
      title:document.title
    };
  })()`);

  const browserUrl = typeof args.url === 'string' && args.url ? args.url : (pageEvidence?.locationHref || context.url);
  const parsed = new URL(browserUrl);
  if (parsed.hostname !== 'github.com') return { ok: false, target, browserContext: context, evidence: pageEvidence, reason: 'Current origin is not github.com.' };
  const url = new URL(pageEvidence?.canonical || browserUrl);
  const parts = url.pathname.split('/').filter(Boolean).map(decodeURIComponent);
  const owner = parts[0] || null;
  const repo = parts[1] || null;
  const nwo = pageEvidence?.repositoryNwo || (owner && repo ? `${owner}/${repo}` : null);
  if (!nwo || !owner || !repo) return { ok: false, target, browserContext: context, evidence: pageEvidence, reason: 'GitHub repository identity is not supported by current evidence.' };

  let repositoryProvider = null;
  if (!pageEvidence?.defaultBranch || !pageEvidence?.repositoryId) {
    const api = await fetchGithubApi(browser, target, `https://api.github.com/repos/${owner}/${repo}`, 100000);
    if (api?.ok) {
      try {
        const parsed = api.json;
        repositoryProvider = {
          defaultBranch: typeof parsed.default_branch === 'string' ? parsed.default_branch : null,
          repositoryId: parsed.id == null ? null : String(parsed.id),
          fullName: typeof parsed.full_name === 'string' ? parsed.full_name : null,
          visibility: typeof parsed.visibility === 'string' ? parsed.visibility : null,
          source: 'github-rest-repository'
        };
      } catch {}
    }
  }
  const family = parts[2] || 'repository';
  const branchHint = typeof pageEvidence?.branchName === 'string' && pageEvidence.branchName ? pageEvidence.branchName : null;
  const result = {
    ok: true,
    target,
    browserContext: context,
    canonicalUrl: url.href,
    owner,
    repo,
    repository: nwo,
    defaultBranch: pageEvidence?.defaultBranch || (family === 'repository' ? branchHint : null) || repositoryProvider?.defaultBranch || null,
    repositoryId: pageEvidence?.repositoryId || repositoryProvider?.repositoryId || null,
    routeFamily: family,
    rawUrl: pageEvidence?.rawUrl || null,
    evidence: { source: pageEvidence?.repositoryNwo ? (repositoryProvider ? 'github-meta+browser-route+github-rest' : 'github-meta+browser-route') : (repositoryProvider ? 'browser-route+github-rest' : 'browser-route'), title: pageEvidence?.title || context.title, repositoryProvider }
  };

  if (family === 'pull' && /^\d+$/.test(parts[3] || '')) result.pullRequest = Number(parts[3]);
  else if (family === 'issues' && /^\d+$/.test(parts[3] || '')) result.issue = Number(parts[3]);
  else if (family === 'commit' && parts[3]) result.commit = parts[3];
  else if (family === 'actions' && parts[3] === 'runs' && /^\d+$/.test(parts[4] || '')) result.workflowRun = Number(parts[4]);
  else if (family === 'compare' && parts[3]) result.compare = parts.slice(3).join('/');

  if ((family === 'blob' || family === 'tree') && parts.length >= 4) {
    result.routeRemainder = parts.slice(3);
    const refParts = branchHint ? branchHint.split('/').filter(Boolean) : [];
    const routeRefMatches = refParts.length > 0 && refParts.every((part, index) => parts[3 + index] === part);
    if (routeRefMatches) {
      result.ref = branchHint;
      result.path = parts.slice(3 + refParts.length).join('/');
      result.refPathEvidence = 'branch-control+route';
    } else if (result.defaultBranch && parts[3] === result.defaultBranch) {
      result.ref = result.defaultBranch;
      result.path = parts.slice(4).join('/');
      result.refPathEvidence = 'default-branch-route';
    } else {
      result.refPathEvidence = 'route-hint-ambiguous';
    }
  }

  return result;
}

export async function fetchInPage(browser, target, url, maxChars = 1000000) {
  const limit = Math.max(1, Number(maxChars));
  const expression = `(async()=>{const attempt=async credentials=>{try{const r=await fetch(${JSON.stringify(url)},{credentials});const text=await r.text();return {ok:r.ok,status:r.status,statusText:r.statusText,url:r.url,contentType:r.headers.get('content-type'),text:text.slice(0,${limit}),totalChars:text.length,credentials};}catch(error){return {ok:false,status:0,error:String(error),credentials};}};const authenticated=await attempt('include');if(authenticated.ok)return authenticated;const publicAttempt=await attempt('omit');return publicAttempt.ok?{...publicAttempt,authenticatedFailure:{status:authenticated.status,error:authenticated.error||null}}:authenticated;})()`;
  return await browser.jsValue(target, expression);
}

export async function fetchGithubApi(browser, target, url, maxChars = 1000000) {
  const limit = Math.max(1, Number(maxChars));
  const expression = `(async()=>{const r=await fetch(${JSON.stringify(url)},{credentials:'omit',headers:{Accept:'application/vnd.github+json'}});const text=await r.text();return {ok:r.ok,status:r.status,statusText:r.statusText,url:r.url,contentType:r.headers.get('content-type'),text:text.slice(0,${limit}),totalChars:text.length,rateRemaining:r.headers.get('x-ratelimit-remaining')};})()`;
  const response = await browser.jsValue(target, expression);
  if (!response?.ok) return { ...response, json: null };
  try { return { ...response, json: JSON.parse(response.text) }; }
  catch { return { ...response, json: null, parseError: 'Response was not valid JSON.' }; }
}
export function diffFiles(diff) {
  const files = [];
  const seen = new Set();
  for (const line of String(diff || '').split(/\r?\n/)) {
    const match = /^diff --git a\/(.+) b\/(.+)$/.exec(line);
    if (!match) continue;
    const path = match[2];
    if (!seen.has(path)) { seen.add(path); files.push(path); }
  }
  return files;
}

export { bounded };
