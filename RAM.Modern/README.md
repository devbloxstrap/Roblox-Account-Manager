# RAM Modern 4.0 (preview)

This is a **new modern Windows frontend**, included alongside the legacy Roblox Account Manager 3.7.2 source. It does **not** migrate the old application one-for-one.

## Implemented
- .NET 10 WPF Windows 10/11 desktop UI (dark appearance)
- Locally saved non-secret account profiles, groups, favorites, notes and search
- Duplicate checking, atomic-ish save with backup; open profile data folder
- Official Roblox browser login / profile / game links
- Windows GitHub Actions workflow for x64 self-contained portable executable
- Windows DPAPI-encrypted local Roblox login-file snapshots, save and restore for selected account (experimental)
- Zero multi-instance functionality

## Not implemented / known limitations
- Session snapshot/restore is implemented but **not yet validated on a Windows client**. It operates on the local `RobloxCookies.dat` file, and Roblox may reject it or change file format. It cannot renew expired sessions.
- No automatic account identity validation before snapshot: user must verify the account manually.
- Browser sessions are separate from Roblox Windows client sessions.
- No live Roblox API authentication validation; no automated avatar fetching; no legacy sensitive-cookie import.
- Windows CI build has **not been run** in this environment. Verify in GitHub Actions.
- No installer or automatic updater. Output is a self-contained portable executable.
- Legacy source stays alongside as reference; do not build legacy solution expecting modern dependencies.

## GitHub setup
Push full extracted repository to GitHub. Workflow at `.github/workflows/ram-modern-windows.yml` automatically builds on push, or run it using Actions > Build RAM Modern > Run workflow. Download the `RAM-Modern-win-x64` artifact. Extract before running. Windows 10/11 x64 only.

## Build locally
`dotnet publish RAM.Modern/RAM.Modern.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o output`

## Security
Profile metadata is saved in `%LOCALAPPDATA%\\RAM.Modern\\profiles.json`. Session snapshots are encrypted using Windows DPAPI (CurrentUser) under `%LOCALAPPDATA%\RAM.Modern\sessions`. No passwords are requested. Never share session backups, live RobloxCookies.dat, or profile notes. Read `SESSION_TESTING.md` for security notes and verification steps.

## Credits and licenses
Legacy RAM: GPL-3.0 (retain original LICENSE). This project includes original source as provided. Voidstrap account-switching design was reviewed as a reference only; no Voidstrap source files are copied into `RAM.Modern`. Keep GPL notice with redistributed combined repository; review third-party obligations before distributing binaries.
