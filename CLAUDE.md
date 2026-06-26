# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**KP2FAChecker** is a KeePass 2.x plugin that checks which two-factor-authentication (2FA) methods the domain of a saved entry supports, using the [2FA Directory API](https://api.2fa.directory/) maintained by 2factorauth. Shared infrastructure is compiled into the `.plgx` at build time — there is no shared binary dependency at runtime.

It reuses the entire `src\Shared` infrastructure **verbatim** from its sibling plugin **KPPasskeyChecker** (same source files, same `KPPasskeyChecker.Shared.*` namespaces). The two plugins are intentionally separate (one repo per plugin); the shared code is the canonical-source-and-CI-mirror model.

> **Not to be confused** with the separate third-party plugin **KP2faChecker** (by tiuub), whose entry-list column is titled *"2FA Support"*. A user may run both. To avoid two identical headers, this plugin's column is titled **"2FA Methods"**.

## Build

Canonical build:

```
.\build.ps1
```

The single source of truth lives under **`src\`** (`src\Shared` + `src\KP2FAChecker`). `build.ps1` produces **both** shipping artifacts into `build\`:

- **`KP2FAChecker.plgx`** — KeePass packages the sources (`KeePass.exe --plgx-create`) and compiles them on the user's machine at load time (C# 5). KeePass needs one flat folder containing a `.csproj` + all `.cs`, so `build.ps1` **generates** that flat folder in `%TEMP%` from `src\` on demand, packages it, then deletes it. There is **no committed copy** of the sources.
- **`KP2FAChecker.dll`** — the same sources compiled with the in-box `csc.exe` (C# 5, `/optimize+`) into a single self-contained assembly (`Shared` compiled in; no third-party deps). The build asserts its `ProductName` is `KeePass Plugin`, otherwise KeePass silently ignores the DLL.

Both come from the same `src\` sources, so they are functionally identical — ship both, install only one.

**Visual Studio / `dotnet build`** also build the plugin: `KP2FAChecker.sln` → `src\KP2FAChecker\KP2FAChecker.csproj` (SDK-style, **`LangVersion 5`**; `Shared` linked in as source → single self-contained DLL). A **Release** build runs a post-build step that also emits the `.plgx` (`build.ps1 -PlgxOnly`) and copies the DLL into `build\`; Debug builds skip that for fast iteration.

**Prerequisites:** `KeePass.exe` at `Libs\KeePass.exe` (not in the repo / not on NuGet; used for packaging and as a compile reference, never bundled).

**Minimum versions (empirically verified 2026-06-25 via compile-bisection in `..\probe\`):**

| Axis | Minimum | Determining factor |
|---|---|---|
| KeePass | **2.18** | `Plugin.UpdateUrl` introduced in 2.18 |
| .NET Framework | **4.6** | `HashAlgorithmName` / `RSASignaturePadding` introduced in .NET 4.6 (`Shared/Pgp/`) |

Both prereqs are embedded in the `.plgx` (`-plgx-prereq-kp:2.18 -plgx-prereq-net:4.6` in `build.ps1`).
Use **KeePass 2.18** as `Libs\KeePass.exe` to act as a compile-time tripwire: the build fails immediately
if new code accidentally references a newer KeePass API. Bump the reference *and* the prereqs in
`build.ps1` only when a newer API is intentionally required.

Target framework: `.NET 4.8` (`net48`). UI is WinForms (not WPF). **Keep all source C# 5-compatible** — the `.plgx` is recompiled as C# 5 on the user's machine, so modern C# / NRT syntax would break it.

## Release

Releases are scripted by **`release.ps1`** (repo root): a three-stage **branch + PR** flow.
**`CHANGELOG.md`** (repo root, [Keep a Changelog](https://keepachangelog.com/) format) is the
single source of the GitHub release notes; the release type is explicit.

```
release.ps1 -Version <x.y.z> -Type <draft|prerelease|release> [-Stage Preview|Prepare|Publish] [-Force]
```

- **Preview** (default) — lists the version bump, the files that change, the working-tree changes
  that would be committed, the CHANGELOG notes and the plan. Changes nothing (the approval gate).
- **Prepare** — bumps the three version locations (`VersionInfo.txt`, `Properties\AssemblyInfo.cs`,
  `PluginVersion.cs`), builds, creates branch `release/vX.Y.Z`, commits, pushes and opens a PR.
  No GitHub release yet.
- **Publish** — run **after** the PR is merged to `main`: builds from `main` and creates the GitHub
  release `vX.Y.Z` (tag on `main`) with the `.plgx`/`.dll` assets and the CHANGELOG section as notes.

`-Type` maps to `--draft` / `--prerelease` / (none = a normal "Latest" release). GitHub shows a
SHA-256 digest per asset itself, so no `SHA256SUMS` file is shipped. `Prepare` generates the
`## [x.y.z]` section in `CHANGELOG.md` automatically from branch commits — review/edit it before
confirming. The umbrella **`..\release-all.ps1`** releases both plugins in lockstep at one version;
run a single repo's `release.ps1` to release one plugin.

