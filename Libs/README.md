# Libs

This folder holds the local `KeePass.exe` used to **build** KPPasskeyChecker — for packaging the `.plgx`
and as a compile reference for the `.dll`. It is **not** bundled into the plugin output.

`KeePass.exe` is intentionally **not committed** to this repository (it is excluded in
`.gitignore`). To build, supply your own copy:

1. Download KeePass 2.x from <https://keepass.info/> — the **portable** edition is enough, no
   installation required (or copy `KeePass.exe` from an existing installation).
2. Place `KeePass.exe` here as `Libs\KeePass.exe`.

Alternatively, point the build at an existing install without copying it:

- `.\build.ps1 -KeePassExe "C:\Program Files\KeePass Password Safe 2\KeePass.exe"`, or
- in Visual Studio, set the `KeePass` reference's `HintPath` to your copy.
