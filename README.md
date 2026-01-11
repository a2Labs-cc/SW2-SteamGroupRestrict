<div align="center">

# [SwiftlyS2] SteamGroupRestrict

<a href="https://github.com/a2Labs-cc/SW-SteamRestrict/releases/latest">
  <img src="https://img.shields.io/github/v/release/a2Labs-cc/SW-SteamGroupRestrict?label=release&color=07f223&style=for-the-badge">
</a>
<a href="https://github.com/a2Labs-cc/SW-SteamGroupRestrict/issues">
  <img src="https://img.shields.io/github/issues/a2Labs-cc/SW-SteamGroupRestrict?label=issues&color=E63946&style=for-the-badge">
</a>
<a href="https://github.com/a2Labs-cc/SW-SteamGroupRestrict/releases">
  <img src="https://img.shields.io/github/downloads/a2Labs-cc/SW-SteamGroupRestrict/total?label=downloads&color=3A86FF&style=for-the-badge">
</a>
<a href="https://github.com/a2Labs-cc/SW-SteamGroupRestrict/stargazers">
  <img src="https://img.shields.io/github/stars/a2Labs-cc/SW-SteamGroupRestrict?label=stars&color=e3d322&style=for-the-badge">
</a>

<br/>
<sub>Made by <a href="https://github.com/agasking1337" target="_blank" rel="noopener noreferrer">aga</a></sub>

</div>


## Overview

**SwiftlyS2-SteamRestrict** is a plugin for **SwiftlyS2** that restricts player access to the server based on their Steam profile information.

It automatically checks players' Steam profiles using the Steam Web API and enforces restrictions such as:

- **Minimum CS2 Level**
- **Minimum Hours Played**
- **Minimum Steam Level**
- **Minimum Account Age**
- **Block Private Profiles**
- **Block Banned Accounts** (VAC, Trade, Game bans)
- **Require Steam Group Membership**

Players who do not meet the criteria are warned and eventually kicked from the server.

## Support

Need help or have questions? Join our Discord server:

<p align="center">
  <a href="https://discord.gg/d853jMW2gh" target="_blank">
    <img src="https://img.shields.io/badge/Join%20Discord-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord">
  </a>
</p>


## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbspDownload Latest Plugin Version</strong> ⇢
    <a href="https://github.com/a2Labs-cc/SW-SteamGroupRestrict/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbspDownload Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Installation

1. Download/build the plugin.
2. Copy the published plugin folder to your server:

```
.../game/csgo/addons/swiftlys2/plugins/SteamRestrict/
```

3. Ensure the plugin has its `resources/` folder alongside the DLL (translations, gamedata).
4. Start/restart the server.

## Configuration

The plugin uses SwiftlyS2's JSON config system.

- **File name**: `config.json`
- **Section**: `swiftlys2/configs/plugins/SteamRestrict`

On first run the config will be created automatically. The exact resolved path is logged on startup:

### Key Configuration Options

- `LogProfileInformations`: Enable/disable logging of profile information (default: true)
- `ChatPrefix`: Prefix for chat messages (default: "[SteamRestrict]")
- `ChatPrefixColor`: Color for the chat prefix (default: "[red]")
- `SteamWebAPI`: Your Steam Web API key (required for functionality). You can obtain an API key from https://steamcommunity.com/dev/apikey
- `MinimumCS2Level`: Minimum CS2 level required (default: -1, disabled)
- `MinimumHour`: Minimum hours played required (default: -1, disabled)
- `MinimumSteamLevel`: Minimum Steam level required (default: -1, disabled)
- `MinimumSteamAccountAgeInDays`: Minimum account age in days (default: -1, disabled)
- `BlockPrivateProfile`: Block players with private profiles (default: false)
- `BlockTradeBanned`: Block trade banned accounts (default: false)
- `BlockVACBanned`: Block VAC banned accounts (default: false)
- `SteamGroupID`: Require membership in this Steam group (leave empty to disable)
- `BlockGameBanned`: Block game banned accounts (default: false)
- `PrivateProfileWarningTime`: Time in seconds to warn private profile users before kick (default: 20)
- `PrivateProfileWarningPrintSeconds`: Interval for warning messages (default: 3)

## How It Works

### Steam Profile Checking
The plugin uses the Steam Web API to fetch detailed player profile information when they connect to the server.

### Restrictions Enforcement
Players are evaluated against the configured restrictions:
- **Level and Hours**: Minimum requirements for CS2 level, hours played, and Steam level
- **Account Age**: Ensures accounts are old enough to prevent new/alternative accounts
- **Bans**: Blocks players with VAC, trade, or game bans
- **Privacy**: Can block or warn players with private profiles
- **Group Membership**: Requires players to be members of a specific Steam group

Players who fail to meet the criteria receive warnings in chat and are eventually kicked from the server.

## Building

```bash
dotnet build
```

## Credits
- Original plugin [KitsuneLab-Development/CS2-SteamRestrict](https://github.com/KitsuneLab-Development/CS2-SteamRestrict)
- Developed by [agasking1337](https://github.com/agasking1337)
- Readme template by [criskkky](https://github.com/criskkky)