### Development workflow per version

Feature work for a release follows a lightweight branch model — no per-feature branches:

1. **Create the release branch** — run `release.ps1 -Stage Preview` first (dry-run), then
   `release.ps1 -Stage Prepare` to bump the version, create `release/vX.Y.Z`, commit the bump,
   push, and open the PR against `main`.
2. **One commit per completed feature** — after each feature is done, Lars commits it directly on
   `release/vX.Y.Z` with a clear, scoped message (e.g. `add 2FA scope self-test`).
   The developer agent signals when a feature is ready by outputting a suggested commit message.
3. **PR accumulates feature commits** — the single open PR `release/vX.Y.Z → main` is the
   review surface for the whole version. No force-push; no squash during development.
4. **Publish on merge** — once Lars merges the PR to `main`, run `release.ps1 -Stage Publish`
   to tag `main` and create the GitHub release.

## Architecture

### Solution layout

```
src/
├── Shared/                        # Verbatim copy from KPPasskeyChecker; KPPasskeyChecker.Shared.* namespaces
│   ├── Caching/                   # ILocalCache, CacheEntry, FileSystemJsonCache
│   ├── Http/                      # ConditionalHttpFetcher, FetchResult, FetchOutcome, UserAgent
│   ├── DomainMatching/            # DomainCandidateGenerator (walks up host to eTLD+1 via PSL)
│   ├── KeePassUi/                 # PluginSettingsStoreBase wrapping IPluginHost.CustomConfig
│   └── Pgp/                       # Generic OpenPGP signature verification (BCL-only)
└── KP2FAChecker/
    ├── KP2FACheckerExt.cs         # Plugin entry point; column provider + Tools menu + UpdateUrl
    ├── Properties/AssemblyInfo.cs # AssemblyProduct "KeePass Plugin" (required) + version
    ├── Data/                      # Domain model, API client, TfaDirectoryService, TfaTrustAnchor
    ├── Settings/                  # TfaSettingsStore, TfaSettingsForm
    └── UI/                        # TfaColumnProvider ("2FA Methods")
```

The `src\Shared` tree is **reused verbatim** from KPPasskeyChecker (the canonical source); do not fork its logic. It keeps the `KPPasskeyChecker.Shared.*` namespaces — those are just labels and carry no dependency on the rest of the KPPasskeyChecker plugin, so the verbatim copy compiles cleanly here and stays trivial to keep in sync (CI mirror).

### Key data-flow concepts

