#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const { createRequire } = require('module');

const requireFromHere = createRequire(__filename);

function resolvePlatformPackage() {
  const arch =
    process.arch === 'x64' ? 'x64' :
    process.arch === 'arm64' ? 'arm64' :
    null;

  if (!arch) {
    console.error(`scenetake: unsupported architecture: ${process.arch}`);
    process.exit(1);
  }

  const name = `@scenetake/${process.platform}-${arch}`;
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
