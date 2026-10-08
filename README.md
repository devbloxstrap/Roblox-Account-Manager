# Roblox Account Manager

A modern, open-source Windows desktop application for organizing Roblox accounts with a clean desktop interface.

## Features

- Profiles, groups, favorites, notes and account search
- Dark-themed WPF desktop interface with C# and .NET 10
- Locally saved profile data and backups
- Official Roblox login and profile links
- Experimental DPAPI-protected local session snapshots and restoration
- Windows 10 / Windows 11 x64 target

**Session restore is experimental.** Roblox may reject restored sessions or alter its local session format. Expired sessions require signing in again. Multi-instance launching is not included.

## Build and download

Open [Build RAM Modern](https://github.com/devbloxstrap/Roblox-Account-Manager/actions/workflows/ram-modern-windows.yml) under GitHub Actions, select **Run workflow**, then download the `RAM-Modern-win-x64` artifact from the completed run. Extract before running `RAM.Modern.exe`.

Local build (Windows with .NET 10 SDK):

```powershell
dotnet publish RAM.Modern/RAM.Modern.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## Testing

This is a preview and has not yet completed Windows runtime and current Roblox authentication testing. See [Session Testing](RAM.Modern/SESSION_TESTING.md).

## Security

Never upload Roblox cookies, login files, or account backups. Test using secondary accounts.

## Licensing

This repository includes existing GPL-licensed source and other third-party components. Preserve original license and copyright notices and review redistribution obligations before publishing binaries.
