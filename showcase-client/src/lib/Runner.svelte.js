// A long-running call with progress and a cancel button: what the Break, Enigma and Modern pages
// share. Create one per page with `new Runner()`, then `await runner.run('break', request)`.

import { call } from './api.js';

export class Runner {
  busy = $state(false);
  progress = $state(null); // { fraction, message, best }
  error = $state(null);
  elapsed = $state(0);
  #cancel = null;
  #timer = null;

  async run(operation, request) {
    this.busy = true;
    this.progress = null;
    this.error = null;
    this.elapsed = 0;
    const started = performance.now();
    this.#timer = setInterval(() => (this.elapsed = performance.now() - started), 200);
    const { promise, cancel } = call(operation, request, (p) => (this.progress = p));
    this.#cancel = cancel;
    try {
      return await promise;
    } catch (e) {
      this.error = e.name === 'AbortError' ? 'Cancelled.' : e.message;
      return null;
    } finally {
      clearInterval(this.#timer);
      this.elapsed = performance.now() - started;
      this.busy = false;
      this.#cancel = null;
    }
  }

  cancel() {
    this.#cancel?.();
  }
}
