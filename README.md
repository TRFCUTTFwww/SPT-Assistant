# SPT Assistant

[English](README.md) · [简体中文](README.zh-CN.md)

An in-game control panel for **SPT**, built with C# and Unity. Manage your character, raid, equipment, quests and server settings from one interface.

**Version 2.0.5** · **Current target: SPT 5.0 BE / EFT 1.3.0.1.48558** · Unity IL2CPP / .NET 10

> **UNSTABLE — EXPERIMENTAL.** This project has not undergone broad testing. Bugs, crashes, multiplayer desynchronization and unexpected profile changes are possible. Back up your profiles and configuration before installing or updating. A successful build or hot reload does not establish that every feature works in a raid.
>
> Development follows the latest SPT version. **Backward compatibility is not guaranteed.** The exact supported SPT/EFT build is stated for each release; this release targets the versions above. Fika support is experimental and has not been broadly tested across hosts, clients or mod combinations.

## Installation

1. Install the matching SPT/EFT build listed above and start SPT once so BepInEx interop assemblies are generated. Use a separate SPT installation.
2. Close that game's client and server. Back up `SPT_Runtime/user/profiles`, `BepInEx/config` and any existing Assistant files.
3. Download **SPT-Assistant-2.0.5-experimental.zip** from [Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases/tag/v2.0.5) and extract it to a separate folder. GitHub's automatic source archives contain source code, not an installed mod.
4. From the extracted folder, run the following in Windows PowerShell, replacing the example path:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language en
   ```

5. Start the SPT server, then start the client through the SPT launcher. Press **Right Shift** to open the panel. The interface language can be changed in the menu.

The installer copies only Assistant components, generates item-name/price caches from your own SPT database, preserves existing settings and API keys, and backs up replaced files under `mod-backups`. First installation of the full package creates a random local administration API key. **Fika clients:** use `-ClientOnly`; connect to the host and do not start a local server. Server administration features require matching server components and the host's key, and should be enabled only for trusted administrators. This package does not install Fika.

See [the bilingual installation guide](docs/INSTALL.md) for file locations, updates, source builds and removal, and [2.0.5 release notes](docs/release-2.0.5.md) for tested boundaries and remaining limitations.

## Features

| Area | What you can do |
| --- | --- |
| **Home dashboard** | View the current PMC profile, currencies, combat statistics and quest progress, with shortcuts to frequently used tools. |
| **Visuals & HUD** | Configure player, loot and vehicle ESP, world markers, floating information panels and a performance monitor. |
| **Navigation** | Locate quest objectives and extraction points, check distances and use navigation tools. |
| **Weapons & combat** | Edit weapons, adjust aiming tools and inspect ammunition, ballistics and armor protection. |
| **Character** | Manage health and movement tools, including noclip and optional laser-eye controls. |
| **Raid tools** | Control raid time and the day/night clock, pause timers, manage AI spawning and interact with raid loot. |
| **Items & equipment** | Search for items, send items by mail, manage equipment and configure repair, durability, keys and keycards. |
| **Quests & story** | Inspect quest progress, replay quests, manage story branches and skip eligible waiting periods. |
| **Profile data** | Manage skills, achievements, wallets and PMC/Scav character settings. |
| **Server settings** | Adjust insurance, repair, hideout, trading and raid options, plus karma and flea-market reputation. |
| **Interface** | Switch instantly between Chinese and English; customize hotkeys, panel layouts, opacity and themes. |
| **Hot reload** | Reload supported client and server modules from the panel while retaining the host. |

[Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases) · [Changelog](https://github.com/TRFCUTTFwww/SPT-Assistant/blob/main/CHANGELOG.md)
