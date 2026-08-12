import lighthouse from 'lighthouse';
import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { targetFromArgs } from '../common/lib.mjs';

export default async function lighthouseAudit(browser, args = {}) {
  const target = await targetFromArgs(browser, args);
  const context = await browser.current();
  const status = await browser.status();
  const url = typeof args.url === 'string' && args.url ? args.url : (context.url || context.Url);
  const port = Number(status.port ?? status.Port);
  if (!url) throw new Error('No current URL is available for Lighthouse.');
  if (!Number.isInteger(port) || port <= 0) throw new Error('The current eyeBROWSE browser did not expose a valid CDP port.');
  const device = args.device === 'mobile' ? 'mobile' : 'desktop';
  const categories = Array.isArray(args.categories) && args.categories.length
    ? args.categories
    : ['accessibility', 'seo', 'best-practices', 'agentic-browsing'];
  const artifactRoot = status.artifactRoot || status.ArtifactRoot;
  if (!artifactRoot) throw new Error('The current eyeBROWSE runtime did not expose an artifact root.');
  const outputDir = join(artifactRoot, 'lighthouse', `${Date.now()}-${Math.random().toString(36).slice(2)}`);
  await mkdir(outputDir, { recursive: true });

  const flags = {
    port,
    onlyCategories: categories,
    output: ['json', 'html'],
    logLevel: 'error',
    maxWaitForLoad: Number(args.maxWaitForLoad || 30000),
    formFactor: device,
    screenEmulation: device === 'desktop'
      ? { mobile: false, width: 1350, height: 940, deviceScaleFactor: 1, disabled: false }
      : { mobile: true, width: 412, height: 823, deviceScaleFactor: 1.75, disabled: false }
  };

  let result;
  try {
    result = await lighthouse(url, flags);
    if (!result?.lhr) throw new Error('Lighthouse returned no LHR.');
  } finally {
    await browser.emulateReset(target).catch(() => null);
  }

  const reports = Array.isArray(result.report) ? result.report : [result.report];
  const jsonPath = join(outputDir, 'report.json');
  const htmlPath = join(outputDir, 'report.html');
  if (typeof reports[0] === 'string') await writeFile(jsonPath, reports[0], 'utf8');
  else await writeFile(jsonPath, JSON.stringify(result.lhr), 'utf8');
  if (typeof reports[1] === 'string') await writeFile(htmlPath, reports[1], 'utf8');

  const jsonArtifact = await browser.registerArtifact('lighthouse-json', jsonPath, target, url);
  const htmlArtifact = typeof reports[1] === 'string'
    ? await browser.registerArtifact('lighthouse-html', htmlPath, target, url)
    : null;

  const lhr = result.lhr;
  const scores = Object.values(lhr.categories || {}).map(category => ({
    id: category.id,
    title: category.title,
    score: category.score,
    auditCount: category.auditRefs?.length || 0
  }));
  const audits = Object.values(lhr.audits || {});
  const failed = audits.filter(a => a.score !== null && a.score < 1);
  const passed = audits.filter(a => a.score === 1);
  const agenticRefs = new Set(lhr.categories?.['agentic-browsing']?.auditRefs?.map(x => x.id) || []);
  const agentic = audits
    .filter(a => agenticRefs.has(a.id))
    .map(a => ({ id: a.id, title: a.title, score: a.score, scoreDisplayMode: a.scoreDisplayMode, displayValue: a.displayValue, description: a.description }))
    .slice(0, 100);
  const findings = failed
    .map(a => ({ id: a.id, title: a.title, score: a.score, displayValue: a.displayValue, description: a.description }))
    .slice(0, Number(args.maxFindings || 100));

  return {
    target,
    url: lhr.mainDocumentUrl || url,
    requestedUrl: lhr.requestedUrl || url,
    finalDisplayedUrl: lhr.finalDisplayedUrl || null,
    lighthouseVersion: lhr.lighthouseVersion,
    device,
    categories,
    scores,
    auditCounts: { passed: passed.length, failed: failed.length, total: audits.length },
    agenticBrowsing: agentic,
    findings,
    timingMs: lhr.timing?.total ?? null,
    artifacts: [jsonArtifact, htmlArtifact].filter(Boolean)
  };
}
