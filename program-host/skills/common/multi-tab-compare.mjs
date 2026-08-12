import { optionalInt } from './lib.mjs';

export default async function multiTabCompare(browser, args = {}) {
  let targets = Array.isArray(args.targets) ? args.targets.filter(x => typeof x === 'string') : [];
  if (targets.length === 0) targets = (await browser.targets()).filter(x => x.type === 'page').map(x => x.id);
  const limit = optionalInt(args, 'maxTargets', 12, 2, 100);
  targets = targets.slice(0, limit);
  if (targets.length < 2) return { ok: false, reason: 'At least two page targets are required.', targets };
  const textLimit = optionalInt(args, 'textChars', 8000, 200, 50000);
  const rows = await Promise.all(targets.map(async target => {
    const value = await browser.jsValue(target, `(()=>({url:location.href,title:document.title,text:(document.querySelector('main,article,[role=main]')||document.body).innerText.slice(0,${textLimit})}))()`);
    return { target, ...value };
  }));
  return { ok: true, count: rows.length, tabs: rows };
}
