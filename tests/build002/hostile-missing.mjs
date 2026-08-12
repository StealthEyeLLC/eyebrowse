import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const browser = await EyeBrowse.connect();
const fail = message => { throw new Error(message); };
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

async function openReady(url, host, path) {
  const t = (await browser.open(url)).target;
  await browser.activate(t.id);
  await browser.wait(t.id, `location.hostname === ${JSON.stringify(host)} && location.pathname === ${JSON.stringify(path)} && document.readyState === 'complete'`, 10000, 50);
  return t;
}

async function processInfo() {
  const result = await browser.cdp('SystemInfo.getProcessInfo', {});
  return result.processInfo ?? [];
}

async function identifyBusyRenderer(target, ms = 650) {
  const before = await processInfo();
  const beforeMap = new Map(before.filter(x => x.type === 'renderer').map(x => [String(x.id), Number(x.cpuTime || 0)]));
  await browser.jsValue(target, `window.slowFixture.block(${ms})`);
  const after = await processInfo();
  const rows = after.filter(x => x.type === 'renderer').map(x => ({ id: String(x.id), cpuBefore: beforeMap.get(String(x.id)) ?? 0, cpuAfter: Number(x.cpuTime || 0), delta: Number(x.cpuTime || 0) - (beforeMap.get(String(x.id)) ?? 0) })).sort((a,b)=>b.delta-a.delta);
  return { winner: rows[0] ?? null, top: rows.slice(0, 5) };
}

