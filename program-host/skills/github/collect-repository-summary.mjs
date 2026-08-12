import { resolveGithubContext } from './lib.mjs';

export default async function collectRepositorySummary(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  const data = await browser.jsValue(context.target, `(()=>{const text=s=>document.querySelector(s)?.textContent?.trim()||null;const desc=document.querySelector('[data-pjax="#repo-content-pjax-container"] p, [data-testid="repository-description"]')?.textContent?.trim()||null;const links=Array.from(document.querySelectorAll('a[href]')).filter(a=>a.href.includes('/${context.owner}/${context.repo}/')).map(a=>({text:(a.innerText||'').trim(),href:a.href}));return {description:desc,headings:Array.from(document.querySelectorAll('h1,h2')).map(x=>x.innerText.trim()).filter(Boolean).slice(0,30),links:links.slice(0,100)}})()`);
  return { ok: true, repository: context.repository, defaultBranch: context.defaultBranch, routeFamily: context.routeFamily, currentObject: { pullRequest: context.pullRequest || null, issue: context.issue || null, commit: context.commit || null, workflowRun: context.workflowRun || null }, page: data, source: 'github-meta+browser-semantic-page' };
}
