import { optionalInt, targetFromArgs } from './lib.mjs';

export default async function collectLinks(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  const limit = optionalInt(args, 'limit', 200, 1, 2000);
  const expression = `(()=>Array.from(document.links).slice(0,${limit}).map((a,index)=>({index,text:(a.innerText||a.textContent||'').trim().replace(/\\s+/g,' ').slice(0,500),href:a.href,rel:a.rel||'',target:a.target||''})))()`;
  const links = await browser.jsValue(target, expression) ?? [];
  return { target, count: links.length, links };
}
