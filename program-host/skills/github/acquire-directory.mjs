import { resolveGithubContext } from './lib.mjs';

export default async function acquireDirectory(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  const target = context.target;
  const entries = await browser.jsValue(target, `(()=>Array.from(document.querySelectorAll('a[href]')).map(a=>({text:(a.innerText||a.textContent||'').trim(),href:a.href})).filter(x=>x.href.includes('/${context.owner}/${context.repo}/blob/')||x.href.includes('/${context.owner}/${context.repo}/tree/')).slice(0,500))()`);
  return {
    ok: true,
    context,
    entries: entries || [],
    requestedDestination: args.destination || null,
    handoff: args.destination ? { eye: 'CODEeye', operation: 'acquire-directory', repository: context.repository, ref: context.ref || context.defaultBranch, path: context.path || '', destination: args.destination } : null,
    note: args.destination ? 'Material repository directory acquisition is an engineering-provider operation.' : 'Returned browser-visible structured directory entries only.'
  };
}
