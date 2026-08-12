export default async function batchFormFill(browser, args = {}) {
  if (!Array.isArray(args.fields) || args.fields.length === 0) throw new TypeError('fields must be a non-empty array');
  const results = [];
  for (const field of args.fields) {
    if (!field || typeof field.id !== 'string') throw new TypeError('each field requires an eyeBROWSE element id');
    const kind = field.kind || 'fill';
    if (kind === 'fill') results.push({ id: field.id, kind, result: await browser.fill(field.id, String(field.value ?? '')) });
    else if (kind === 'select') results.push({ id: field.id, kind, result: await browser.select(field.id, Array.isArray(field.value) ? field.value.map(String) : [String(field.value)]) });
    else if (kind === 'check') results.push({ id: field.id, kind, result: field.value === false ? await browser.uncheck(field.id) : await browser.check(field.id) });
    else throw new TypeError(`unsupported field kind '${kind}'`);
  }
  let submitted = null;
  if (typeof args.submit === 'string' && args.submit) submitted = await browser.click(args.submit);
  return { ok: true, fields: results.length, submitted: Boolean(submitted), results, submitResult: submitted };
}
