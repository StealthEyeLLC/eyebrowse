import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const mode = process.argv[2] || 'before';
const requestedTarget = process.argv[3] || null;
const browser = await EyeBrowse.connect();
try {
  const status = await browser.status();
  const targets = await browser.targets();
  let target = requestedTarget ? targets.find(x => x.id === requestedTarget) : targets.find(x => x.url === 'http://127.0.0.1:18762/forms');
  if (!target) target = (await browser.open('http://127.0.0.1:18762/forms')).target;
  await browser.activate(target.id);
  const surface = await browser.observe(target.id);
  const submit = await browser.query({ target: target.id, role: 'button', name: 'Submit', limit: 5 });
  const name = await browser.query({ target: target.id, role: 'textbox', name: 'Name', limit: 5 });
  const lifecycle = await browser.lifecycle(target.id);
  console.log(JSON.stringify({
    mode,
    status,
    target: target.id,
    rawTargetId: target.targetId,
    document: surface.document,
    submit: submit[0] || null,
    name: name[0] || null,
    lifecycle
  }, null, 2));
} finally {
  browser.close();
}
