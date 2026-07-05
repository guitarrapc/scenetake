#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const isWin = process.platform === 'win32';
const ext = isWin ? '.exe' : '';
const binPath = path.join(__dirname, '..', 'dist', `scenetake${ext}`);

if (!fs.existsSync(binPath)) {
  console.error('scenetake: binary not found.');
  console.error('Reinstall the package or download from GitHub Releases.');
  process.exit(1);
}

const child = spawn(binPath, process.argv.slice(2), { stdio: 'inherit' });
child.on('error', (err) => {
  console.error('scenetake: failed to launch binary.', err.message || err);
  process.exit(1);
});
child.on('exit', (code) => {
  process.exit(code == null ? 1 : code);
});
