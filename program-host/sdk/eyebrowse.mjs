import net from 'node:net';

const DEFAULT_PIPE = '\\\\.\\pipe\\eyebrowse-dev';

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
    socket.on('close', () => this.#failAll(new Error('eyebrowse kernel pipe closed')));
  }

  static async connect(pipe = DEFAULT_PIPE) {
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
    const response = new Promise((resolve, reject) => {
      this.#pending.set(id, { resolve, reject });
    });
    this.#socket.write(JSON.stringify({ id, method, params }) + '\n');
    const message = await response;
    if (!message.ok) {
      const error = new Error(message.error?.message ?? `eyebrowse RPC failed: ${method}`);
      error.type = message.error?.type;
      throw error;
    }
    return message.result;
  }

  status() { return this.call('browser.status'); }
  targets() { return this.call('target.list'); }
  open(url) { return this.call('target.open', { url }); }
  observe(target) { return this.call('observe.surface', { target }); }
  delta(target, since) { return this.call('observe.delta', { target, since }); }
  query(query) { return this.call('query.find', query); }
  inspect(id) { return this.call('inspect.element', { id }); }
  click(id) { return this.call('action.click', { id }); }
  fill(id, text) { return this.call('action.fill', { id, text }); }
  type(id, text) { return this.call('action.type', { id, text }); }
  key(target, key) { return this.call('action.key', { target, key }); }
  scroll(target, deltaY, deltaX = 0) { return this.call('action.scroll', { target, deltaX, deltaY }); }
  js(target, expression) { return this.call('js.evaluate', { target, expression }); }
  wait(target, expression, timeoutMs = 5000, intervalMs = 100) {
    return this.call('wait.until', { target, expression, timeoutMs, intervalMs });
  }
  cdp(method, params = {}) { return this.call('cdp.send', { method, params }); }

  async jsValue(target, expression) {
    const evaluation = await this.js(target, expression);
    return evaluation?.result?.value;
  }

  close() {
    if (this.#socket.destroyed) return;
    this.#socket.end();
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