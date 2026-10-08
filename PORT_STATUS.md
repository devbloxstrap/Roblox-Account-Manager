# RAM modernization — first source patch (not a verified release)

This is a **source-only preliminary patch** based on the uploaded 3.7.2 repository. It is **not** a completed RAM 4.0, a tested Windows executable, or a Voidstrap integration.

## Implemented in this patch
- Use `TotalSeconds` instead of the seconds component for account-save throttling.
- Write serialized account data to a staging file, then atomically replace the previous database file.
- Avoid overwriting backups with an empty previous database.
- Keep existing password-encrypted/DPAPI/plaintext formats unchanged for backward compatibility.
- Hide the obsolete multi-instance checkbox. Multi-instance functionality is **out of scope**.
- Enable Windows 10 compatibility manifest flag (also used by Windows 11) and long-path-awareness.

## Existing capabilities preserved (not newly implemented)
- Account list, grouping, search, avatar features, password encryption, settings, old WinForms layout.

## NOT implemented or verified yet
- Voidstrap account-switcher code integration, Roblox session import/export, supported current authentication flows.
- New .NET / WPF architecture, WebView2 browser, modern UI, new updater and installer.
- Windows 11 build/runtime test, game joining, Roblox client/API compatibility.
- A compiled executable; this environment has no .NET SDK or Windows GUI runtime.

## Security note
- The original project uses `DataProtectionScope.LocalMachine` for its default DPAPI data. This retains compatibility but is weaker for local multi-user separation than a future user-scoped storage migration. Do not distribute account data or session cookies.

## Build
Open `RBX Alt Manager.sln` in Visual Studio on Windows with .NET Framework 4.7.2 developer pack. Restore legacy NuGet dependencies. Review warnings and run the application in a controlled test profile before use.
