// Where the answers come from. The ASP.NET showcase runs the library on the server behind /api/ (as
// native .NET — fast); the static build (`npm run build:pages`, for GitHub Pages) runs the very same
// API in the browser, from the C# library compiled to WebAssembly, inside a Web Worker. Both take a
// JSON request and give back a JSON response, so the pages never know which they are talking to.

export const inBrowser = import.meta.env.MODE === 'pages';

let worker;
let nextId = 0;
const pending = new Map();

/**
 * Runs `operation` (for example 'break') on `request` and returns `{ promise, cancel }`. The promise
 * resolves to the parsed response. `onProgress` receives `{ fraction, message, best }` reports from
 * the long searches (in the browser; the server is fast enough not to need them).
 */
export function call(operation, request = {}, onProgress) {
  if (!inBrowser) return callServer(operation, request);

  // The .NET runtime lives in a worker, so a long search (Playfair, the bombe) never freezes the page.
  worker ??= startWorker();
  const id = nextId++;
  const promise = new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject, onProgress });
    worker.postMessage({ id, operation, request: JSON.stringify(request) });
  });
  // A WebAssembly worker busy in a search cannot read a "stop" message, so cancelling ends the worker
  // itself; the next call starts a fresh one (a few seconds while the runtime loads again).
  const cancel = () => worker && abandon(worker, 'Cancelled.');
  return { promise, cancel };
}

/** Like `call`, but just the promise — for quick operations nobody will cancel. */
export const ask = (operation, request) => call(operation, request).promise;

function callServer(operation, request) {
  const controller = new AbortController();
  const promise = (async () => {
    const response = await fetch(`/api/${operation}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
      signal: controller.signal,
    });
    if (!response.ok) throw new Error(await response.text());
    return response.json();
  })();
  return { promise, cancel: () => controller.abort() };
}

function startWorker() {
  const started = new Worker(new URL('./engine-worker.js', import.meta.url), { type: 'module' });
  started.onmessage = ({ data: { id, response, progress, error, fatal } }) => {
    if (fatal) return abandon(started, error);
    const call = pending.get(id);
    if (!call) return;
    if (progress) return call.onProgress?.(JSON.parse(progress));
    pending.delete(id);
    if (error) call.reject(new Error(error));
    else call.resolve(JSON.parse(response));
  };
  started.onerror = (event) => abandon(started, event.message || 'The code-breaking engine failed to start.');
  // The runtime sits beside index.html, wherever the site is hosted (a GitHub Pages project site
  // lives under /<repository>/, not at the root).
  started.postMessage({ frameworkUrl: new URL('_framework/dotnet.js', document.baseURI).href });
  return started;
}

// A worker that was cancelled, whose runtime failed to load, or that died cannot be revived in place.
// Fail everything waiting on it and let the next call start afresh.
function abandon(failed, error) {
  failed.terminate();
  if (worker === failed) worker = undefined;
  for (const call of pending.values()) call.reject(new Error(error));
  pending.clear();
}
