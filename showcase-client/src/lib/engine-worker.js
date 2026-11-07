// Hosts the WebAssembly build of the showcase API (RP.CodeBreaker.Showcase.Browser) off the main
// thread. The first message says where the runtime is; every later one is a request. Requests run one
// at a time — the runtime here has a single thread — and long ones post progress as they go.

let frameworkUrl;
let exports;
let current;

// Called from C# (a [JSImport]) whenever a search reports progress. The worker is busy running that
// search, but postMessage only queues the report, so it reaches the page straight away.
globalThis.codeBreakerProgress = (json) => self.postMessage({ id: current, progress: json });

self.onmessage = async ({ data }) => {
  if (data.frameworkUrl) {
    frameworkUrl = data.frameworkUrl;
    return;
  }
  const { id, operation, request } = data;
  let Call;
  try {
    ({ Call } = await (exports ??= load()));
  } catch (error) {
    // Fatal: the page discards this worker and starts a new one on the next call.
    self.postMessage({ fatal: true, error: `The WebAssembly runtime failed to load: ${error?.message ?? error}` });
    return;
  }
  try {
    current = id;
    self.postMessage({ id, response: Call(operation, request) });
  } catch (error) {
    self.postMessage({ id, error: String(error?.message ?? error) });
  }
};

async function load() {
  // Without this flag dotnet.js takes any worker for one of its own runtime threads and waits
  // forever for a main thread that never comes; with it, the worker hosts a whole runtime.
  globalThis.dotnetSidecar = true;
  const { dotnet } = await import(/* @vite-ignore */ frameworkUrl);
  const runtime = await dotnet.create();
  const assembly = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  return assembly.ShowcaseExports;
}
