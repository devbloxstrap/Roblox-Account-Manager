# RAM.Modern — Preview 7: account-switching architecture

## New implemented source modules (NOT yet Windows runtime verified)

| Module | Behavior |
| --- | --- |
| `BrowserLoginCapture` | Launch official Roblox login in an isolated Edge/Chrome profile with localhost-only DevTools; extract *only* the Roblox session upon explicit capture |
| `BrowserLoginWindow` | User-visible consent and Capture action; password/2FA remain on Roblox website |
| `AccountAuthStore` | Per-profile session encryption at rest via Windows DPAPI; no session secrets in JSON/CSV backups |
| `RobloxTicketLauncher` | Authenticate per-account with Roblox; fetch a short-lived ticket; invoke registered Roblox game-launch URI; handle identity mismatch, expired session and rotated cookie |
| Main dashboard | Add account via official browser, verify saved login, launch selected game/preferred game using account-specific ticket |
| Server Browser | Account selector, server join and shuffled server join using fresh account-specific ticket |
| Advanced Controls | Preferred game launch uses account-specific ticket |
| Smoke tests | Added protected-session-store and ticket URI offline tests |

The old file snapshot classes remain for backward compatibility/legacy desktop-client verification but **are not part of the switching path**. Old snapshots do not automatically become valid per-account browser sessions; re-add affected accounts through official browser login.

## Existing functionality retained
Encrypted metadata profile store, group priorities, games library, public servers/discovery/avatars, FPS controls, local API, watcher, import/export, diagnostics and themes. Auto-updater and multi-instance launching excluded by request.

## Not guaranteed / still restricted
Actual browser CDP access may be blocked by policy; Roblox may change its ticket endpoint or client launch URI; authentication tickets are server-authorized and cannot be generated locally. No CAPTCHA bypass, password interception, cookie theft from unrelated browser profiles, invisible player tracking, or arbitrary remote in-game control. Production-ready behavior requires Windows build and manual multi-account game test.
