# RAM 4.0 Preview 7 — one-pass verification

## What's changed
Previous Preview 6 copied the installed client's `%LOCALAPPDATA%/Roblox/LocalStorage/RobloxCookies.dat`. That was *not* equivalent to the original account manager's account-specific ticket launches. The old encrypted snapshots are left on disk as legacy backups but are no longer used for switching.

Preview 7 adds:
- A real, **explicit user-triggered** login import using a fresh temporary Edge/Chrome window on the official `https://www.roblox.com/login` website. Roblox password and 2FA stay in that official login page, and are not handled by RAM.
- User presses **Capture my signed-in account** after finishing login. RAM reads the session from **its own isolated browser**, verifies it against Roblox, and encrypts it with Windows DPAPI under the user's account. This session is sensitive, like a password.
- Each Roblox account is stored separately; no manual logout between account additions.
- A selected account's verified session requests a fresh ticket from Roblox, and uses the official registered `roblox-player:` URI for a **specific game Place ID**, optionally targeting a Job ID.
- Session identity is verified before *each launch*, and a rotated session value is encrypted back to the same account after identity checking.
- Multi-instance is disabled. Auto updater remains excluded.

## Build and one-pass checklist
1. Copy project contents (including `.github/workflows`) to the repo root; commit; wait for GitHub Actions **green** (static verification, smoke tests, Windows publish).
2. Open the produced EXE. Confirm your previous account profiles are still present. **Do not delete profiles or encrypted backups.**
3. Click **Add account via browser**. Edge/Chrome will show the real Roblox login page in a **separate temporary profile**. Finish account A login there (including any captcha or 2FA), then click **Capture my signed-in account** in RAM. Verify account A's username and saved-login badge.
4. Repeat **Add account via browser** for account B. There is **no need to log out account A**. Capture account B. Confirm two profiles with their correct usernames and user IDs.
5. Select account A and press **Verify saved account login**. Repeat for account B. Both should show Roblox-verified identities. A mere saved-file badge is not enough.
6. With Roblox Player closed, set a valid **Place ID** in the Quick Actions text box, select account A and click **Launch selected account game**. Confirm that the **actual Roblox game window** is running as account A (not merely that RAM reports URI handoff).
7. Close Roblox Player, select account B, press **Launch selected account game** with the Place ID, and confirm account B in the game. Then repeat for account A. No session swapping, browser logout, or multi-instance involved.
8. In **Game Explorer & Tools → Server Browser**, choose an account from **Launch as account**, load a Place ID, choose a server and join it. Verify the game and account. Optionally try Shuffle.
9. Confirm remaining features once: profiles, search/groups/sort, favorites/recents, encrypted metadata backup/import, FPS settings readback/restore, theme editor, watcher, and optional local API.
10. If failure occurs, send the **exact error text** and the **safe RAM diagnostics**. Do not send `.ROBLOSECURITY` cookies, tickets, isolated browser profiles, or `account-auth/` files.

## Compatibility / limitations
- **Login capture:** Edge/Chrome must allow app-created localhost debugging for a fresh isolated profile; browser-managed policies may block it. If capture says it cannot connect, this is a browser policy/compatibility error, not proof the account is invalid.
- **Roblox authorization:** The live users and ticket endpoints may change or refuse these requests. Browser capture success does **not** guarantee game launch. Roblox may invalidate or rotate a stored session.
- **Game only:** The fresh ticket targets a Roblox **game**, not the desktop Home screen. A Place ID is required.
- **No Windows execution here:** Linux-side source checks do not compile a WPF executable. GitHub Actions Windows build and this one-pass real Roblox test are required.
- **Security:** RAM reads the session secret only after you click Capture; it is DPAPI-encrypted on disk, never exported in metadata backups. The temporary browser profile is deleted on a best-effort basis when the browser closes. Check `%TEMP%` for an orphaned `RAMModern-Login-*` directory if the browser crashes, and delete it after closing all such browser windows.
