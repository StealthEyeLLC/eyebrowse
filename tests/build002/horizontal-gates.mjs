import { readFile } from 'node:fs/promises';
import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';
import exportPage from '../../program-host/skills/common/export-page.mjs';
import downloadResource from '../../program-host/skills/common/download-resource.mjs';
import batchFormFill from '../../program-host/skills/common/batch-form-fill.mjs';
import multiTabCompare from '../../program-host/skills/common/multi-tab-compare.mjs';

const fixture = process.env.BUILD002_FIXTURE_ORIGIN || 'http://127.0.0.1:18762';
const outputRoot = process.env.BUILD002_HORIZONTAL_OUTPUT_ROOT || 'X:\\SkillPlaneTest\\horizontal';
const b = await EyeBrowse.connect();
const out = { fixture, outputRoot, checks: {} };
async function ready(t) { await b.activate(t.id); await b.wait(t.id, 'document.readyState==="complete"', 10000, 50); return await b.observe(t.id); }
try {
  const forms = (await b.open(`${fixture}/forms`)).target; await ready(forms);
  out.markdown = await exportPage(b, { target: forms.id, format: 'markdown', destination: `${outputRoot}\\page.md` });
  out.markdownSample = (await readFile(out.markdown.destination, 'utf8')).slice(0, 220);

  const tableHtml = '<html><head><title>Horizontal Table</title></head><body><main><h1>Metrics</h1><table><caption>Build 002 sample</caption><tr><th>Name</th><th>Value</th></tr><tr><td>alpha</td><td>10</td></tr><tr><td>beta</td><td>20</td></tr></table></main></body></html>';
  const table = (await b.open('data:text/html,' + encodeURIComponent(tableHtml))).target; await ready(table);
  out.csv = await exportPage(b, { target: table.id, format: 'csv', destination: `${outputRoot}\\table.csv` });
  out.csvText = await readFile(out.csv.destination, 'utf8');

  await b.activate(forms.id); await b.observe(forms.id);
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

  out.download = await downloadResource(b, { target: forms.id, url: `${fixture}/download/test.txt`, destination: `${outputRoot}\\downloaded-fixture.txt`, timeoutMs: 15000, discoverTimeoutMs: 5000 });
  out.downloadText = await readFile(out.download.artifact.path, 'utf8');

  const identity = (await b.open(`${fixture}/identity`)).target; await ready(identity);
  await b.activate(forms.id); await b.observe(forms.id);
  const before = await b.current();
  out.multi = await multiTabCompare(b, { targets: [forms.id, identity.id], maxTargets: 2, textChars: 1200 });
  const after = await b.current();
  out.primary = { before, after, preserved: before.target === after.target && before.document === after.document };

  out.checks = {
    markdown: out.markdown.ok && out.markdown.chars > 50,
    csv: out.csv.ok && out.csv.rows === 3 && out.csvText.includes('alpha,10'),
    forms: out.form.ok && out.form.fields === 3 && out.form.submitted && out.formResult.includes('Horizontal Acceptance'),
    download: out.download.ok && out.download.download.state === 'completed' && out.downloadText.includes('deterministic fixture download'),
    multiTab: out.multi.ok && out.multi.count === 2 && out.primary.preserved
  };
  out.ok = Object.values(out.checks).every(Boolean);
  console.log(JSON.stringify(out, null, 2));
  process.exitCode = out.ok ? 0 : 1;
} finally { b.close(); }