- **TfaDirectoryService** owns the fetch-and-cache lifecycle. UI reads `TfaDirectoryService.Current` for cache status (last refreshed, staleness).
- **TfaApiClient** fetches one of `all.json`, `totp.json`, `u2f.json`, `sms.json`, or `email.json` from `https://api.2fa.directory/v4/` depending on the configured `TfaDataScope`. Only the selected endpoint is ever fetched.
- **ConditionalHttpFetcher** issues `If-None-Match` conditional GETs using stored ETags. On failure it returns the last known-good cached payload so the plugin never fails hard. Response size is capped (16 MB) to bound memory against a hostile endpoint — do not remove.
- **FileSystemJsonCache** stores content + metadata as two files per key under `%LocalAppData%\KeePassPluginCache\KP2FAChecker\`. Writes are atomic (`.tmp` then `File.Replace`).
- **DomainCandidateGenerator** walks from the full host down to the registrable domain (eTLD+1 per the Public Suffix List, including private PSL entries). The directory is checked at each candidate, most-specific first. Do not reduce straight to eTLD+1, and do not match only the raw host.
- **Settings** are persisted via `IPluginHost.CustomConfig` (`GetString`/`SetString`/`GetLong`/`SetLong`/`GetBool`/`SetBool`).

### API schema notes (2FA Directory v4)

This plugin consumes **API version 4** — a per-domain object map (the cleanest analog to the passkeys v1 map KPPasskeyChecker consumes). Base URL `https://api.2fa.directory/`, each endpoint has a sibling `.json.sig`.

- Endpoints: `v4/all.json`, `v4/sms.json`, `v4/email.json`, `v4/totp.json`, `v4/u2f.json`, `v4/custom-hardware.json`, `v4/custom-software.json`. In `all.json`, **disabled sites appear as empty objects** (`{"domain.com": {}}`). The `custom-hardware.json` / `custom-software.json` endpoints exist but are **intentionally not offered as scopes** — the Software/Hardware methods (and their product names) already appear in every other scope via each entry's full method set, so a dedicated scope would add no new information. The selectable scopes are `all`/`totp`/`u2f`/`sms`/`email`.
- Per-domain entry schema (key = domain): `methods` (array of `sms`/`call`/`email`/`totp`/`u2f`/`custom-software`/`custom-hardware`), `custom-software`/`custom-hardware` (product-name arrays, only when those tokens are present), `documentation`/`recovery` (URLs), `notes` (free text). v4 does **not** include service names, icons, `contact`, `regions`, or `additional-domains` (those are v3-only) — so there is no alias expansion; each alias is its own top-level key.
- An entry whose `methods` is absent/empty (or `{}`) means "no documented 2FA" → it is **skipped** at index time and renders a blank cell. Unknown method tokens are dropped (forward-compatible), never thrown.
- **Method → column label:** `totp`→TOTP, `u2f`→Security Key, `sms`→SMS, `email`→Email, `call`→Phone Call, `custom-software`→Software (+ product names), `custom-hardware`→Hardware (+ product names). The cell is a comma-joined summary, e.g. `TOTP, Security Key, SMS`.
- **Attribution required wherever data is shown** (the data is MIT-licensed): *"Data sourced from 2FA Directory by 2factorauth."* The plugin **code** is GPLv3.
- User-Agent format: `{PluginName}/{Version} (+{GitHubRepoURL})`.

### Signature verification (PGP)

Implemented in `Shared/Pgp/` (generic OpenPGP, reused unchanged) + `Data/TfaTrustAnchor.cs` (the pinned 2fa key). Enabled by the `VerifyPgpSignature` setting (**default on**, functional).

