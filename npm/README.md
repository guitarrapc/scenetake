# npm packages

Platform-specific binaries are published as optional dependencies (esbuild-style). No install scripts are used.

```
packages/
  scenetake/          # meta package (bin wrapper)
  linux-x64/          # @scenetake/linux-x64 (glibc)
  linux-arm64/
  linux-x64-musl/     # @scenetake/linux-x64-musl (musl)
  linux-arm64-musl/
  darwin-x64/
  darwin-arm64/
  win32-x64/
  win32-arm64/
scripts/
  assemble-packages.sh
  stage-all.sh
  test-wrapper.sh     # checks the wrapper picks the glibc or musl package
```

Release CI assembles binaries into `packages/*` and runs `npm stage publish` for each package.

The Linux packages declare `libc`, so npm installs the one for the host's C library. An npm that ignores `libc` installs both, and the wrapper starts the one that matches the host.
