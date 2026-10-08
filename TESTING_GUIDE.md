# RAM Modern Preview 5 - Windows acceptance tests

Use Windows 10/11 x64 and two test accounts, not a main Roblox account at first.

## Build

1. Extract ZIP and upload contents to your GitHub repository, preserving `.github/workflows/ram-modern-windows.yml`.
2. GitHub Actions > Build RAM Modern > Run workflow.
3. Confirm success in the `Publish Windows executable` step and obtain `RAM-Modern-win-x64` artifact.
4. Run `RAM.Modern.exe` from the extracted artifact.
5. If compilation fails, share the **first compiler error** and link to the run.

## Eight feature tests

1. **Encryption:** Start with a profile, save it, close RAM. Confirm `profiles.ramdpapi` exists in `%LOCALAPPDATA%\RAM.Modern`. Reopen and confirm the profile remains. If `profiles.json` from previous version exists, back up that directory before opening to test automatic migration. No account cookie files should be uploaded to GitHub.
2. **Group priorities:** Assign groups `010 Main` and `100 Storage`. Use Advanced Controls > Themes & Groups to modify priority; confirm list order updates and persists on restart.
3. **Small servers:** Games & Tools > Server Browser > enter a public game Place ID > Scan smallest. Check count/pages and select a server. Confirm that a Roblox deep-link *request* was sent; joining is verified only if the client actually joins that server.
4. **Discovery:** Games & Tools > Discover Games > paste a known Place ID > Load recommendations. Open a recommended game and add it to favorites. If endpoint rejects request, note the HTTP code.
5. **Account utilities:** Games & Tools > Account Utilities > choose a saved account > Open selected account browser. Sign in via official Roblox site, close and reopen isolated Edge; confirm session behavior. Chrome and the desktop Roblox client should remain unaffected.
6. **Watcher:** Advanced Controls > Roblox Watcher > Check recent disconnect signal. Test process detection, opt-in relaunch, and 5-minute cooldown. **Do not intentionally cause a network disconnect on an account with valuable unsaved game progress.** A manual close should not restart unless the heuristic considers a recent log entry a disconnect.
7. **FPS:** Close Roblox. Advanced Controls > FPS > read current setting, choose 60/120/240, apply and re-read. Verify in Roblox's graphics settings and restore the backup. If setting is absent/overwritten, use the in-game FPS selector.
8. **Themes:** Advanced Controls > Themes & Groups > choose Ocean, Preview, then Save settings. Restart RAM and verify accent persists. Try Custom `#35B7FF`.

## Authentication note

Roblox desktop login-file restore can be rejected despite a successful byte-for-byte restore. Check desktop user identity before trusting a restored session. Browser/Edge login does not automatically sign in Roblox desktop.