- The mechanism is **identical to KPPasskeyChecker** and uses the **same 2factorauth signing key**. Each endpoint has a sibling `<file>.json.sig` served as `application/pgp-signature`. It is **not** a detached signature — it is a complete *inline* OpenPGP signed message: an old-format **Compressed Data packet (tag 8, ZIP/DEFLATE)** wrapping a one-pass-signature packet + a literal-data packet (embedding the JSON) + a v4 signature packet. Verification decompresses, verifies the signature, and uses the **embedded** literal JSON.
- Algorithm is **RSA-4096 + SHA-512**, sig type `0x00` (binary). All native to .NET 4.8 (`RSA.VerifyHash` + `RSASignaturePadding.Pkcs1`, `DeflateStream`). **No BouncyCastle / external lib.**
- The DEFLATE inflate is **bounded to 16 MB** (decompression-bomb guard). Verification is fully fail-closed.
- The signing key is published as a **DNS CERT record (type 37) on `security.2fa.directory`**. We **pin** it at build time in `TfaTrustAnchor` (full CERT RDATA hex, identical to `PasskeyTrustAnchor`) and assert its v4 fingerprint equals `0D504141CE290061BD4F95A4AD8483C1CBABC36D` (key id `AD8483C1CBABC36D`, UID *2FactorAuth (Code signing key) <security@2fa.directory>*). Verification only ever uses the pinned key. Key rotation by 2factorauth requires a plugin update (intended fail-closed behaviour).
- When verification is on, the fetch path switches to the `.sig` URL and caches the **extracted verified JSON** under a separate cache key (`..._signed`), so toggling the setting can never serve unverified cached JSON as verified. On verification failure the plugin falls back only to previously *verified* cached data — never to unverified `.json`.

### Update check

KeePass's built-in plugin update check is wired up by overriding `Plugin.UpdateUrl` in `KP2FACheckerExt` (returns `PluginVersion.UpdateUrl`).

- URL: `https://raw.githubusercontent.com/gusowski1/KP2FAChecker/main/VersionInfo.txt` — the `VersionInfo.txt` at the **repo root** on the `main` branch.
- File format (UTF-8, **no BOM**): a separator char on its own line, then `Title:Version` lines, then the separator again. `Title` must equal the **AssemblyTitle** (`KP2FAChecker`); `Version` is the **AssemblyFileVersion**:
  ```
  :
  KP2FAChecker:0.1.0
  :
  ```
- **On every release**, bump three things together: `VersionInfo.txt`, `AssemblyVersion`/`AssemblyFileVersion` (in `Properties/AssemblyInfo.cs`), and `PluginVersion.Current`. The file lists the *latest available* version.

### Settings (TfaDataScope)

| Key | Type | Default |
|-----|------|---------|
| `Scope` | `TfaDataScope` enum (`AnySupport` / `TotpOnly` / `U2fOnly` / `SmsOnly` / `EmailOnly`) | `AnySupport` |
| `RefreshInterval` | hours | 24 |
| `VerifyPgpSignature` | bool | **true** |

`AnySupport` → `all.json` (disabled/empty entries skipped); each other scope maps to the matching method file. The settings dialog is opened from the Tools menu as a standalone dialog.

## Conventions

- All code, identifiers, and comments must be in **English**.
- **User-facing strings are single-language** (English until localization; when localized, uniformly one language). The one exception is **OS-/framework-provided error messages** (e.g. a .NET `Exception.Message`): these are shown **verbatim and never translated**, so they appear in the user's OS language by design (the user is assumed to read their own OS language). A localized framework message shown next to our English text is therefore **intentional, not a defect** — do not "fix" it.
- This plugin is intentionally separate from KPPasskeyChecker — do **not** merge passkey and 2FA logic into one plugin.
- The plugin **code** is licensed under **GPLv3** (`LICENSE`); any new dependency must be GPL-compatible (this is partly why the plugin sticks to the .NET BCL). The 2FA Directory **data** is MIT-licensed (attribution required).
- The entry-list column is titled **"2FA Methods"** — never "2FA Support" (that is the separate third-party KP2faChecker's column).
- The `src\Shared` tree is reused **verbatim** from KPPasskeyChecker (its canonical source); do not edit it here.
- Everything under `Libs\` except its `README.md` is **gitignored** — `KeePass.exe` must be supplied there locally for builds; see `Libs\README.md`.
