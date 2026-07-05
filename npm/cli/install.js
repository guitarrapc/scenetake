#!/usr/bin/env node
'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const https = require('https');
const crypto = require('crypto');
const { spawnSync } = require('child_process');
const { HttpsProxyAgent } = require('https-proxy-agent');
const { getProxyForUrl } = require('proxy-from-env');

const pkg = require(path.join(__dirname, '..', 'package.json'));
const version = pkg.version;
const repo = 'guitarrapc/scenetake';

function fail(message, err) {
  if (err) {
    console.error(message, err.message || err);
  } else {
    console.error(message);
  }
  process.exit(1);
}

function resolveAsset() {
  const platform = process.platform;
  const arch = process.arch;

  const archSuffix =
    arch === 'x64' ? 'amd64' :
    arch === 'arm64' ? 'arm64' :
    null;

  if (!archSuffix) {
    fail(`scenetake: unsupported architecture: ${arch}. Use GitHub Releases or build from source.`);
  }

  let artifact;
  if (platform === 'win32') {
    artifact = `scenetake-win-${archSuffix}.zip`;
  } else if (platform === 'linux') {
    artifact = `scenetake-linux-${archSuffix}.tar.gz`;
  } else if (platform === 'darwin') {
    artifact = `scenetake-osx-${archSuffix}.tar.gz`;
  } else {
    fail(`scenetake: unsupported platform: ${platform}. Use GitHub Releases or build from source.`);
  }

  return {
    platform,
    artifact,
    isWin: platform === 'win32',
    isMac: platform === 'darwin',
    binaryName: platform === 'win32' ? 'scenetake.exe' : 'scenetake',
  };
}

function releaseUrl(fileName) {
  return `https://github.com/${repo}/releases/download/v${version}/${fileName}`;
}

function download(downloadUrl, redirects = 0) {
  return new Promise((resolve, reject) => {
    if (redirects > 5) {
      reject(new Error('too many redirects while downloading'));
      return;
    }

    const proxy = getProxyForUrl(downloadUrl);
    const agent = proxy ? new HttpsProxyAgent(proxy) : undefined;
    const url = new URL(downloadUrl);

    const request = https.get(
      {
        hostname: url.hostname,
        path: url.pathname + url.search,
        protocol: url.protocol,
        port: url.port,
        agent,
        headers: {
          'User-Agent': 'scenetake-npm-wrapper',
          Accept: '*/*',
        },
      },
      (res) => {
        if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
          res.resume();
          resolve(download(res.headers.location, redirects + 1));
          return;
        }

        if (res.statusCode !== 200) {
          res.resume();
          reject(new Error(`download failed (${res.statusCode}) from ${downloadUrl}`));
          return;
        }

        const chunks = [];
        res.on('data', (chunk) => chunks.push(chunk));
        res.on('end', () => resolve(Buffer.concat(chunks)));
        res.on('error', reject);
      }
    );

    request.setTimeout(60_000, () => {
      request.destroy(new Error('download timed out'));
    });
    request.on('error', reject);
  });
}

function sha256(buffer) {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

function parseChecksumFile(text, artifactName) {
  const lines = text.split(/\r?\n/).filter(Boolean);
  for (const line of lines) {
    const match = line.match(/^([0-9a-f]{64})\s{2}(.+)$/i);
    if (match && match[2] === artifactName) {
      return match[1].toLowerCase();
    }
  }

  const first = lines[0]?.trim().split(/\s+/)[0];
  if (first && /^[0-9a-f]{64}$/i.test(first)) {
    return first.toLowerCase();
  }

  throw new Error(`could not parse checksum file for ${artifactName}`);
}

function extractArchive(archivePath, destDir, artifact) {
  fs.mkdirSync(destDir, { recursive: true });

  if (artifact.endsWith('.zip')) {
    if (process.platform !== 'win32') {
      throw new Error(`zip extraction requires Windows: ${artifact}`);
    }
    extractZip(archivePath, destDir);
    return;
  }

  if (artifact.endsWith('.tar.gz')) {
    extractTarGz(archivePath, destDir);
    return;
  }

  throw new Error(`unsupported archive format: ${artifact}`);
}

function extractTarGz(archivePath, destDir) {
  runCommand('tar', ['-xzf', archivePath, '-C', destDir], 'archive extraction');
}

function extractZip(archivePath, destDir) {
  const script = [
    '$ErrorActionPreference = "Stop"',
    `Expand-Archive -LiteralPath ${psLiteral(archivePath)} -DestinationPath ${psLiteral(destDir)} -Force`,
  ].join('; ');

  runCommand(
    'powershell.exe',
    ['-NoProfile', '-NonInteractive', '-Command', script],
    'zip extraction'
  );
}

function psLiteral(value) {
  return `'${value.replace(/'/g, "''")}'`;
}

function runCommand(command, args, label) {
  const result = spawnSync(command, args, { stdio: 'pipe' });
  if (result.status !== 0) {
    const detail =
      result.stderr?.toString().trim() ||
      result.stdout?.toString().trim() ||
      `exit ${result.status}`;
    throw new Error(`${label} failed: ${detail}`);
  }
}

function installComplete(distDir, asset) {
  const binaryPath = path.join(distDir, asset.binaryName);
  if (!fs.existsSync(binaryPath)) {
    throw new Error(`binary not found after extraction: ${asset.binaryName}`);
  }

  if (!asset.isWin) {
    fs.chmodSync(binaryPath, 0o755);
  }

  if (asset.isMac) {
    const helperPath = path.join(distDir, 'minipty_spawn_helper');
    if (!fs.existsSync(helperPath)) {
      throw new Error('minipty_spawn_helper not found after extraction');
    }
    fs.chmodSync(helperPath, 0o755);
  }
}

async function main() {
  const asset = resolveAsset();
  const distDir = path.join(__dirname, '..', 'dist');
  const destPath = path.join(distDir, asset.binaryName);

  if (fs.existsSync(destPath)) {
    if (!asset.isMac || fs.existsSync(path.join(distDir, 'minipty_spawn_helper'))) {
      return;
    }
  }

  const archiveUrl = releaseUrl(asset.artifact);
  const checksumUrl = `${archiveUrl}.sha256`;

  const [archiveBuffer, checksumBuffer] = await Promise.all([
    download(archiveUrl),
    download(checksumUrl),
  ]);

  const expected = parseChecksumFile(checksumBuffer.toString('utf8'), asset.artifact);
  const actual = sha256(archiveBuffer);
  if (actual !== expected) {
    throw new Error('checksum mismatch for downloaded archive');
  }

  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'scenetake-npm-'));
  const archivePath = path.join(tempDir, asset.artifact);

  try {
    fs.writeFileSync(archivePath, archiveBuffer);
    fs.rmSync(distDir, { recursive: true, force: true });
    extractArchive(archivePath, distDir, asset.artifact);
    installComplete(distDir, asset);
  } finally {
    fs.rmSync(tempDir, { recursive: true, force: true });
  }
}

main().catch((err) => {
  fail('scenetake: install failed.', err);
});
