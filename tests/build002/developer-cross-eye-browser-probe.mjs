import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const mode = process.argv[2] || 'baseline';
const existingTarget = process.argv[3] || null;
const priorConsoleMax = Number(process.argv[4] || 0);
const priorExceptionMax = Number(process.argv[5] || 0);
const browser = await EyeBrowse.connect();

function findHeader(value, name) {
  if (!value || typeof value !== 'object') return null;
  const wanted = name.toLowerCase();
  for (const [key, item] of Object.entries(value)) {
    if (key.toLowerCase() === wanted) return String(item);
    if (item && typeof item === 'object') {
      const nested = findHeader(item, name);
      if (nested) return nested;
    }
  }
  return null;
}

try {
  const url = `http://127.0.0.1:18762/cross-eye?mode=${encodeURIComponent(mode)}&nonce=${Date.now()}`;
  let target;
  if (existingTarget) {
    target = existingTarget;
    await browser.navigate(target, url);
  } else {
    target = (await browser.open(url)).target.id;
  }
  await browser.activate(target);
  await browser.wait(target, `location.pathname === '/cross-eye' && document.readyState === 'complete'`, 10000, 50);
  await new Promise(resolve => setTimeout(resolve, 150));
  const surface = await browser.observe(target);
  await browser.runtimeDebugEnable(target);
  const scripts = await browser.runtimeScripts(target, 'cross-eye.js', 100);
  const script = scripts.find(x => String(x.url || '').includes('/cross-eye.js')) || scripts.at(-1) || null;
  const source = script ? await browser.runtimeScriptSource(target, script.scriptId) : null;
  const fetchEvidence = await browser.jsValue(target, `(async () => { const response = await fetch('/cross-eye.js?evidence=' + Date.now(), { cache: 'no-store' }); return { hash: response.headers.get('X-EyeBrowse-Source-Sha256'), text: await response.text() }; })()`);
  await new Promise(resolve => setTimeout(resolve, 100));
  const consoleEntries = await browser.console(target, 500);
  const exceptions = await browser.exceptions(target, 500);
  const requests = await browser.network({ target, contains: '/cross-eye.js', limit: 100 });
  const request = requests.at(-1) || null;
  const detail = request ? await browser.networkDetail(request.id) : null;
  const state = await browser.jsValue(target, 'globalThis.crossEyeFixture ?? null');
  const statusText = await browser.jsValue(target, `document.querySelector('#cross-eye-status')?.textContent ?? null`);
  const newConsole = consoleEntries.filter(x => Number(x.id || 0) > priorConsoleMax);
  const newExceptions = exceptions.filter(x => Number(x.id || 0) > priorExceptionMax);
  const errorConsole = newConsole.filter(x => /error|warning/i.test(String(x.level || x.source || '')));
  const diagnosticText = JSON.stringify([...errorConsole, ...newExceptions]);
  const sourceText = JSON.stringify(source || {});
  const out = {
    mode,
    target,
    document: surface.document,
    url: surface.url,
    state,
    statusText,
    scripts,
    source,
    sourceHash: fetchEvidence?.hash || findHeader(detail, 'X-EyeBrowse-Source-Sha256'),
    fetchedSourceContainsControlledFailure: String(fetchEvidence?.text || '').includes('Build002 cross-Eye controlled source failure'),
    fetchedSourceContainsFixedMarker: String(fetchEvidence?.text || '').includes('Build002 cross-Eye source fixed'),
    request,
    requestDetail: detail,
    consoleMax: Math.max(0, ...consoleEntries.map(x => Number(x.id || 0))),
    exceptionMax: Math.max(0, ...exceptions.map(x => Number(x.id || 0))),
    newConsole,
    newExceptions,
    newErrorCount: errorConsole.length + newExceptions.length,
    diagnosedControlledFailure: diagnosticText.includes('Build002 cross-Eye controlled source failure'),
    sourceContainsControlledFailure: sourceText.includes('Build002 cross-Eye controlled source failure'),
    sourceContainsFixedMarker: sourceText.includes('Build002 cross-Eye source fixed')
  };
  console.log(JSON.stringify(out, null, 2));
} finally {
  browser.close();
}
