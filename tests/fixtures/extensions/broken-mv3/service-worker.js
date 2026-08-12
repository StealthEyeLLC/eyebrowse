console.error('Build002 extension fixture background loaded');
chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.local.set({ fixtureInstalled: true, fixtureVersion: chrome.runtime.getManifest().version });
});
chrome.action.onClicked.addListener(() => {
  console.error('Build002 controlled action failure');
  throw new Error('Build002 controlled extension action failure');
});
Promise.reject(new Error('Build002 controlled extension startup rejection'));
