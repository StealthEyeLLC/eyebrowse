import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { EyeBrowse } from '../sdk/eyebrowse.mjs';
import { programs } from './programs.mjs';

const selector = process.argv[2];
if (!selector) {
  console.error('Usage: node src/run.mjs <program-name|program.mjs> [json-arguments|@arguments.json]');
  process.exit(2);
}

let args = {};
if (process.argv[3]) {
  try {
    const rawArgument = process.argv[3];
    const rawJson = rawArgument.startsWith('@') ? readFileSync(resolve(rawArgument.slice(1)), 'utf8') : rawArgument;
    args = JSON.parse(rawJson.replace(/^\uFEFF/, ''));
  } catch (error) {
    console.error(JSON.stringify({ ok: false, error: { type: 'arguments', message: `Invalid JSON arguments: ${error.message}` } }, null, 2));
    process.exit(2);
  }
}
if (!args || Array.isArray(args) || typeof args !== 'object') {
  console.error(JSON.stringify({ ok: false, error: { type: 'arguments', message: 'Program arguments must be a JSON object.' } }, null, 2));
  process.exit(2);
}

let moduleUrl;
let programName = selector;
if (programs.has(selector)) {
  moduleUrl = new URL(programs.get(selector), import.meta.url);
} else {
  const path = resolve(selector);
  if (!existsSync(path)) {
    console.error(JSON.stringify({ ok: false, error: { type: 'program', message: `Unknown named program '${selector}'.` }, availablePrograms: [...programs.keys()] }, null, 2));
    process.exit(2);
  }
  moduleUrl = pathToFileURL(path);
  programName = path;
}

const browser = await EyeBrowse.connect();
try {
  const module = await import(moduleUrl.href);
  if (typeof module.default !== 'function') throw new TypeError('Program module must default-export an async function(browser, args).');
  const started = performance.now();
  const result = await module.default(browser, args);
  const elapsedMs = Math.round(performance.now() - started);
  console.log(JSON.stringify({ ok: true, program: programName, kernelOperations: browser.operationCount, elapsedMs, result }, null, 2));
} catch (error) {
  console.error(JSON.stringify({
    ok: false,
    program: programName,
    kernelOperations: browser.operationCount,
    error: { name: error?.name, type: error?.type, message: error?.message, stack: error?.stack }
  }, null, 2));
  process.exitCode = 1;
} finally {
  browser.close();
}
