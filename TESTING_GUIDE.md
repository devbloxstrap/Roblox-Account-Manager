# Preview 7 testing

For a single end-to-end testing session, use [ONE_PASS_TESTING.md](ONE_PASS_TESTING.md).

**IMPORTANT:** Preview 6's `Save verified session` / `Restore selected session` steps based on copying `RobloxCookies.dat` are obsolete and must NOT be used to test Preview 7 account switching. Each account now has its own browser-captured, DPAPI-encrypted login and fresh game-launch ticket.

The correct process is: Add via browser -> Capture signed-in account -> Repeat without logout -> Verify saved login -> Enter game Place ID -> Launch selected account -> Inspect actual Roblox Player.

The source-level verifier is not a substitute for GitHub Actions compilation or real Roblox gameplay testing.