try {
  const status = await browser.status();
  const extensions = await browser.extensions();
  const bridge = extensions.find(x => x.name === 'eyebrowse agent bridge' && x.enabled);
  if (!bridge) fail('core bridge not enabled');

  // Renderer-process pressure: same logical target, cross-site document, independently map renderer by CPU delta.
  const rendererTarget = await openReady('http://127.0.0.1:18762/slow?renderer=a', '127.0.0.1', '/slow');
  const rendererBeforeSurface = await browser.observe(rendererTarget.id);
  const rendererA = await identifyBusyRenderer(rendererTarget.id);
  if (!rendererA.winner || rendererA.winner.delta < 0.2) fail(`could not identify first active renderer: ${JSON.stringify(rendererA)}`);
  await browser.navigate(rendererTarget.id, 'http://127.0.0.2:18762/slow?renderer=b');
  await browser.wait(rendererTarget.id, "location.hostname === '127.0.0.2' && location.pathname === '/slow' && document.readyState === 'complete'", 10000, 50);
  const rendererAfterSurface = await browser.observe(rendererTarget.id);
  const rendererB = await identifyBusyRenderer(rendererTarget.id);
  if (!rendererB.winner || rendererB.winner.delta < 0.2) fail(`could not identify second active renderer: ${JSON.stringify(rendererB)}`);
  const rendererPass = rendererBeforeSurface.target === rendererAfterSurface.target && rendererBeforeSurface.document !== rendererAfterSurface.document && rendererA.winner.id !== rendererB.winner.id;
  if (!rendererPass) fail(`renderer swap failed: ${rendererA.winner?.id} -> ${rendererB.winner?.id}; ${rendererBeforeSurface.document} -> ${rendererAfterSurface.document}`);

  // Popup / new-tab pressure through a semantic button.
  const popupPrimary = await openReady('http://127.0.0.1:18762/hostile/popup', '127.0.0.1', '/hostile/popup');
  const popupSurface = await browser.observe(popupPrimary.id);
  const popupButton = (await browser.query({ target: popupPrimary.id, role: 'button', name: 'Open popup', limit: 10 }))[0];
  if (!popupButton) fail('semantic popup button missing');
  const beforeTargets = new Set((await browser.targets()).map(x => x.id));
  await browser.click(popupButton.id);
  let popup = null;
  const popupDeadline = Date.now() + 10000;
  while (Date.now() < popupDeadline && !popup) {
    popup = (await browser.targets()).find(x => !beforeTargets.has(x.id) && String(x.url || '').includes('/horizontal/export') && String(x.url || '').includes('popup=1')) ?? null;
    if (!popup) await sleep(50);
  }
  if (!popup) fail('popup target was not discovered');
  const popupNewSurface = await browser.observe(popup.id);
  await browser.activate(popupPrimary.id);
  const popupCurrent = await browser.current();
  const popupPass = popupPrimary.id !== popup.id && String(popupButton.id).startsWith('e_') && String(popupNewSurface.target).startsWith('t_') && popupCurrent.target === popupPrimary.id;
  if (!popupPass) fail('popup/new-tab invariants failed');

  // Target replacement / wrong-object pressure.
  const oldTarget = await openReady('http://127.0.0.1:18762/identity?replacement=old', '127.0.0.1', '/identity');
  const oldSurface = await browser.observe(oldTarget.id);
  const oldElement = (await browser.query({ target: oldTarget.id, role: 'button', name: 'Stable action', limit: 10 }))[0];
  if (!oldElement) fail('replacement baseline element missing');
  const rawTargetId = oldTarget.targetId;
  const closeResult = await browser.cdp('Target.closeTarget', { targetId: rawTargetId });
  const closeDeadline = Date.now() + 10000;
  while (Date.now() < closeDeadline && (await browser.targets()).some(x => x.id === oldTarget.id)) await sleep(50);
  const oldStillPresent = (await browser.targets()).some(x => x.id === oldTarget.id);
  let oldInspect = null;
  let oldInspectError = null;
  let oldIdentity = null;
  let oldIdentityError = null;
  let oldClickError = null;
  try { oldInspect = await browser.inspect(oldElement.id); } catch (error) { oldInspectError = String(error?.message || error); }
  try { oldIdentity = await browser.identity(oldElement.id); } catch (error) { oldIdentityError = String(error?.message || error); }
  try { await browser.click(oldElement.id); } catch (error) { oldClickError = String(error?.message || error); }
  const replacement = await openReady('http://127.0.0.1:18762/identity?replacement=new', '127.0.0.1', '/identity');
  const replacementSurface = await browser.observe(replacement.id);
  const replacementElement = (await browser.query({ target: replacement.id, role: 'button', name: 'Stable action', limit: 10 }))[0];
  if (!replacementElement) fail('replacement new element missing');
  const staleInspectPass = oldInspect?.identity === 'stale' && Array.isArray(oldInspect?.actions) && oldInspect.actions.length === 0;
  const staleIdentityPass = oldIdentity?.outcome === 'stale' && oldIdentity?.backendNodeId == null;
  const staleActuationPass = typeof oldClickError === 'string' && /stale/i.test(oldClickError);
  const replacementPass = closeResult?.success !== false && !oldStillPresent && staleInspectPass && staleIdentityPass && staleActuationPass && replacement.id !== oldTarget.id && replacementSurface.document !== oldSurface.document && replacementElement.id !== oldElement.id;
  if (!replacementPass) fail(`target replacement wrong-object defense failed: ${JSON.stringify({ closeResult, oldStillPresent, oldInspect, oldInspectError, oldIdentity, oldIdentityError, oldClickError, staleInspectPass, staleIdentityPass, staleActuationPass, oldTarget:oldTarget.id, replacement:replacement.id, oldDocument:oldSurface.document, newDocument:replacementSurface.document, oldElement:oldElement.id, newElement:replacementElement.id })}`);

  console.log(JSON.stringify({
    ok: true,
    status: { kernelPid: status.kernelPid, browserId: status.browserId, port: status.port, browserVersion: status.browserVersion },
    bridge: { id: bridge.id, path: bridge.path },
    renderer: {
      pass: rendererPass,
      target: rendererBeforeSurface.target,
      beforeDocument: rendererBeforeSurface.document,
      afterDocument: rendererAfterSurface.document,
      beforeOrigin: 'http://127.0.0.1:18762',
      afterOrigin: 'http://127.0.0.2:18762',
      beforeRenderer: rendererA.winner,
      afterRenderer: rendererB.winner,
      beforeTop: rendererA.top,
      afterTop: rendererB.top
    },
    popup: {
      pass: popupPass,
      primaryTarget: popupPrimary.id,
      primaryDocument: popupSurface.document,
      button: popupButton.id,
      popupTarget: popup.id,
      popupDocument: popupNewSurface.document,
      popupUrl: popup.url,
      currentAfterReactivate: popupCurrent.target
    },
    replacement: {
      pass: replacementPass,
      closeResult,
      oldTarget: oldTarget.id,
      oldRawTargetId: rawTargetId,
      oldDocument: oldSurface.document,
      oldElement: oldElement.id,
      oldStillPresent,
      oldInspect,
      oldInspectError,
      oldIdentity,
      oldIdentityError,
      oldClickError,
      staleInspectPass,
      staleIdentityPass,
      staleActuationPass,
      newTarget: replacement.id,
      newRawTargetId: replacement.targetId,
      newDocument: replacementSurface.document,
      newElement: replacementElement.id
    }
  }, null, 2));
} finally {
  browser.close();
}