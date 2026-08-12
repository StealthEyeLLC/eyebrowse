import { targetFromArgs, optionalInt } from './lib.mjs';

export default async function downloadResource(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  const url = typeof args.url === 'string' && args.url ? args.url : await browser.jsValue(target, 'location.href');
  const filename = typeof args.filename === 'string' && args.filename ? args.filename : '';
  const before = new Set((await browser.downloads()).map(x => x.id));
  await browser.js(target, `(()=>{const a=document.createElement('a');a.href=${JSON.stringify(url)};a.download=${JSON.stringify(filename)};a.style.display='none';document.documentElement.appendChild(a);a.click();a.remove();return true;})()`);
  const deadline = Date.now() + optionalInt(args, 'discoverTimeoutMs', 10000, 100, 120000);
  let found = null;
  while (Date.now() < deadline) {
    const downloads = await browser.downloads();
    found = downloads.find(x => !before.has(x.id)) || null;
    if (found) break;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  if (!found) return { ok: false, target, url, reason: 'Chrome did not report a new browser download.' };
  const completed = await browser.downloadWait(found.id, optionalInt(args, 'timeoutMs', 120000, 100, 600000));
  if (completed.state !== 'completed') return { ok: false, target, url, download: completed };
  if (typeof args.destination === 'string' && args.destination)
    return { ok: true, target, url, download: completed, artifact: await browser.downloadSave(completed.id, args.destination) };
  return { ok: true, target, url, download: completed };
}
