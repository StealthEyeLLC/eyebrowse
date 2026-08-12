import { writeTextAtomic } from '../common/lib.mjs';
import { resolveGithubContext, fetchInPage } from './lib.mjs';

export default async function acquireFile(browser, args = {}) {
  const context = await resolveGithubContext(browser, args);
  if (!context.ok) return context;
  if (context.routeFamily !== 'blob') return { ok: false, context, reason: 'Current GitHub context is not a file/blob page.' };
  if (typeof args.destination !== 'string' || !args.destination) throw new TypeError('destination is required');

  let rawUrl = context.rawUrl;
  if (!rawUrl && context.ref && context.path)
    rawUrl = `https://github.com/${context.repository}/raw/refs/heads/${encodeURIComponent(context.ref)}/${context.path.split('/').map(encodeURIComponent).join('/')}`;
  if (!rawUrl) return { ok: false, context, reason: 'Repository is resolved, but exact ref/path/raw-resource evidence is ambiguous. Abstaining rather than guessing a file.' };

  const fetched = await fetchInPage(browser, context.target, rawUrl, Number(args.maxChars || 5000000));
  if (!fetched?.ok) return { ok: false, context, rawUrl, fetch: fetched, reason: 'Authenticated browser fetch of the GitHub raw resource failed.' };
  if (fetched.totalChars > Number(args.maxChars || 5000000)) return { ok: false, context, rawUrl, fetch: { ...fetched, text: undefined }, reason: 'Source exceeds the bounded text acquisition limit; use a streaming/resource path.' };
  const destination = await writeTextAtomic(args.destination, fetched.text);
  return { ok: true, repository: context.repository, ref: context.ref || null, path: context.path || null, rawUrl: fetched.url || rawUrl, destination, chars: fetched.totalChars };
}
