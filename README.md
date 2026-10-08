# RAM 4.0 — Preview 7 (Independent account login + ticket-based game launches)

This folder contains the modern Windows WPF client for Roblox Account Manager. It retains the previous advanced UI and tools, and replaces legacy RobloxCookies.dat switching with an **independent per-account official-browser login** and fresh Roblox authentication-ticket handoff.

## How to build

GitHub Actions: `.github/workflows/ram-modern-windows.yml`; or with the .NET 10 Windows SDK:

```powershell
dotnet restore RAM.Modern.SmokeTests/RAM.Modern.SmokeTests.csproj
dotnet run --project RAM.Modern.SmokeTests/RAM.Modern.SmokeTests.csproj -c Release
dotnet publish RAM.Modern/RAM.Modern.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Correct workflow
1. **Add account via browser**, sign in on official Roblox.com in a new Edge/Chrome window and click **Capture my signed-in account**. RAM never asks for a password.
2. Repeat for additional accounts without logging out previous accounts.
3. **Verify saved account login** on each profile.
4. Enter a valid Place ID, close any running Roblox Player, select a profile and press **Launch selected account game**. A fresh Roblox authentication ticket is requested *for that profile*.
5. The returned URI is merely an OS handoff; inspect the actual Roblox window to verify game login.

See [ONE_PASS_TESTING.md](ONE_PASS_TESTING.md) and [FEATURE_STATUS.md](FEATURE_STATUS.md) for precise limitations and the unified testing checklist.

**Security:** Treat session secrets as passwords. Windows DPAPI ties them to the current Windows login; they are never included in metadata export. Closing temporary browser profiles is best-effort. Do not share debugging logs containing authentication links.

Auto-updater and multi-instance launching remain intentionally excluded.
