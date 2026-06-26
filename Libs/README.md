# Libs

This folder holds the local `KeePass.exe` used to **build** KP2FAChecker — for packaging the `.plgx`
and as a compile reference for the `.dll`. It is **not** bundled into the plugin output.

## Minimum versions (empirically verified, 2026-06-25)

| Axis | Minimum | Determining factor |
|---|---|---|
| **KeePass** | **2.18** | `Plugin.UpdateUrl` property introduced in 2.18 |
| **.NET Framework** | **4.6** | `HashAlgorithmName` / `RSASignaturePadding` introduced in .NET 4.6 (used in PGP verification) |

The `.plgx` prereqs are embedded in the build output (`-plgx-prereq-kp:2.18 -plgx-prereq-net:4.6`),
so KeePass rejects the plugin with a clear message on older installs instead of a cryptic compile error.

**Tripwire recommendation:** use `KeePass 2.18` as the `Libs\KeePass.exe` reference. The build then
fails immediately if any future code change references a newer KeePass API, making a floor bump an
explicit, deliberate act rather than an accidental regression. Upgrade the reference — and bump the
prereqs in `build.ps1` — only when a newer KeePass API is intentionally required.

Probe scripts and all per-version build artefacts live under `..\probe\` (gitignored).

## Supplying KeePass.exe

`KeePass.exe` is intentionally **not committed** to this repository (it is excluded in
`.gitignore`). To build, supply your own copy:

1. Download KeePass 2.x (≥ 2.18) from <https://keepass.info/> — the **portable** edition is enough,
   no installation required (or copy `KeePass.exe` from an existing installation).
2. Place `KeePass.exe` here as `Libs\KeePass.exe`.

Alternatively, point the build at an existing install without copying it:

- `.\build.ps1 -KeePassExe "C:\Program Files\KeePass Password Safe 2\KeePass.exe"`, or
- in Visual Studio, set the `KeePass` reference's `HintPath` to your copy.
