import { mkdir, rename, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';

export function requireString(args, name) {
  const value = args?.[name];
  if (typeof value !== 'string' || !value.trim()) throw new TypeError(`${name} must be a non-empty string`);
  return value.trim();
}

export function optionalInt(args, name, fallback, min = 1, max = 10000) {
  const raw = args?.[name];
  if (raw == null) return fallback;
  const value = Number(raw);
  if (!Number.isInteger(value) || value < min || value > max) throw new TypeError(`${name} must be an integer in [${min}, ${max}]`);
  return value;
}

export async function targetFromArgs(browser, args) {
  if (typeof args?.target === 'string' && args.target) return args.target;
  const context = await browser.current();
  if (context?.ambiguous || !context?.target) throw new Error(context?.ambiguityReason || 'No unambiguous current browser target.');
  return context.target;
}

export async function writeTextAtomic(destination, text) {
  const path = resolve(destination);
  await mkdir(dirname(path), { recursive: true });
  const temp = `${path}.eyebrowse-${process.pid}-${Date.now()}.tmp`;
  await writeFile(temp, text, 'utf8');
  await rename(temp, path);
  return path;
}

export function csv(rows) {
  const quote = value => {
    const s = value == null ? '' : String(value);
    return /[",\r\n]/.test(s) ? `"${s.replaceAll('"','""')}"` : s;
  };
  return rows.map(row => row.map(quote).join(',')).join('\r\n') + '\r\n';
}

export function bounded(value, max = 200000) {
  const text = String(value ?? '');
  return text.length <= max ? text : text.slice(0, max) + `\n...[truncated ${text.length-max} chars]`;
}
