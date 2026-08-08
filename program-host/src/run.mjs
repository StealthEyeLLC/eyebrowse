import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { EyeBrowse } from '../sdk/eyebrowse.mjs';

const programPath = process.argv[2];
if (!programPath) {
  console.error('Usage: node src/run.mjs <program.mjs>');
  process.exit(2);
}

const browser = await EyeBrowse.connect();
try {
  const module = await import(pathToFileURL(resolve(programPath)).href);
  if (typeof module.default !== 'function') {
    throw new TypeError('Program module must default-export an async function(browser).');
  }

  const started = performance.now();
  const result = await module.default(browser);
  const elapsedMs = Math.round(performance.now() - started);
  console.log(JSON.stringify({
    ok: true,
    kernelOperations: browser.operationCount,
    elapsedMs,
    result
  }, null, 2));
} catch (error) {
  console.error(JSON.stringify({
    ok: false,
    kernelOperations: browser.operationCount,
    error: { name: error?.name, type: error?.type, message: error?.message, stack: error?.stack }
  }, null, 2));
  process.exitCode = 1;
} finally {
  browser.close();
}