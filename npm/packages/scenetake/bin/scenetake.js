#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const { createRequire } = require('module');

const requireFromHere = createRequire(__filename);

// npm installs the Linux package whose `libc` matches the host, or both where npm ignores `libc`, so the wrapper
// picks by the host's C library. A Node built against glibc reports the glibc it runs on; a musl Node reports none.
function isMusl() {
  const report = process.report;
  if (report && typeof report.getReport === 'function') {
    const excludeNetwork = report.excludeNetwork;
    // The network section resolves interface names, which can be slow and is not needed here.
    report.excludeNetwork = true;
    try {
      return !report.getReport().header.glibcVersionRuntime;
    } catch {
      // Fall back to the dynamic linker's script below.
    } finally {
      report.excludeNetwork = excludeNetwork;
    }
  }
  try {
    return fs.readFileSync('/usr/bin/ldd', 'utf8').includes('musl');
  } catch {
    return false;
  }
}

function resolvePlatformPackage() {
  const arch =
    process.arch === 'x64' ? 'x64' :
    process.arch === 'arm64' ? 'arm64' :
    null;

  if (!arch) {
    console.error(`scenetake: unsupported architecture: ${process.arch}`);
    process.exit(1);
  }

  const libc = process.platform === 'linux' && isMusl() ? '-musl' : '';
  const name = `@scenetake/${process.platform}-${arch}${libc}`;
  try {
    return path.dirname(requireFromHere.resolve(`${name}/package.json`));
  } catch {
    console.error(`scenetake: platform package not installed: ${name}`);
    console.error('Reinstall scenetake for your platform.');
    process.exit(1);
  }
}

function resolveBinary() {
  const pkgDir = resolvePlatformPackage();
  const binName = process.platform === 'win32' ? 'scenetake.exe' : 'scenetake';
  const binPath = path.join(pkgDir, binName);

  if (!fs.existsSync(binPath)) {
    console.error(`scenetake: binary not found at ${binPath}`);
    process.exit(1);
  }

  return binPath;
}

const child = spawn(resolveBinary(), process.argv.slice(2), { stdio: 'inherit' });
child.on('error', (err) => {
  console.error('scenetake: failed to launch binary.', err.message || err);
  process.exit(1);
});
child.on('exit', (code) => {
  process.exit(code == null ? 1 : code);
});
