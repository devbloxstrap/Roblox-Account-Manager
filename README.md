# Roblox Account Manager

A modern Windows desktop application for managing Roblox account profiles, storing local profile metadata, and saving or restoring encrypted Roblox client session snapshots.

## Highlights

- Modern dark glass-style desktop UI
- Custom app icon and branding assets
- Resizable and maximizable custom window
- Local account profiles with favorites, groups, notes, and search
- Encrypted session snapshots for the current Windows user
- Quick links for Roblox login, home, profile pages, and game launching
- GitHub Actions workflow to build a Windows x64 executable

## Build on GitHub

Push the repository to GitHub and open **Actions**.
Run **Build RAM Modern** or trigger a push to `main`.
After the workflow succeeds, download the **RAM-Modern-win-x64** artifact.

## Local storage

Profile data is stored under:

`%LOCALAPPDATA%\RAM.Modern`

Session snapshots are stored in the same application folder and are protected with Windows DPAPI for the current user.

## Important notes

- Session restore is best effort only. Roblox may reject expired or revoked sessions.
- Close Roblox Player and Roblox Studio completely before saving or restoring a session.
- Do not share session files, cookies, or profile backups.
- Test with secondary Roblox accounts first.
