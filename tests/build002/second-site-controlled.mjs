import { EyeBrowse } from '../../program-host/sdk/eyebrowse.mjs';

const browser = await EyeBrowse.connect();
const base = 'http://127.0.0.2:18762';
const expectedHost = '127.0.0.2';
const fail = message => { throw new Error(message); };

const pick = async (target, role, name) => {
  const rows = await browser.query({ target, role, limit: 100 });
  return rows.find(x => x.name === name) ?? rows.find(x => String(x.name || '').includes(name)) ?? null;
};

const openReady = async (url, expectedPath) => {
  const target = (await browser.open(url)).target;
  await browser.activate(target.id);
  const deadline = Date.now() + 10000;
  while (Date.now() < deadline) {
    const info = (await browser.targets()).find(x => x.id === target.id);
    if (info?.url && info.url !== 'about:blank') {
      try {
        const parsed = new URL(info.url);
        if (parsed.hostname === expectedHost && parsed.pathname === expectedPath) break;
      } catch {}
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  await browser.wait(
    target.id,
    `location.hostname === ${JSON.stringify(expectedHost)} && location.pathname === ${JSON.stringify(expectedPath)} && document.readyState === 'complete'`,
    10000,
    50
  );
  return target;
};

try {
  const unauth = await openReady(`${base}/second-mail`, '/second-mail/sign-in');
  const unauthUrl = await browser.jsValue(unauth.id, 'location.href');
  if (!String(unauthUrl).endsWith('/second-mail/sign-in')) fail(`unauthenticated route did not redirect: ${unauthUrl}`);

  await browser.navigate(unauth.id, `${base}/second-mail/login`);
  await browser.wait(
    unauth.id,
    `location.hostname === ${JSON.stringify(expectedHost)} && location.pathname === '/second-mail' && document.readyState === 'complete'`,
    10000,
    50
  );
  const primary = unauth.id;
  const primarySurface = await browser.observe(primary);
  const authState = await browser.jsValue(primary, "document.querySelector('#auth-state')?.textContent");
  const primaryOrigin = await browser.jsValue(primary, 'location.origin');
  const initialCount = await browser.jsValue(primary, "document.querySelector('#mail-count')?.textContent");
  if (authState !== 'authenticated') fail(`primary not authenticated: ${authState}`);
  if (primaryOrigin !== base) fail(`primary origin changed: ${primaryOrigin}`);
  if (initialCount !== 'showing 25 of 240') fail(`virtualized inbox baseline unexpected: ${initialCount}`);

  const second = await openReady(`${base}/second-mail`, '/second-mail');
  const secondUrl = await browser.jsValue(second.id, 'location.href');
  const secondOrigin = await browser.jsValue(second.id, 'location.origin');
  const secondAuth = await browser.jsValue(second.id, "document.querySelector('#auth-state')?.textContent ?? null");
  if (!String(secondUrl).endsWith('/second-mail') || secondOrigin !== base || secondAuth !== 'authenticated') {
    fail(`auth cookie did not persist in second tab: ${secondUrl} / ${secondOrigin} / ${secondAuth}`);
  }

  await browser.activate(primary);
  const search = await pick(primary, 'textbox', 'Search mail');
  if (!search) fail('Search mail semantic control missing');
  await browser.fill(search.id, 'Invoice');
  await browser.wait(primary, "document.querySelector('#mail-count')?.textContent === 'showing 6 of 6'", 10000, 50);
  const afterSearch = await browser.observe(primary);
  const searchedCount = await browser.jsValue(primary, "document.querySelector('#mail-count')?.textContent");
  const messageButtons = await browser.query({ target: primary, role: 'button', limit: 100 });
  const invoice = messageButtons.find(x => String(x.name || '').includes('Open Invoice'));
  if (!invoice) fail('No semantic invoice button after search');
  await browser.click(invoice.id);
  await browser.wait(primary, "document.querySelector('#mail-status')?.textContent.startsWith('opened:')", 10000, 50);
  await browser.observe(primary);
  const openedStatus = await browser.jsValue(primary, "document.querySelector('#mail-status')?.textContent");
  const messageText = await browser.jsValue(primary, "document.querySelector('#message')?.innerText");
  const attachment = await pick(primary, 'link', 'Download attachment');
  if (!attachment) fail('Attachment link missing after message open');
  const attachmentUrl = await browser.jsValue(primary, "document.querySelector('#message a[download]')?.href");
  if (!String(attachmentUrl).startsWith(`${base}/second-mail/attachment/`)) fail(`attachment escaped second-site origin: ${attachmentUrl}`);

  const compose = await pick(primary, 'button', 'Compose');
  if (!compose) fail('Compose semantic control missing');
  await browser.click(compose.id);
  await browser.observe(primary);
  const to = await pick(primary, 'textbox', 'To');
  const subject = await pick(primary, 'textbox', 'Subject');
  const body = await pick(primary, 'textbox', 'Body');
  const send = await pick(primary, 'button', 'Send');
  if (!to || !subject || !body || !send) fail('Compose semantic controls incomplete');
  await browser.fill(to.id, 'recipient@example.test');
  await browser.fill(subject.id, 'Second-site deterministic message');
  await browser.fill(body.id, 'Rich text body from semantic contenteditable control.');
  await browser.click(send.id);
  await browser.wait(primary, "document.querySelector('#mail-status')?.textContent.startsWith('sent:')", 10000, 50);
  const sentStatus = await browser.jsValue(primary, "document.querySelector('#mail-status')?.textContent");
  const composerHidden = await browser.jsValue(primary, "document.querySelector('#composer')?.hidden");
  const sent = JSON.parse(String(sentStatus).slice(5));
  const current = await browser.current();
  const cookies = (await browser.cdp('Storage.getCookies', {})).cookies ?? [];
  const authCookies = cookies.filter(x => x.name === 'second_mail_auth').map(x => ({ name: x.name, value: x.value, domain: x.domain, path: x.path, httpOnly: x.httpOnly, sameSite: x.sameSite }));

  console.log(JSON.stringify({
    ok: true,
    unauthenticated: { target: unauth.id, url: unauthUrl },
    authenticated: { target: primary, document: primarySurface.document, origin: primaryOrigin, authState, initialCount },
    persistence: { secondTarget: second.id, url: secondUrl, origin: secondOrigin, authState: secondAuth, authCookies },
    search: { control: search.id, count: searchedCount, messageButtons: messageButtons.length, selected: invoice.id, selectedName: invoice.name, document: afterSearch.document },
    message: { status: openedStatus, text: messageText, attachmentId: attachment.id, attachmentUrl },
    compose: { composeId: compose.id, toId: to.id, subjectId: subject.id, bodyId: body.id, sendId: send.id, sent, composerHidden },
    currentTarget: current.target
  }, null, 2));
} finally {
  browser.close();
}