chrome.runtime.onInstalled.addListener(() => {});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'eyebrowse.ping') {
    sendResponse({
      ok: true,
      version: chrome.runtime.getManifest().version,
      sender: sender?.tab?.id ?? null
    });
  }
});
