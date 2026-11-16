// Runs the .NET runtime off the page's main thread, so typing never waits for a render. Messages are handled one
// at a time in arrival order; each reply carries the request's id.
import { dotnet } from './_framework/dotnet.js';

let interop = null;

const ready = (async () => {
  const runtime = await dotnet.withApplicationArguments('playground').create();
  const config = runtime.getConfig();
  const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
  interop = exports.Scenetake.Playground.PlaygroundInterop;
  await runtime.runMain();
  return interop.GetProductVersion();
})();

let queue = Promise.resolve();

self.onmessage = (event) => {
  queue = queue.then(() => handle(event.data));
};

async function handle(message) {
  const { id, type } = message;
  try {
    const version = await ready;
    switch (type) {
      case 'init':
        self.postMessage({ id, type: 'ready', version });
        return;
      case 'loadOutputs':
        self.postMessage({ id, type: 'outputs', meta: JSON.parse(interop.LoadOutputs(message.json)) });
        return;
      case 'renderScenario':
        reply(id, () => interop.RenderScenario(message.yaml, JSON.stringify(message.options ?? {})));
        return;
      case 'renderCast':
        reply(id, () => interop.RenderCast(message.cast, JSON.stringify(message.options ?? {})));
        return;
      default:
        throw new Error(`unknown message: ${type}`);
    }
  } catch (error) {
    const text = error?.message ?? String(error);
    self.postMessage({ id, type: 'failed', error: text, dead: /runtime (already )?exited/i.test(text) });
  }
}

function reply(id, render) {
  const started = performance.now();
  const meta = JSON.parse(render());
  const cast = interop.TakeCast();
  const svg = interop.TakeSvg();
  const ms = performance.now() - started;
  self.postMessage({ id, type: 'result', meta, cast, svg, ms }, [cast.buffer, svg.buffer]);
}
