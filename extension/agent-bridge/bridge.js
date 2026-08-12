(() => {
  if (Number(globalThis.__eyebrowseIdentity?.version ?? 0) >= 2) return;

  let nextSerial = 1;
  let sequence = 0;
  const nodeToSerial = new WeakMap();
  const serialToNode = new Map();
  const serialToLogical = new Map();
  const logicalToSerial = new Map();
  const logicalToIncarnation = new Map();
  const events = [];
  const maxEvents = 512;
  let documentLogicalId = null;

  const token = globalThis.crypto?.randomUUID?.() ??
    `doc-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;

  function remember(kind, data = null) {
    sequence += 1;
    events.push({ sequence, kind, data, at: performance.now() });
    while (events.length > maxEvents) events.shift();
  }

  function serialFor(node) {
    if (!node || typeof node !== 'object') return null;
    let serial = nodeToSerial.get(node);
    if (serial) return serial;
    serial = nextSerial++;
    nodeToSerial.set(node, serial);
    serialToNode.set(serial, typeof WeakRef === 'function' ? new WeakRef(node) : { deref: () => node });
    return serial;
  }

  function nodeFor(serial) {
    const ref = serialToNode.get(serial);
    const node = ref?.deref?.() ?? null;
    if (!node) {
      serialToNode.delete(serial);
      const logical = serialToLogical.get(serial);
      if (logical) {
        logicalToSerial.delete(logical);
        logicalToIncarnation.delete(logical);
      }
      serialToLogical.delete(serial);
    }
    return node;
  }

  function bind(node, logicalId, incarnation = 1) {
    const serial = serialFor(node);
    if (!serial || !logicalId) return null;
    const previousLogical = serialToLogical.get(serial);
    if (previousLogical && previousLogical !== logicalId) {
      logicalToSerial.delete(previousLogical);
      logicalToIncarnation.delete(previousLogical);
    }
    const previousSerial = logicalToSerial.get(logicalId);
    if (previousSerial && previousSerial !== serial)
      serialToLogical.delete(previousSerial);
    const normalizedIncarnation = Math.max(1, Number(incarnation) || 1);
    serialToLogical.set(serial, logicalId);
    logicalToSerial.set(logicalId, serial);
    logicalToIncarnation.set(logicalId, normalizedIncarnation);
    return { serial, logicalId, incarnation: normalizedIncarnation, documentToken: token, documentLogicalId };
  }

  function lookup(node) {
    const serial = serialFor(node);
    if (!serial) return null;
    const logicalId = serialToLogical.get(serial) ?? null;
    return {
      serial,
      logicalId,
      incarnation: logicalId ? (logicalToIncarnation.get(logicalId) ?? 1) : 1,
      documentToken: token,
      documentLogicalId
    };
  }

  function lookupLogical(logicalId) {
    const serial = logicalToSerial.get(logicalId);
    if (!serial) return null;
    const node = nodeFor(serial);
    if (!node) return null;
    return {
      serial,
      logicalId,
      incarnation: logicalToIncarnation.get(logicalId) ?? 1,
      documentToken: token,
      documentLogicalId
    };
  }

  function exportBindings() {
    const live = [];
    for (const [logicalId, serial] of logicalToSerial.entries()) {
      if (nodeFor(serial)) {
        live.push({
          logicalId,
          serial,
          incarnation: logicalToIncarnation.get(logicalId) ?? 1
        });
      }
    }
    return {
      version: 2,
      documentToken: token,
      documentLogicalId,
      sequence,
      prerendering: Boolean(document.prerendering),
      visibilityState: document.visibilityState,
      wasDiscarded: Boolean(document.wasDiscarded),
      bindings: live
    };
  }

  function setDocumentLogicalId(value) {
    documentLogicalId = value || null;
    return { documentToken: token, documentLogicalId };
  }

  function eventsSince(since = 0) {
    return {
      sequence,
      events: events.filter(event => event.sequence > since)
    };
  }

  const observer = new MutationObserver(records => {
    let added = 0;
    let removed = 0;
    let attributes = 0;
    let text = 0;
    for (const record of records) {
      if (record.type === 'childList') {
        added += record.addedNodes.length;
        removed += record.removedNodes.length;
      } else if (record.type === 'attributes') {
        attributes += 1;
      } else if (record.type === 'characterData') {
        text += 1;
      }
    }
    remember('mutation', { records: records.length, added, removed, attributes, text });
  });

  try {
    observer.observe(document, {
      subtree: true,
      childList: true,
      attributes: true,
      characterData: true
    });
  } catch {}

  document.addEventListener('focusin', event => remember('focus', { serial: serialFor(event.target) }), true);
  document.addEventListener('input', event => remember('input', { serial: serialFor(event.target) }), true);
  document.addEventListener('change', event => remember('change', { serial: serialFor(event.target) }), true);
  document.addEventListener('selectionchange', () => remember('selection'), true);
  document.addEventListener('visibilitychange', () => remember('visibility', { state: document.visibilityState }), true);
  globalThis.addEventListener('scroll', () => remember('scroll', { x: scrollX, y: scrollY }), { passive: true, capture: true });
  globalThis.addEventListener('pagehide', event => remember(event.persisted ? 'bfcache-enter' : 'pagehide', { persisted: Boolean(event.persisted) }), true);
  globalThis.addEventListener('pageshow', event => remember(event.persisted ? 'bfcache-restore' : 'pageshow', { persisted: Boolean(event.persisted) }), true);
  globalThis.addEventListener('freeze', () => remember('freeze'), true);
  globalThis.addEventListener('resume', () => remember('resume'), true);
  document.addEventListener('prerenderingchange', () => remember('prerender-activate', { prerendering: Boolean(document.prerendering) }), true);

  Object.defineProperty(globalThis, '__eyebrowseIdentity', {
    value: Object.freeze({
      version: 2,
      documentToken: token,
      get sequence() { return sequence; },
      serialFor,
      bind,
      lookup,
      lookupLogical,
      exportBindings,
      setDocumentLogicalId,
      eventsSince
    }),
    configurable: false,
    enumerable: false,
    writable: false
  });

  remember('bridge-ready', {
    documentToken: token,
    prerendering: Boolean(document.prerendering),
    visibilityState: document.visibilityState,
    wasDiscarded: Boolean(document.wasDiscarded)
  });
})();