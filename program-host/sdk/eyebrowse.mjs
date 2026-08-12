import net from 'node:net';

const pipeName = process.env.EYEBROWSE_PIPE_NAME || 'eyebrowse-dev';
const DEFAULT_PIPE = `\\\\.\\pipe\\${pipeName}`;

export class EyeBrowse {
  #socket;
  #buffer = '';
  #nextId = 0;
  #pending = new Map();
  operationCount = 0;

  constructor(socket) {
    this.#socket = socket;
    socket.setEncoding('utf8');
    socket.on('data', chunk => this.#onData(chunk));
    socket.on('error', error => this.#failAll(error));
    socket.on('close', () => this.#failAll(new Error('eyeBROWSE kernel pipe closed')));
  }

  static async connect(pipe = process.env.EYEBROWSE_PIPE_PATH || DEFAULT_PIPE) {
    const socket = net.createConnection(pipe);
    await new Promise((resolve, reject) => {
      socket.once('connect', resolve);
      socket.once('error', reject);
    });
    return new EyeBrowse(socket);
  }

  async call(method, params = {}) {
    const id = ++this.#nextId;
    this.operationCount += 1;
    const response = new Promise((resolve, reject) => this.#pending.set(id, { resolve, reject }));
    this.#socket.write(JSON.stringify({ id, method, params }) + '\n');
    const message = await response;
    if (!message.ok) {
      const error = new Error(message.error?.message ?? `eyeBROWSE RPC failed: ${method}`);
      error.type = message.error?.type;
      throw error;
    }
    return aliasRecordKeys(message.result);
  }

  status() { return this.call('browser.status'); }
  capabilities(prefix) { return this.call('browser.capabilities', prefix ? { prefix } : {}); }
  current() { return this.call('context.current'); }
  targets() { return this.call('target.list'); }
  debugTarget(target) { return this.call('debug_target.attach', { target }); }
  cognition() { return this.call('target.cognition'); }
  open(url) { return this.call('target.open', { url }); }
  activate(target) { return this.call('target.activate', { target }); }
  closeTarget(target) { return this.call('target.close', { target }); }
  demote(target, to = 'warm') { return this.call('target.demote', { target, to }); }
  lifecycle(target) { return this.call('lifecycle.status', { target }); }
  navigate(target, url) { return this.call('navigate.go', { target, url }); }
  back(target) { return this.call('navigate.back', { target }); }
  forward(target) { return this.call('navigate.forward', { target }); }
  reload(target, ignoreCache = false) { return this.call('navigate.reload', { target, ignoreCache }); }
  observe(target) { return this.call('observe.surface', { target }); }
  delta(target, since) { return this.call('observe.delta', { target, since }); }
  query(query) { return this.call('query.find', query); }
  inspect(id) { return this.call('inspect.element', { id }); }
  identity(id) { return this.call('identity.resolve', { id }); }
  click(id) { return this.call('action.click', { id }); }
  fill(id, text) { return this.call('action.fill', { id, text }); }
  type(id, text) { return this.call('action.type', { id, text }); }
  key(target, key) { return this.call('action.key', { target, key }); }
  scroll(target, deltaY, deltaX = 0) { return this.call('action.scroll', { target, deltaX, deltaY }); }
  hover(id) { return this.call('action.hover', { id }); }
  doubleClick(id) { return this.call('action.double_click', { id }); }
  contextClick(id) { return this.call('action.context_click', { id }); }
  focus(id) { return this.call('action.focus', { id }); }
  select(id, values) { return this.call('action.select', { id, values: Array.isArray(values) ? values : [values] }); }
  check(id) { return this.call('action.check', { id }); }
  uncheck(id) { return this.call('action.uncheck', { id }); }
  upload(id, files) { return this.call('file.upload', { id, files: Array.isArray(files) ? files : [files] }); }
  js(target, expression) { return this.call('js.evaluate', { target, expression }); }
  wait(target, expression, timeoutMs = 5000, intervalMs = 100) { return this.call('wait.until', { target, expression, timeoutMs, intervalMs }); }
  waitAny(target, expressions, timeoutMs = 5000, intervalMs = 100) { return this.call('wait.any', { target, expressions, timeoutMs, intervalMs }); }
  waitAll(target, expressions, timeoutMs = 5000, intervalMs = 100) { return this.call('wait.all', { target, expressions, timeoutMs, intervalMs }); }
  waitSequence(target, expressions, timeoutMs = 5000, intervalMs = 100) { return this.call('wait.sequence', { target, expressions, timeoutMs, intervalMs }); }
  quiet(target, quietMs, timeoutMs = Math.max(5000, quietMs)) { return this.call('wait.quiet_for', { target, quietMs, timeoutMs }); }
  network(query) { return this.call('network.search', query); }
  networkBody(id) { return this.call('network.body', { id }); }
  networkDetail(id) { return this.call('network.detail', { id }); }
  networkSearchBody(id, query, options = {}) { return this.call('network.search_body', { id, query, ...options }); }
  networkMessages(target, kind, limit = 200) { return this.call('network.messages', { target, ...(kind ? { kind } : {}), limit }); }
  networkBodySave(id, destination, timeoutMs = 120000) { return this.call('network.body.save', { id, ...(destination ? { destination } : {}), timeoutMs }); }
  console(target, limit = 100) { return this.call('console.list', { target, limit }); }
  exceptions(target, limit = 100) { return this.call('exception.list', { target, limit }); }
  downloads() { return this.call('download.list'); }
  downloadWait(id, timeoutMs = 120000) { return this.call('download.wait', { id, timeoutMs }); }
  downloadSave(id, destination) { return this.call('download.save', { id, destination }); }
  downloadCancel(id) { return this.call('download.cancel', { id }); }
  artifacts() { return this.call('artifact.list'); }
  registerArtifact(type, path, target, source) { return this.call('artifact.register', { type, path, ...(target ? { target } : {}), ...(source ? { source } : {}) }); }
  screenshot(target, destination) { return this.call('screenshot.full_page', { target, ...(destination ? { destination } : {}) }); }
  screenshotElement(id, destination) { return this.call('screenshot.element', { id, ...(destination ? { destination } : {}) }); }
  screenshotRegion(target, x, y, width, height, destination) { return this.call('screenshot.region', { target, x, y, width, height, ...(destination ? { destination } : {}) }); }
  performance(target) { return this.call('performance.metrics', { target }); }
  dialog(target) { return this.call('dialog.current', { target }); }
  handleDialog(target, accept = true, promptText) { return this.call('dialog.handle', { target, accept, ...(promptText !== undefined ? { promptText } : {}) }); }
  emulateViewport(target, width, height, options = {}) { return this.call('emulate.viewport', { target, width, height, ...options }); }
  emulateCpu(target, rate) { return this.call('emulate.cpu', { target, rate }); }
  emulateGeolocation(target, latitude, longitude, accuracy = 1) { return this.call('emulate.geolocation', { target, latitude, longitude, accuracy }); }
  emulateLocale(target, locale) { return this.call('emulate.locale', { target, locale }); }
  emulateTimezone(target, timezoneId) { return this.call('emulate.timezone', { target, timezoneId }); }
  emulateMedia(target, media, features = {}) { return this.call('emulate.media', { target, media, features }); }
  emulateNetwork(target, options = {}) { return this.call('emulate.network', { target, ...options }); }
  emulateReset(target) { return this.call('emulate.reset', { target }); }
  performanceTimelineEnable(target, eventTypes) { return this.call('performance.timeline.enable', { target, eventTypes }); }
  performanceTimeline(target, type, limit = 200) { return this.call('performance.timeline.list', { target, ...(type ? { type } : {}), limit }); }
  traceStart(target, categories) { return this.call('performance.trace.start', { target, ...(categories ? { categories } : {}) }); }
  traceStop(target, timeoutMs = 60000) { return this.call('performance.trace.stop', { target, timeoutMs }); }
  memoryCurrent(target) { return this.call('memory.current', { target }); }
  heapSnapshot(target, captureNumericValue = true) { return this.call('memory.heap_snapshot', { target, captureNumericValue }); }
  memorySamplingStart(target, samplingInterval = 32768, stackDepth = 128) { return this.call('memory.sampling.start', { target, samplingInterval, stackDepth }); }
  memorySamplingStop(target) { return this.call('memory.sampling.stop', { target }); }
  extensions() { return this.call('extension.list'); }
  extensionLoadUnpacked(path, enableInIncognito = false) { return this.call('extension.load_unpacked', { path, enableInIncognito }); }
  extensionUninstall(id) { return this.call('extension.uninstall', { id }); }
  extensionTriggerAction(id, target) { return this.call('extension.trigger_action', { id, target }); }
  extensionStorage(id, storageArea = 'local', keys) { return this.call('extension.storage', { id, storageArea, ...(keys ? { keys } : {}) }); }
  runtimeDebugEnable(target) { return this.call('runtime_debug.enable', { target }); }
  runtimeScripts(target, contains, limit = 500) { return this.call('runtime_debug.scripts', { target, ...(contains ? { contains } : {}), limit }); }
  runtimeScriptSource(target, scriptId) { return this.call('runtime_debug.source', { target, scriptId }); }
  runtimeScriptSearch(target, scriptId, query, options = {}) { return this.call('runtime_debug.search', { target, scriptId, query, ...options }); }
  runtimePaused(target) { return this.call('runtime_debug.paused', { target }); }
  accessibilityInspect(id) { return this.call('accessibility.inspect', { id }); }
  accessibilityAudit(target) { return this.call('accessibility.audit', { target }); }
  screencastStart(target, options = {}) { return this.call('screencast.start', { target, ...options }); }
  screencastStop(target) { return this.call('screencast.stop', { target }); }  webmcp(target) { return this.call('webmcp.list', { target }); }
  webmcpInspect(target, name, frameId) { return this.call('webmcp.inspect', { target, name, ...(frameId ? { frameId } : {}) }); }
  webmcpExecute(target, name, input = {}, frameId, timeoutMs = 30000) { return this.call('webmcp.execute', { target, name, input, ...(frameId ? { frameId } : {}), timeoutMs }); }
  runtimeTools(target) { return this.call('runtime_tools.list', { target }); }
  runtimeToolInspect(target, name, group) { return this.call('runtime_tools.inspect', { target, name, ...(group ? { group } : {}) }); }
  runtimeToolExecute(target, name, input = {}, group) { return this.call('runtime_tools.execute', { target, name, input, ...(group ? { group } : {}) }); }
  subscribe(methods, target) { return this.call('cdp.subscribe', { methods: Array.isArray(methods) ? methods : [methods], ...(target ? { target } : {}) }); }
  next(id, timeoutMs = 5000, limit = 50) { return this.call('cdp.next', { id, timeoutMs, limit }); }
  unsubscribe(id) { return this.call('cdp.unsubscribe', { id }); }
  cdp(method, params = {}, target) { return this.call('cdp.send', { method, params, ...(target ? { target } : {}) }); }

  async jsValue(target, expression) {
    const evaluation = await this.js(target, expression);
    return evaluation?.result?.value;
  }

  close() {
    if (!this.#socket.destroyed) this.#socket.end();
  }

  #onData(chunk) {
    this.#buffer += chunk;
    while (true) {
      const newline = this.#buffer.indexOf('\n');
      if (newline < 0) break;
      const line = this.#buffer.slice(0, newline).trim();
      this.#buffer = this.#buffer.slice(newline + 1);
      if (!line) continue;
      let message;
      try { message = JSON.parse(line); }
      catch (error) { this.#failAll(error); continue; }
      const pending = this.#pending.get(message.id);
      if (!pending) continue;
      this.#pending.delete(message.id);
      pending.resolve(message);
    }
  }

  #failAll(error) {
    for (const pending of this.#pending.values()) pending.reject(error);
    this.#pending.clear();
  }
}


function aliasRecordKeys(value) {
  if (Array.isArray(value)) return value.map(aliasRecordKeys);
  if (!value || typeof value !== 'object') return value;
  const output = {};
  for (const [key, child] of Object.entries(value)) {
    const normalized = aliasRecordKeys(child);
    if (/^[A-Z]/.test(key)) {
      const alias = key[0].toLowerCase() + key.slice(1);
      if (!(alias in output)) output[alias] = normalized;
      if (!(key in output)) Object.defineProperty(output, key, { value: normalized, enumerable: false, configurable: true, writable: true });
    } else {
      output[key] = normalized;
    }
  }
  return output;
}
