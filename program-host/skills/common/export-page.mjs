import { targetFromArgs, writeTextAtomic, csv, bounded } from './lib.mjs';
import collectTable from './collect-table.mjs';

export default async function exportPage(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  if (typeof args.destination !== 'string' || !args.destination) throw new TypeError('destination is required');
  const format = (args.format || 'markdown').toLowerCase();
  if (format === 'csv') {
    const table = await collectTable(browser, { ...args, target });
    if (!table.found) return { ok: false, target, reason: 'No table matched the requested page context.' };
    const destination = await writeTextAtomic(args.destination, csv(table.rows));
    return { ok: true, target, format, destination, rows: table.rowCount };
  }
  if (format !== 'markdown' && format !== 'text') throw new TypeError('format must be markdown, text, or csv');
  const expression = format === 'text'
    ? `(()=>({title:document.title,url:location.href,text:(document.querySelector('main,article,[role=main]')||document.body).innerText}))()`
    : `(()=>{const root=document.querySelector('main,article,[role=main]')||document.body;const out=[];const push=s=>{s=(s||'').trim();if(s)out.push(s)};const fence=String.fromCharCode(96).repeat(3);for(const el of root.querySelectorAll('h1,h2,h3,h4,h5,h6,p,li,blockquote,pre')){const t=(el.innerText||el.textContent||'').trim();if(!t)continue;if(/^H[1-6]$/.test(el.tagName))push('#'.repeat(Number(el.tagName[1]))+' '+t);else if(el.tagName==='LI')push('- '+t);else if(el.tagName==='BLOCKQUOTE')push('> '+t.replace(/\\n/g,'\\n> '));else if(el.tagName==='PRE')push(fence+'\\n'+t+'\\n'+fence);else push(t)}return {title:document.title,url:location.href,markdown:out.join('\\n\\n')};})()`;
  const result = await browser.jsValue(target, expression);
  const body = format === 'text' ? result?.text : result?.markdown;
  if (body == null) return { ok: false, target, reason: 'The current document did not expose useful text content.' };
  const prefix = format === 'markdown' ? `# ${result.title || 'Page'}\n\nSource: ${result.url || ''}\n\n` : '';
  const material = bounded(prefix + body, Number(args.maxChars || 1000000));
  const destination = await writeTextAtomic(args.destination, material);
  return { ok: true, target, format, destination, chars: material.length, sourceUrl: result.url };
}
