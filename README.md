# SPT Assistant

2.3.4: [PVE season selection and custom corner version text](docs/release-2.3.4.md).


[English](README.md) · [简体中文](README.zh-CN.md)

An in-game control panel for **SPT**, built with C# and Unity. Manage your character, raid, equipment, quests and server settings from one interface.

**Version 2.3.4** · **Current target: SPT 5.0 BE / EFT 1.3.0.1.48558** · Unity IL2CPP / .NET 10

> **UNSTABLE — EXPERIMENTAL.** This project has not undergone broad testing. Bugs, crashes, multiplayer desynchronization and unexpected profile changes are possible. Back up your profiles and configuration before installing or updating. A successful build or hot reload does not establish that every feature works in a raid.
>
> Development follows the latest SPT version. **Backward compatibility is not guaranteed.** The exact supported SPT/EFT build is stated for each release; this release targets the versions above. Fika support is experimental and has not been broadly tested across hosts, clients or mod combinations.

## Installation: double-click Setup

1. Under **Assets** on the [release downloads](https://github.com/TRFCUTTFwww/SPT-Assistant/releases), download **`SPT-Assistant-2.3.1-Setup.exe`**. The `Source code` downloads are for developers.
2. Double-click the EXE and select **`EscapeFromTarkov.exe` in your SPT game folder**. Setup also tries to find installed games automatically.
3. Close the game and SPT server, then click **Install now**. Start SPT as usual and press **Right Shift** in game.

Keep the default mode for single-player or hosting friends; choose client only when joining a friend's server. The EXE contains the complete offline payload. It checks the target version and package hashes, backs up replaced files, and retains existing settings and administration keys. No commands, SDK installation or manual file copying are required.

The installer is unsigned and does not bypass Windows protection. Verify that you downloaded the official release asset before handling an unknown-publisher prompt. If Windows denies write access, use Setup's Retry as admin button.

See [installation and troubleshooting](docs/INSTALL.md) for technical details and the retained command-line ZIP option.

## 2.3.1 update

New misc switches: **Diplomatic immunity** keeps AI Scavs, bosses and followers neutral toward a player Scav without changing Fence reputation; **In high demand** instantly completes new flea listings with buyer **Assistant** and native Ragman payment. Flea fee waiver now handles cached client fees and the final server charge.

Train schedules, pre-raid duration, BTR stop markers, energy/hydration editing, an independent keycard-hacking toggle, and [SQLite profile backups with cross-character restoration](docs/profile-backup.md). Zero-fare cash checks, boundary-sniper protection and special-AI weapon glitch handling were revised. See [release notes](docs/release-2.3.1.md).

## Features

Blackwall Pulse is built into 2.3 under Fun Features. The release package includes its animation, textures and audio; installation backs up and disables the retired standalone plugin. See [Blackwall details](docs/blackwall-pulse.md).


The [chat and slash-command system](docs/CHAT.md) adds a configurable T chat overlay, command history, Tab completion and locally entered commands in single-player and supported FIKA scenarios. Text chat between users of the same upgraded server uses mutually verified TLS 1.3 channels with session-level forward secrecy. Read the fingerprint-verification steps and limitations before use. The old multiplayer branch is unchanged; remote game commands are disabled in main.

The opt-in [world editor](docs/world-editor.md) adds a crosshair context menu for creating, moving, copying and editing locally controlled AI and loose loot. It is unstable and still needs in-raid verification.

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

## 2.3: BTR, integrated Minecraft features and updates

Vehicles → BTR controls adds separate free ride, free delivery, instant delivery and expanded 10×100 (1,000-cell) transfer-container switches. Call BTR selects the nearest reachable native stop. Instant delivery mails items as soon as the raid result submits them; expanded dimensions apply to newly created containers. Settings → Version updates checks GitHub Releases and supports HTTP, HTTPS, SOCKS5 or system proxies, without automatic installation.

Pixel swords, ProjectE, the transmutation table, totems and Minecraft HUD now run inside Assistant on both client and server. The HUD master switch, hearts, hunger, hydration, experience and hotbar each have controls. Only the old independent Minecraft loaders are retired; other modules and Assistant core reload remain available. The 3-X weapon and its module are removed. Close both client and server for full installation; the installer backs up retired code and profiles while retaining MC configuration, wallets, knowledge, terminal data and pending-operation journals.

### Upstream Minecraft repositories and assets

| Project | Repository | Contribution |
| --- | --- | --- |
| AllTheModium | [AllTheModium](https://github.com/AllTheMods/AllTheModium) | Pixel swords / 像素剑 |
| ProjectE | [ProjectE](https://github.com/sinkillerj/ProjectE) | Transmutation, tools, artifacts / 转换桌、工具与饰品 |
| Modern Industrialization | [Modern Industrialization](https://github.com/AztechMC/Modern-Industrialization) | Quantum sword / 量子剑 |
| Applied Energistics 2 | [Applied Energistics 2](https://github.com/AppliedEnergistics/Applied-Energistics-2) | Wireless terminal artwork / 无线终端素材 |
| Thirst Mod | [Thirst Mod](https://github.com/ghen-git/Thirst-Mod) | Hydration HUD artwork / 口渴 HUD 素材 |

Vanilla Minecraft UI and totem assets belong to Mojang/Microsoft. Fixed revisions, hashes and third-party notices are retained in the Minecraft asset provenance and license files. See [2.3 notes](docs/release-2.3.md).
