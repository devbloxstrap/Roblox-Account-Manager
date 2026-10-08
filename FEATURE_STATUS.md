# RAM Modern 4.0 - Consolidated Preview 6 feature audit

This is the **consolidated source preview** intended for a single Windows testing session after GitHub Actions passes. It is **not a verified equivalent of original RAM**. Source presence != successful client behavior.

## Features implemented in this package (real code and UI)

- Account profiles: search, create, update, delete, account avatar lookup, numeric account and group ordering, favorites, notes, groups, preferred Place ID/Job ID.
- DPAPI-encrypted local metadata, encrypted session snapshot save/restore, identity comparison against Roblox authenticated user endpoint (when desktop session data is available), user-scoped encrypted session and profile backups.
- AES-256-GCM password-encrypted metadata export/import (excludes login cookies and saved sessions), safe JSON/CSV metadata import.
- Roblox Player installation discovery, launch request, process-exit watcher, opt-in throttled relaunch with best-effort disconnect log hints.
- Public game server browser, pagination, bounded low-population scan, cancellation, public Job-ID shuffle and shuffle-on-join, specific-server protocol handoff, private server official links.
- Favorite/recent games, related-game recommendations, game universe information and public outfits listing, with API failure feedback.
- Public username lookup and official player profile, public avatar lookup (does not track concealed in-game presence).
- Isolated account-specific Edge profiles; Edge password-manager shortcut, official Roblox login and 2FA/CAPTCHA flow. RAM never intercepts passwords.
- Account utilities (official profile, privacy/security/Quick Login, avatar, groups), authenticated loopback-only developer API with token authentication, selected-account profile/browser/client operations; process controls require app confirmation.
- FPS supported presets 30–240 and settings backup/restore, theme presets and accent editor, safe diagnostics export, Windows-compatible custom branded interface.
- Windows GitHub Actions build/publish pipeline plus smoke tests (no external Roblox account required).

## Partial or external client dependencies - **not verified**

- True one-click native Roblox account switching: client accepts/restores DPAPI-encrypted local cookie snapshot only when Roblox permits; authenticated API verification does not prove the Roblox client UI has logged in. Requires real account testing.
- Direct join to a specific server: Roblox may reject the protocol or server may fill. Server scans use public listing only and are never global guarantees.
- Complete AFK-kick detection: Roblox has no stable public signal; watcher uses heuristics and opt-in throttle.
- FPS edits: depends on installed Roblox settings XML format and client support.
- Public API features: Roblox can rate limit/change server, user, outfit or recommendation endpoints.
- Account-specific Edge password autofill: managed entirely by the browser; RAM does not automatically capture or save login credentials.

## Unsupported / not copied from original RAM

- Covert finder for a user who has disabled join/presence, remote in-game manipulation of arbitrary Roblox games, exploit-driven control, CAPTCHA solving/bypass and password harvesting.
- Importing raw `.ROBLOSECURITY` tokens or automatic cookie refresh behind Roblox's session defenses. Official Roblox sign-in and safe saved-client-session flow are supplied instead.
- Auto-updater and multi-instance launching (both expressly excluded by user).

## Testing

1. GitHub Actions first builds and runs `RAM.Modern.SmokeTests` on Windows 10/11 runner then publishes `RAM.Modern.exe` if tests pass.
2. The user's Windows functional test is a **single consolidated pass** covering the checklist in `ONE_PASS_TESTING.md`.
3. Use Advanced Controls > Account controls > Export safe diagnostics report for reproducible error evidence. Never export account cookies or passwords.
