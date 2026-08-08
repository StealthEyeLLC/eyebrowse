export default async function milestoneD(browser) {
  const status = await browser.status();

  const opened = await browser.open(`https://example.com/?eyebrowse=milestone-d-${Date.now()}`);
  const target = opened.target.Id;
const ready = await browser.wait(target, "location.href.includes('eyebrowse=milestone-d-') && document.readyState === 'complete'", 10000, 50);
  if (!ready.matched) throw new Error('Milestone D primary page did not become ready.');

  await browser.jsValue(target, `(() => {
    document.body.innerHTML = '<main><h1>eyebrowse Program Host Fixture</h1><label>Program input <input id="program-input"></label><div id="program-items"></div></main>';
    const host = document.getElementById('program-items');
    for (let i = 1; i <= 12; i++) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'Program item ' + i;
      button.dataset.index = String(i);
      button.addEventListener('click', () => {
        button.dataset.hit = '1';
        button.textContent = 'Clicked program item ' + i;
      });
      host.appendChild(button);
    }
    return { buttons: host.children.length };
  })()`);

  const baseline = await browser.observe(target);
  const textboxMatches = await browser.query({ target, role: 'textbox', contains: 'Program input', limit: 5 });
  if (textboxMatches.length !== 1) throw new Error(`Expected one program textbox, saw ${textboxMatches.length}.`);
  await browser.fill(textboxMatches[0].Id, 'program-host-ran');

  const buttons = await browser.query({ target, role: 'button', contains: 'Program item', limit: 20 });
  if (buttons.length !== 12) throw new Error(`Expected 12 program buttons, saw ${buttons.length}.`);

  for (const button of buttons) {
    await browser.click(button.Id);
  }

  await browser.jsValue(target, `(() => {
    setTimeout(() => document.body.dataset.eyebrowseProgramDone = 'yes', 300);
    return 'scheduled';
  })()`);

  const delayed = await browser.wait(
    target,
    "document.body.dataset.eyebrowseProgramDone === 'yes'",
    5000,
    50
  );
  if (!delayed.matched || delayed.elapsedMs < 200) {
    throw new Error(`Browser-resident wait did not actually wait: ${JSON.stringify(delayed)}`);
  }

  const delta = await browser.delta(target, baseline.Cursor);
  const primarySummary = await browser.jsValue(target, `({
    input: document.getElementById('program-input').value,
    hitCount: document.querySelectorAll('button[data-hit="1"]').length,
    clickedLabels: [...document.querySelectorAll('button')].map(x => x.textContent)
  })`);

  const second = await browser.open('https://github.com/StealthEyeLLC/eyebrowse');
  const secondTarget = second.target.Id;
  const secondReady = await browser.wait(secondTarget, "document.readyState === 'complete'", 15000, 100);
  if (!secondReady.matched) throw new Error('GitHub tab did not become ready.');
  const secondSummary = await browser.jsValue(secondTarget, `({
    title: document.title,
    url: location.href,
    linkCount: document.links.length
  })`);

  const targets = await browser.targets();
  const browserVersion = await browser.cdp('Browser.getVersion');
  const cookies = await browser.cdp('Storage.getCookies');

  return {
    primary: {
      target,
      document: baseline.Document,
      baselineCursor: baseline.Cursor,
      deltaCursor: delta.Cursor,
      changedObjects: delta.Changed.length,
      summary: primarySummary
    },
    wait: delayed,
    secondTab: { target: secondTarget, ...secondSummary },
    liveTargets: targets.length,
    browserProduct: browserVersion.product,
    cookieCount: cookies.cookies?.length ?? 0,
    kernelPid: status.kernelPid
  };
}
