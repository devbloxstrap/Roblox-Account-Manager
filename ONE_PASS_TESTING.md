# RAM Modern 4.0 - one-pass Windows acceptance test

**Run only after GitHub Actions 'Build RAM Modern' is green.** Do not use important/valuable Roblox accounts until this passes with a spare test account. Run no more than one Roblox client at a time, and never share cookies, recovery codes, passwords, session data or screenshots containing them.

## GitHub deployment (once)

Extract this ZIP and upload/overwrite the included `RAM.Modern/`, `RAM.Modern.SmokeTests/`, `.github/workflows/ram-modern-windows.yml`, and root documentation in `https://github.com/devbloxstrap/Roblox-Account-Manager`. Commit once, wait for Actions green, download `RAM-Modern-win-x64` artifact, and run `RAM.Modern.exe`.

## Single acceptance session (one run)

Mark each check pass/fail and take a screenshot **only of non-secret app UI**:

- [ ] Actions succeeds: tests show `RESULT: 8 PASS, 0 FAIL` and EXE artifact exists.
- [ ] App starts, window resizes/maximizes; icon and theme controls show correctly.
- [ ] Create 2 test account profiles; search, sort, favorites, group priority, save, restart, and verify encrypted profiles persist.
- [ ] Advanced > Export encrypted metadata backup. Import with correct password; wrong password must be rejected.
- [ ] Game Explorer > public server list loads; scan 5 pages, cancel scan, shuffle real Job ID; shuffle on join; deep link may require Roblox client support.
- [ ] Games Library: favorite, recent and clear; recommendations; private server official link, universe/outfits.
- [ ] Public Player Lookup shows correct user ID/profile; no private player presence claimed.
- [ ] Sessions & Safety: audit count, verify current client identity, open isolated Edge login and password settings.
- [ ] Desktop test account: sign in via *Roblox Player*, close it, Save Current Session, select another previously verified test account, restore and relaunch. Record actual observed Roblox username **without including auth tokens**. If rejected, mark client incompatibility.
- [ ] Advanced > FPS: read supported setting, apply supported preset, restore backup. Close Roblox before editing.
- [ ] Advanced > Watcher: process count, start/exit notifications. Opt-in relaunch test only with disposable client.
- [ ] Advanced > Developer API: default disabled; enable locally; unauthenticated requests denied; correct bearer token returns `/v1/status` (do not share token).
- [ ] Advanced > Account controls > Export safe diagnostics report; send only this txt plus screenshots or GitHub Actions error logs if something fails.

**Important:** Full remote in-game game manipulation, CAPTCHA bypass, raw token import, automatic cookie refresh, hidden-player stalking, auto-updater and multi-instance support are not part of this release. Public API behavior and Roblox session acceptance cannot be verified on this Linux build environment.
