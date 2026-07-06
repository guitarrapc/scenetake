# npm packages

Platform-specific binaries are published as optional dependencies (esbuild-style). No install scripts are used.

```
packages/
  scenetake/          # meta package (bin wrapper)
  linux-x64/          # @scenetake/linux-x64
  linux-arm64/
  darwin-x64/
  darwin-arm64/
  win32-x64/
  win32-arm64/
scripts/
  assemble-packages.sh
  stage-all.sh
```

Release CI assembles binaries into `packages/*` and runs `npm stage publish` for each package.
