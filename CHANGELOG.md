# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

`release.ps1` uses the section for the version being released as the GitHub release notes,
so keep each `## [x.y.z]` heading and its body accurate before running a release.

## [0.3.0] - 2026-06-26

### Added
- Plugin icon (16×16, KeePass key with amber badge) shown in the Tools menu, entry
  context menu, and detail dialog title bar.
- Self-check harness (`tools/SelfCheck`) for offline regression testing of core logic
  (method-token parsing, scope mapping, domain matching, PGP verification path) without
  a running KeePass process.
- Self-check runs automatically at the start of the release Prepare stage; the release
  aborts immediately if any check fails.

### Fixed
- GDI handle leak: the native HICON created by `Bitmap.GetHicon()` is now released via
  `DestroyIcon` in `Terminate` (`Icon.Dispose` does not free it).
- PGP packet parser: added bounds checks before multi-byte reads in the signature and
  unhashed-subpacket paths to prevent out-of-bounds reads on malformed input.

## [0.2.0] - 2026-06-24
### Added
- Per-entry detail window: double-click the **2FA Methods** column cell to open a
  KeePass-native dialog showing the entry's 2FA methods (incl. custom software/hardware
  product names), documentation and recovery links, and notes, with the required attribution.

### Changed
- The manual **Refresh now** action now reports failures clearly, distinguishing the
  cause (network / data format / signature verification) while continuing to show the
  last cached data.

### Fixed
- The Public Suffix List cache no longer fails to update after its 7-day TTL (atomic
  replace instead of an unconditional `File.Move`).

### Security
- JSON size cap aligned to the 16 MB transport cap; parse failures are now surfaced
  instead of being silently dropped.

## [0.1.0] - 2026-06-23
### Added
- Initial public release: a **2FA Methods** entry-list column backed by the 2FA Directory
  (2factorauth, API v4), with local caching, PGP signature verification of the data, and
  KeePass update-check wiring.