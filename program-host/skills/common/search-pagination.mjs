import { targetFromArgs, optionalInt } from './lib.mjs';

export default async function searchPagination(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  if (typeof args.collectExpression !== 'string' || !args.collectExpression) throw new TypeError('collectExpression is required');
  const maxPages = optionalInt(args, 'maxPages', 10, 1, 200);
  const role = typeof args.nextRole === 'string' && args.nextRole ? args.nextRole : 'button';
  const name = typeof args.nextName === 'string' && args.nextName ? args.nextName : 'Next';
  const collected = [];
  const visited = new Set();
  for (let page = 0; page < maxPages; page++) {
    const url = await browser.jsValue(target, 'location.href');
    if (visited.has(url)) break;
    visited.add(url);
    const value = await browser.jsValue(target, args.collectExpression);
    collected.push({ page: page + 1, url, value });
    const next = (await browser.query({ target, role, name, limit: 5 })).filter(x => !x.disabled);
    if (next.length !== 1) return { ok: next.length === 0, stopped: next.length === 0 ? 'end' : 'ambiguous-next', pages: collected.length, collected, nextCandidates: next.map(x => x.id) };
    await browser.click(next[0].id);
    await browser.quiet(target, Number(args.quietMs || 250), Number(args.timeoutMs || 10000));
  }
  return { ok: true, stopped: 'limit-or-cycle', pages: collected.length, collected };
}
