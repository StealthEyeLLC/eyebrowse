import { readFileSync } from 'node:fs';
import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const browser = await EyeBrowse.connect();
const out = {};
try {
  const targets = await browser.targets();
  const target = targets.find(x => x.url === 'http://127.0.0.1:18762/slow') ?? (await browser.open('http://127.0.0.1:18762/slow')).target;
  out.target = target.id;
  await browser.activate(target.id);
  await browser.observe(target.id);
  out.start = await browser.screencastStart(target.id, { format: 'jpeg', quality: 60, everyNthFrame: 1, maxFrames: 12, maxWidth: 900, maxHeight: 700 });
  await browser.jsValue(target.id, `(() => {
    let n = 0;
    globalThis.__eyebrowseScreencastFixture = setInterval(() => {
      document.body.dataset.screencastFrame = String(++n);
      const heading = document.querySelector('h1');
      if (heading) heading.textContent = 'Performance debugging fixture ' + n;
      if (n > 8) clearInterval(globalThis.__eyebrowseScreencastFixture);
    }, 40);
    return true;
  })()`);
  await new Promise(resolve => setTimeout(resolve, 700));
  out.stop = await browser.screencastStop(target.id);
  out.manifest = JSON.parse(readFileSync(out.stop.path, 'utf8'));
  out.ok = Array.isArray(out.manifest.frames) && out.manifest.frames.length > 0;
  console.log(JSON.stringify(out, null, 2));
  if (!out.ok) process.exitCode = 1;
} finally {
  browser.close();
}
