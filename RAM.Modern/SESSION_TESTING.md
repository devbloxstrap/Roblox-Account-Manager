# RAM Modern session testing (Windows 11)

This is a best-effort local session-file switcher based on Voidstrap's approach, **not** a supported Roblox authentication API. It does not recover expired sessions. Do not use your primary account for initial tests.

1. Download the GitHub Actions artifact and extract it. Run `RAM.Modern.exe` on Windows 10/11.
2. Create and save profile A and profile B.
3. Close all Roblox/Studio windows completely; check Task Manager.
4. Sign in to A using the official Roblox Windows client (not only your web browser), close Roblox, select profile A, choose **Save current session** and confirm.
5. Repeat with B. Close Roblox each time.
6. Select A, choose **Restore selected session**, then open the official Roblox app. Check *in the client* that A is the account signed in.
7. Close Roblox; restore B and verify client identity again.
8. Test switching after restarting Windows; note whether Roblox asks for authentication.
9. If client requires login, the session expired or the client rejected the file. Sign in normally, close Roblox and save a new session. This app cannot renew revoked credentials.
10. Check `%LOCALAPPDATA%\RAM.Modern\sessions` for `.dpapi` files; **never upload those files, logs containing cookies, or your RobloxCookies.dat**.

Troubleshooting: If `RobloxCookies.dat` is absent, the installed client or its storage format differs; feature will not work without a compatible live file. Save operation cannot determine which Roblox account created the login file: verify account manually before saving. Restore creates an encrypted `previous-session.dpapi` backup. This preview does not automate session validation or game launch under a chosen account.
