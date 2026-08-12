import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const browser = await EyeBrowse.connect();
const results = [];
async function step(name, fn) {
  const started = performance.now();
  try {
    const value = await fn();
    results.push({ name, ok: true, elapsedMs: Math.round(performance.now()-started), value });
    return value;
  } catch (error) {
    results.push({ name, ok: false, elapsedMs: Math.round(performance.now()-started), error: error?.message, type: error?.type });
    return null;
  }
}
const sleep = ms => new Promise(r=>setTimeout(r,ms));

try {
  const status = await step('browser.status', ()=>browser.status());
  const slowOpen = await step('open.slow', ()=>browser.open('http://127.0.0.1:18762/slow'));
  const slow = slowOpen?.target?.id || slowOpen?.target?.Id;
  if (slow) {
    await step('slow.activate', ()=>browser.activate(slow));
    await step('slow.observe', ()=>browser.observe(slow));
    await step('performance.timeline.enable', ()=>browser.performanceTimelineEnable(slow,['largest-contentful-paint','layout-shift','longtask']));
    await step('performance.trace.start', ()=>browser.traceStart(slow));
    await step('slow.exercise', ()=>browser.jsValue(slow,'window.slowFixture.block(140)'));
    await step('slow.fetch', ()=>browser.jsValue(slow,'window.slowFixture.fetchDelay(180)'));
    await sleep(100);
    await step('performance.trace.stop', ()=>browser.traceStop(slow,60000));
    await step('performance.timeline.list', ()=>browser.performanceTimeline(slow,undefined,50));
    await step('performance.metrics', ()=>browser.performance(slow));
    const reqs = await step('network.search', ()=>browser.network({target:slow,contains:'/api/slow',limit:10}));
    const req = reqs?.at?.(-1);
    if (req?.id) {
      await step('network.detail', ()=>browser.networkDetail(req.id));
      await step('network.search_body', ()=>browser.networkSearchBody(req.id,'delayedMs'));
      await step('network.body.save', ()=>browser.networkBodySave(req.id));
    }
    await step('emulate.viewport', ()=>browser.emulateViewport(slow,800,600,{deviceScaleFactor:1}));
    await step('screenshot.region', ()=>browser.screenshotRegion(slow,0,0,400,300));
    await step('emulate.reset', ()=>browser.emulateReset(slow));
  }

  const a11yOpen = await step('open.a11y', ()=>browser.open('http://127.0.0.1:18762/a11y'));
  const a11y = a11yOpen?.target?.id;
  if (a11y) {
    await step('a11y.observe', ()=>browser.observe(a11y));
    await step('accessibility.audit', ()=>browser.accessibilityAudit(a11y));
  }

  const memoryOpen = await step('open.memory', ()=>browser.open('http://127.0.0.1:18762/memory'));
  const memory = memoryOpen?.target?.id;
  if (memory) {
    await step('memory.observe', ()=>browser.observe(memory));
    await step('memory.current.before', ()=>browser.memoryCurrent(memory));
    await step('memory.exercise', ()=>browser.jsValue(memory,'window.leakFixture(5)'));
    await step('memory.sampling.start', ()=>browser.memorySamplingStart(memory,65536,64));
    await step('memory.exercise.sampled', ()=>browser.jsValue(memory,'window.leakFixture(5)'));
    await step('memory.sampling.stop', ()=>browser.memorySamplingStop(memory));
  }

  const dialogOpen = await step('open.dialog', ()=>browser.open('http://127.0.0.1:18762/dialog'));
  const dialog = dialogOpen?.target?.id;
  if (dialog) {
    await step('dialog.observe', ()=>browser.observe(dialog));
    await step('dialog.trigger', ()=>browser.jsValue(dialog,"setTimeout(()=>alert('dva-dialog'),0); true"));
    await sleep(100);
    await step('dialog.current', ()=>browser.dialog(dialog));
    await step('dialog.handle', ()=>browser.handleDialog(dialog,true));
  }

  const wmOpen = await step('open.webmcp', ()=>browser.open('http://127.0.0.1:18762/webmcp'));
  const wm = wmOpen?.target?.id;
  if (wm) {
    await step('webmcp.observe', ()=>browser.observe(wm));
    const tools = await step('webmcp.list', ()=>browser.webmcp(wm));
    if (tools?.length) await step('webmcp.inspect.first', ()=>browser.webmcpInspect(wm,tools[0].name,tools[0].frameId));
  }

  const rtOpen = await step('open.runtime-tools', ()=>browser.open('http://127.0.0.1:18762/runtime-tools'));
  const rt = rtOpen?.target?.id;
  if (rt) {
    await step('runtime-tools.observe', ()=>browser.observe(rt));
    await step('runtime-tools.list', ()=>browser.runtimeTools(rt));
    await step('runtime-debug.enable', ()=>browser.runtimeDebugEnable(rt));
    await step('runtime-debug.scripts', ()=>browser.runtimeScripts(rt,'runtime-tools',50));
  }

  await step('extension.list', ()=>browser.extensions());
  await step('browser.capabilities.performance', ()=>browser.capabilities('PerformanceTimeline'));
  await step('artifact.list', ()=>browser.artifacts());
} finally {
  browser.close();
}

const failures = results.filter(x=>!x.ok);
console.log(JSON.stringify({ok:failures.length===0,failures:failures.length,results},null,2));
process.exitCode = failures.length ? 1 : 0;
