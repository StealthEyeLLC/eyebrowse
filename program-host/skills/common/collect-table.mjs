import { optionalInt, targetFromArgs } from './lib.mjs';

export default async function collectTable(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  const index = optionalInt(args, 'index', 0, 0, 1000);
  const limit = optionalInt(args, 'limit', 5000, 1, 50000);
  const selector = typeof args.selector === 'string' && args.selector ? args.selector : null;
  const expression = `(()=>{const table=${selector ? `document.querySelector(${JSON.stringify(selector)})` : `document.querySelectorAll('table')[${index}]`};if(!table)return null;const rows=Array.from(table.rows).slice(0,${limit}).map(row=>Array.from(row.cells).map(cell=>(cell.innerText||cell.textContent||'').trim().replace(/\\s+/g,' ')));return {caption:table.caption?.innerText?.trim()||null,rows};})()`;
  const result = await browser.jsValue(target, expression);
  if (!result) return { target, found: false, rows: [] };
  return { target, found: true, caption: result.caption, rowCount: result.rows.length, rows: result.rows };
}
