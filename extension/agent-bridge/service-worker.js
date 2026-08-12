chrome.runtime.onInstalled.addListener(() => {});

async function currentContext() {
  const tabs = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  const tab = tabs[0] ?? null;
  if (!tab) return null;

  let targetId = null;
  try {
    const targets = await chrome.debugger.getTargets();
    const match = targets.find(target => target.tabId === tab.id && target.type === 'page');
    targetId = match?.id ?? null;
  } catch {}

  return {
    tabId: tab.id ?? null,
    windowId: tab.windowId ?? null,
    targetId,
    url: tab.url ?? '',
    title: tab.title ?? '',
    discarded: Boolean(tab.discarded),
    active: Boolean(tab.active),
    audible: Boolean(tab.audible),
    frozen: Boolean(tab.frozen)
  };
}

async function tabInventory() {
  const tabs = await chrome.tabs.query({});
  let targets = [];
  try { targets = await chrome.debugger.getTargets(); } catch {}
  const byTab = new Map(targets.filter(x => x.tabId != null).map(x => [x.tabId, x]));
  return tabs.map(tab => ({
    tabId: tab.id ?? null,
    windowId: tab.windowId ?? null,
    targetId: tab.id != null ? (byTab.get(tab.id)?.id ?? null) : null,
    url: tab.url ?? '',
    title: tab.title ?? '',
    active: Boolean(tab.active),
    discarded: Boolean(tab.discarded),
    frozen: Boolean(tab.frozen)
  }));
}

globalThis.__eyebrowseExtensionBridge = Object.freeze({
  version: 2,
  currentContext,
  tabInventory
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'eyebrowse.ping') {
    sendResponse({
      ok: true,
      version: chrome.runtime.getManifest().version,
      sender: sender?.tab?.id ?? null
    });
    return;
  }

  if (message?.type === 'eyebrowse.current-context') {
    currentContext().then(sendResponse, error => sendResponse({ ok: false, error: String(error) }));
    return true;
  }
});