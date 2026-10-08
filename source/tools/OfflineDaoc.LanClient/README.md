# Offline DAoC LAN client add-on

Independent launcher and client-only setup for Windows 10/11 with .NET Framework 4.8.
No upstream launcher/server source changes, SDK, SQLite or .NET 10 runtime required on client PCs.
Travel and server timeout behavior are unchanged. LAN only; Hamachi and internet hosting are out of scope.

## Play

1. Keep `Setup-LAN.exe` beside the complete `payload` folder and run setup on the second PC.
2. Install to a new folder. Setup verifies SHA-256 hashes and creates a desktop shortcut.
3. Start the host's existing server after configuring LAN access below.
4. Open **Offline DAoC LAN**, enter the host's private IPv4 address, and choose **Enter Realm**.
5. Create your character. Your account is registered automatically by the server.

The account is unique per Windows user and shared across this add-on's installations.
It lives in `%LOCALAPPDATA%\OfflineDAoC-LAN\Identity\account.txt` with the saved address in `server.txt`.
Keep a private backup of that folder. Restore it before playing on a replacement PC to retain access
to existing characters. Do not share credentials or delete them to fix a connection problem.
Different Windows users get different accounts. Multiple players using one Windows profile are not supported yet.
The launcher checks TCP reachability only; server loading, password errors and edition mismatches
are still reported by the game. Client preferences use `OfflineDAoCLAN` in paths.dat.

## Host setup (manual; stop server before editing)

Back up `runtime/server/config/serverconfig.xml`. Set `IP` and `UdpIP` to `0.0.0.0`
to accept LAN and loopback connections. Leave ports at TCP 10300 and UDP 10400 and
keep `EnableUPnP` and `DetectRegionIP` false. This add-on does not edit region settings.
Allow inbound TCP 10300 and UDP 10400 in Windows Firewall on the Private profile,
scoped to your local subnet. The trusted home network must also be classified as Private; otherwise these rules do not apply. Explicit Public-profile CoreServer block rules can explain a connection timeout on a Public network. No router port forwarding is needed for LAN play.
Use `ipconfig` on the host to find its LAN IPv4 address; a DHCP reservation avoids address changes.
The owner's existing launcher and `offline` account continue to work unchanged.

Both XML `AutoAccountCreation` and database `allow_auto_account_creation` must be true.
Other login policies can restrict registration: staff/tester-only, Discord linking,
same-IP account creation limits and dual-login restrictions. Defaults support multiple players,
but this add-on does not change a played database or grant administrator privileges.
Do not share the host's SQLite database over the network; all saves stay on the host.

## Upgrade / remove

Install each new matching client edition into a new folder. Setup updates its own desktop shortcut;
old files remain available for rollback. Credentials remain outside both folders.
Use the exact client release and edition matching the server; the launcher cannot negotiate compatibility.
Close the game before changing versions. Delete an old client folder only after testing the new one.
To remove this portable-style installation, remove its folder and shortcut. Retain the Identity folder
unless you intentionally want to discard access to your account. No registry installation is performed.

## Build and test

For an original release download, use the complete joined ZIP and its accompanying
`download-manifest.json`. No internal build report is needed:

```powershell
.\package-download.ps1 -DownloadDirectory 'C:\Games\OfflineDAoC\.downloads\v0.35b' -Output 'C:\Games\OfflineDAoC\playable-v0.35b\LAN-Client-0.35b'
```

The output directory must not already exist. This verifies the ZIP's size and SHA-256,
extracts only client assets and license/manifest files, then verifies those files against
the upstream release's `PACKAGE MANIFEST.sha256`. Server files and saved databases are never
extracted. Temporary extraction files are removed afterward; the original downloads stay unchanged.
Copy the complete output folder to the new PC and run `Setup-LAN.exe` there.

Run in PowerShell from this directory:

```powershell
.\build.ps1
.\test.ps1
.\test-package.ps1
.\package.ps1 -CleanRelease 'D:\releases\clean-035b' -ReleaseLabel '0.35b' -Output 'D:\releases\lan-035b'
```

Build uses the Windows Framework compiler and emits standalone `artifacts/OfflineDAoC-LAN.exe`
and `artifacts/Setup-LAN.exe`. Setup alone has no game files: it needs the generated payload beside it.
For the build-report workflow, package input must come from upstream `build_release_035.py`, followed by assembly of the release
notices. Its sibling `clean-035b-build-report.json` is required (or supply `-BuildReport`).
Every copied client asset must match the clean build report. `paths.dat` is replaced with a clean
LAN preference profile. The package contains no server, database, host credentials or bot state.
Keep Setup and payload together when transferring or archiving; this is a folder-based installer,
not a single-file self-extracting archive. Installer hashes detect damage, not publisher authenticity.

For future upstream releases, adapt only the input layout/build-report adapter in package.ps1.
For a different edition, build from a clean release/report for that edition, not a manual game.dll swap.
The add-on directory can be copied forward independently; do not modify upstream game binaries.

## Validation still required on two PCs

- Host and remote player log in simultaneously with distinct accounts.
- Remote character creation, logout/reconnect, and persistence after graceful server restart.
- Movement, combat, grouping, zoning, and inventory/loot ownership.
- Idle and /grind sessions, which retain upstream remote-client timeout behavior.
- `/travel` remains host-only where upstream uses local keyboard control.
- Upgrade client folder while retaining identity; test rollback to the matching previous server/client release.

Automated tests use fake package files and never start a server or game. A successful test run
does not establish real LAN play, UDP behavior or visual UI correctness.

## Repository and standalone use

The repository tracks this source under `source/tools/OfflineDaoc.LanClient`.
You can copy the entire directory to `C:\Games\OfflineDAoC\OfflineDaoc.LanClient` and run it independently of any release. Scripts resolve supporting files relative to their own location. Keep generated client packages with their matching playable release, outside the source checkout. The ignored `artifacts/` directory contains local build outputs, not files to commit.
