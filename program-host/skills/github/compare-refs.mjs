import { resolveGithubContext, fetchInPage, diffFiles, bounded } from './lib.mjs';

export default async function compareRefs(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  let base = args.base, head = args.head;
  if ((!base || !head) && context.compare) {
    const marker = context.compare.split('...');
    if (marker.length === 2) [base, head] = marker;
  }
  if (typeof base !== 'string' || !base || typeof head !== 'string' || !head) return { ok: false, context, reason: 'Both base and head refs are required or must be resolved from the current compare route.' };
  const url = `https://github.com/${context.repository}/compare/${encodeURIComponent(base)}...${encodeURIComponent(head)}.diff`;
  const fetched = await fetchInPage(browser, context.target, url, Number(args.maxDiffChars || 2000000));
  if (!fetched?.ok) return { ok: false, context, url, fetch: fetched };
  const files = diffFiles(fetched.text);
  return { ok: true, repository: context.repository, base, head, filesChanged: files, fileCount: files.length, diffChars: fetched.totalChars, diff: args.includeDiff ? bounded(fetched.text, Number(args.returnDiffChars || 100000)) : undefined, source: 'provider-diff-resource' };
}
