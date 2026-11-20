// Publishes the playground into artifacts/playground and serves it, for a local look. .claude/launch.json runs it for
// Claude's preview pane; it works the same from a terminal.
// Usage: node playground/preview.mjs [port]   (default 5180, then open http://127.0.0.1:<port>/)
import { spawnSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const out = join(root, 'artifacts', 'playground');
const wwwroot = join(out, 'wwwroot');
const port = process.argv[2] ?? '5180';

// Publishing again into the same folder fails on a stale dotnet.<hash>.js (MSB3094), and a stale index.html can keep
// its build placeholders, so both go first.
rmSync(join(wwwroot, '_framework'), { recursive: true, force: true });
for (const name of ['index.html', 'index.html.br', 'index.html.gz'])
  rmSync(join(wwwroot, name), { force: true });

const publish = spawnSync(
  'dotnet',
  ['publish', 'playground/Scenetake.Playground.csproj', '-c', 'Release', '-p:PlaygroundSoftFingerprint=true', '-o', out],
  { cwd: root, stdio: 'inherit' },
);
if (publish.error) {
  console.error(`cannot run dotnet: ${publish.error.message}`);
  process.exit(1);
}
if (publish.status !== 0)
  process.exit(publish.status ?? 1);

// serve.mjs reads its folder and port from the command line.
process.argv = [process.argv[0], join(root, 'playground', 'serve.mjs'), wwwroot, port];
await import('./serve.mjs');
