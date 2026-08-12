import { readFile } from 'node:fs/promises';
import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';
import exportPage from '../../program-host/skills/common/export-page.mjs';
import downloadResource from '../../program-host/skills/common/download-resource.mjs';
import batchFormFill from '../../program-host/skills/common/batch-form-fill.mjs';
import multiTabCompare from '../../program-host/skills/common/multi-tab-compare.mjs';
import searchPagination from '../../program-host/skills/common/search-pagination.mjs';

const fixture = process.env.BUILD002_FIXTURE_ORIGIN || 'http://127.0.0.1:18762';
const outputRoot = process.env.BUILD002_HORIZONTAL_OUTPUT_ROOT || 'X:\\SkillPlaneTest\\horizontal';
const b = await EyeBrowse.connect();
const out = { fixture, outputRoot, checks: {} };
async function ready(t) { await b.activate(t.id); await b.wait(t.id, 'document.readyState==="complete"', 10000, 50); return await b.observe(t.id); }
try {
  const exportTarget = (await b.open(`${fixture}/horizontal/export`)).target; await ready(exportTarget);
  out.markdown = await exportPage(b, { target: exportTarget.id, format: 'markdown', destination: `${outputRoot}\\page.md` });
  out.markdownSample = (await readFile(out.markdown.destination, 'utf8')).slice(0, 220);
  out.csv = await exportPage(b, { target: exportTarget.id, format: 'csv', tableIndex: 0, destination: `${outputRoot}\\table.csv` });
  out.csvText = await readFile(out.csv.destination, 'utf8');

  const forms = (await b.open(`${fixture}/forms`)).target; await ready(forms);
  const name = (await b.query({ target: forms.id, role: 'textbox', name: 'Name', limit: 5 }))[0];
  const role = (await b.query({ target: forms.id, role: 'combobox', name: 'Role', limit: 5 }))[0];
  const enabled = (await b.query({ target: forms.id, role: 'checkbox', name: 'Enabled', limit: 5 }))[0];
  const submit = (await b.query({ target: forms.id, role: 'button', name: 'Submit', limit: 5 }))[0];
  out.formIds = { name: name?.id, role: role?.id, enabled: enabled?.id, submit: submit?.id };
  out.form = await batchFormFill(b, { fields: [
    { id: name.id, kind: 'fill', value: 'Horizontal Acceptance' },
    { id: role.id, kind: 'select', value: 'Operator' },
    { id: enabled.id, kind: 'check', value: true }
  ], submit: submit.id });
  out.formResult = await b.jsValue(forms.id, "document.querySelector('#result')?.textContent||''");

  const downloadPage = (await b.open(`${fixture}/horizontal/downloads`)).target; await ready(downloadPage);
  const downloadSpecs = [
    { key: 'text', url: `${fixture}/horizontal/download/text.txt`, destination: `${outputRoot}\\fixture-note.txt` },
    { key: 'csv', url: `${fixture}/horizontal/download/data.csv`, destination: `${outputRoot}\\fixture-data.csv` },
    { key: 'pdf', url: `${fixture}/horizontal/download/report.pdf`, destination: `${outputRoot}\\fixture-report.pdf` }
  ];
  out.downloads = {};
  for (const spec of downloadSpecs) {
    const result = await downloadResource(b, { target: downloadPage.id, url: spec.url, destination: spec.destination, timeoutMs: 15000, discoverTimeoutMs: 5000 });
    const bytes = result?.artifact?.path ? await readFile(result.artifact.path) : Buffer.alloc(0);
    out.downloads[spec.key] = { ...result, bytes: bytes.length, prefix: bytes.subarray(0, 12).toString('ascii') };
  }

  const identity = (await b.open(`${fixture}/identity`)).target; await ready(identity);
  await b.activate(forms.id); await b.observe(forms.id);
  const before = await b.current();
  out.multi = await multiTabCompare(b, { targets: [forms.id, identity.id], maxTargets: 2, textChars: 1200 });
  const after = await b.current();
  out.primary = { before, after, preserved: before.target === after.target && before.document === after.document };

  const traversalTarget = (await b.open(`${fixture}/horizontal/page/1`)).target; await ready(traversalTarget);
  out.traversal = await searchPagination(b, {
    target: traversalTarget.id,
    collectExpression: "Array.from(document.querySelectorAll('[data-item]')).map(x=>x.textContent)",
    maxPages: 5,
    nextRole: 'button',
    nextName: 'Next',
    quietMs: 100,
    timeoutMs: 5000
  });
  out.traversalValues = out.traversal.collected.flatMap(x => Array.isArray(x.value) ? x.value : []);

  out.checks = {
    markdown: out.markdown.ok && out.markdown.chars > 50 && out.markdownSample.includes('Horizontal export fixture'),
    csv: out.csv.ok && out.csv.rows === 4 && out.csvText.includes('alpha,3,ready') && out.csvText.includes('gamma,8,ready'),
    forms: out.form.ok && out.form.fields === 3 && out.form.submitted && out.formResult.includes('Horizontal Acceptance'),
    downloads: out.downloads.text.ok && out.downloads.text.download.state === 'completed' && out.downloads.text.bytes > 0 && out.downloads.csv.ok && out.downloads.csv.download.state === 'completed' && out.downloads.csv.bytes > 0 && out.downloads.pdf.ok && out.downloads.pdf.download.state === 'completed' && out.downloads.pdf.bytes > 0 && out.downloads.pdf.prefix.startsWith('%PDF-'),
    multiTab: out.multi.ok && out.multi.count === 2 && out.primary.preserved,
    traversal: out.traversal.ok && out.traversal.pages === 3 && out.traversal.stopped === 'end' && JSON.stringify(out.traversalValues) === JSON.stringify(['p1-a','p1-b','p2-a','p2-b','p3-a','p3-b'])
  };
  out.ok = Object.values(out.checks).every(Boolean);
  console.log(JSON.stringify(out, null, 2));
  process.exitCode = out.ok ? 0 : 1;
} finally { b.close(); }