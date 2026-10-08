# RAM Modern 4.0 - Consolidated Preview 6

A modern Windows 10/11 Roblox account/game manager using C# / WPF / .NET 10. This is an independent user-facing application maintained separately from the legacy RAM code.

## Quick start

1. Place `RAM.Modern`, `RAM.Modern.SmokeTests` and `.github/workflows` at the repository root.
2. Commit to `main`; GitHub Actions runs the .NET 10 smoke harness, compiles and publishes `RAM.Modern.exe`.
3. Download the `RAM-Modern-win-x64` build artifact and start `RAM.Modern.exe`.
4. Follow **ONE_PASS_TESTING.md** for the single Windows acceptance test.

## Included

Account profiles, encrypted local storage and metadata backups, identity-checked saved-session workflow (experimental), Roblox desktop launcher, multi-page public server browser and low-player scan, real Job ID shuffle, private server links, public player lookup, favorites/recents, game discovery, outfits and universe viewer, isolated Edge profiles/password manager, FPS presets and backup, theme presets, local developer API, watcher, safe diagnostics and a Windows smoke-test workflow.

## Limits

The app does **not** bypass Roblox login security or automate CAPTCHA solving; an encrypted session snapshot is not a guarantee the official Roblox desktop client will authenticate with it. Browser sessions and desktop sessions are separate. Client compatibility needs actual Windows testing. See **FEATURE_STATUS.md**.

Excluded by request: auto updater and multi-instance launching.
