import { readFileSync } from 'node:fs';
import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const mode = process.argv[2] || 'prepare';
const browser = await EyeBrowse.connect();
const fail = message => { throw new Error(message); };

try {
  if (mode === 'prepare') {
    const status = await browser.status();
    const extensions = await browser.extensions();
    const bridge = extensions.find(x => x.name === 'eyebrowse agent bridge' && x.enabled);
    if (!bridge) fail('Configured eyeBROWSE bridge is not enabled before fixture navigation.');

    const existing = (await browser.targets()).find(x => x.url === 'http://127.0.0.1:18762/forms');
    const target = existing ?? (await browser.open('http://127.0.0.1:18762/forms')).target;
    await browser.activate(target.id);
    const before = await browser.observe(target.id);
    const name = (await browser.query({ target: target.id, role: 'textbox', name: 'Name', limit: 5 }))[0];
    const submit = (await browser.query({ target: target.id, role: 'button', name: 'Submit', limit: 5 }))[0];
    if (!name || !submit) fail('Forms fixture did not expose required semantic controls.');

    const sentinel = `survives-kernel-death-${Date.now()}`;
    await browser.fill(name.id, sentinel);
    const prepared = await browser.observe(target.id);
    const preparedName = await browser.inspect(name.id);
    const lifecycle = await browser.lifecycle(target.id);
    console.log(JSON.stringify({
      mode,
      status,
      bridge,
      target: target.id,
      rawTargetId: target.targetId,
      document: prepared.document,
      cursor: prepared.cursor,
      name: { id: name.id, incarnation: name.incarnation, value: preparedName.value },
      submit: { id: submit.id, incarnation: submit.incarnation },
      sentinel,
      lifecycle
    }, null, 2));
    process.exit(0);
  }

  if (mode === 'verify') {
    const expectedArg = process.argv[3] || '{}';
    const expected = expectedArg.startsWith('@') ? JSON.parse(readFileSync(expectedArg.slice(1), 'utf8')) : JSON.parse(expectedArg);
    const status = await browser.status();
    const extensions = await browser.extensions();
    const bridge = extensions.find(x => x.name === 'eyebrowse agent bridge' && x.enabled);
    if (!bridge) fail('Configured eyeBROWSE bridge is not enabled after kernel restart.');

    const targets = await browser.targets();
    const target = targets.find(x => x.id === expected.target);
    if (!target) fail(`Expected logical target ${expected.target} was not restored.`);
    if (target.targetId !== expected.rawTargetId) fail(`Raw target changed: ${target.targetId} != ${expected.rawTargetId}`);

    const recovered = await browser.observe(target.id);
    const recoveredName = await browser.inspect(expected.name.id);
    const recoveredSubmit = await browser.inspect(expected.submit.id);
    if (recovered.document !== expected.document) fail(`Document identity changed: ${recovered.document} != ${expected.document}`);
    if (recoveredName.id !== expected.name.id || recoveredSubmit.id !== expected.submit.id) fail('Exact element identities were not restored.');
    if (recoveredName.value !== expected.sentinel) fail(`Recovered field value changed: ${recoveredName.value} != ${expected.sentinel}`);

    const since = recovered.cursor;
    const afterValue = `${expected.sentinel}-after-restart`;
    await browser.fill(expected.name.id, afterValue);
    const delta = await browser.delta(target.id, since);
    const finalName = await browser.inspect(expected.name.id);
    const changed = delta.changed?.some(x => x.id === expected.name.id) ?? false;
    if (!changed) fail(`No post-restart semantic delta was emitted for ${expected.name.id}.`);
    if (finalName.value !== afterValue) fail('Old recovered element ID did not actuate the surviving control.');

    console.log(JSON.stringify({
      mode,
      status,
      bridge,
      target: target.id,
      rawTargetId: target.targetId,
      document: recovered.document,
      name: { id: recoveredName.id, incarnation: recoveredName.incarnation, beforeValue: recoveredName.value, afterValue: finalName.value },
      submit: { id: recoveredSubmit.id, incarnation: recoveredSubmit.incarnation },
      delta: { since: delta.since, cursor: delta.cursor, changed: delta.changed },
      exactTarget: target.id === expected.target && target.targetId === expected.rawTargetId,
      exactDocument: recovered.document === expected.document,
      exactName: recoveredName.id === expected.name.id,
      exactSubmit: recoveredSubmit.id === expected.submit.id,
      oldIdActionSucceeded: finalName.value === afterValue,
      postRestartDelta: changed
    }, null, 2));
    process.exit(0);
  }

  fail(`Unknown mode: ${mode}`);
} finally {
  browser.close();
}