# SPT Assistant

[English](README.md) · [简体中文](README.zh-CN.md)

An extensible in-game control panel, world editor, and client/server toolkit for **SPT (Single Player Tarkov)**, built with C#, Unity IMGUI/Harmony, and .NET 10. Manage your character, raids, AI, items, quests, story, and server rules from one unified interface.

**Version:** `2.4` · **Target:** `SPT 5.0 BE / EFT 1.3.0.1.48558` (Windows x64, Unity IL2CPP / .NET 10)


---

## Quick Installation (Recommended)

1. Open [Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases) and under **Assets**, download **`SPT-Assistant-2.4-Setup.exe`** (do **not** download `Source code` unless you are building from source).
2. Double-click the installer and select **`EscapeFromTarkov.exe`** inside your SPT game folder.
3. Close both the EFT client and the SPT server, then click **Install now**.
4. Start the SPT server and game as usual, then press **Right Shift** in game to open the panel.

- **Single-player / Fika Host:** Keep the default full installation mode.
- **Fika Client (joining a friend):** Select **Client only**.
- Setup displays Chinese and English together. The language selector sets the in-game Assistant language. Releases also include `Local.Spt5.ESP.Core.module` for existing installations; load it from **Settings & Hotkeys → Version Update**. Use the full installer when updating server components or resources.
- The Setup EXE is self-contained and offline: it checks target version compatibility and file hashes, backs up replaced files under `mod-backups/`, and preserves existing configs and admin keys without requiring `.NET SDK` or manual file copying.
- The installer is unsigned. Only proceed past Windows SmartScreen/unknown-publisher prompts if you downloaded it directly from official Releases; use **Retry as admin** inside Setup if the target folder denies write access.

For manual ZIP/PowerShell installation, troubleshooting, and clean removal, see [Installation & Troubleshooting](docs/INSTALL.md).

---

## Architecture & Hot Reload

SPT Assistant is split into lightweight permanent hosts and hot-reloadable feature modules:

| Component | Role | Hot Reload |
| --- | --- | --- |
| `esp-host` (`Local.Spt5.ESP.dll`) | BepInEx IL2CPP client host, hot reload lifecycle, diagnostics | Host (restart to update) |
| `esp-cover` (`Local.Spt5.ESP.Core.module`) | Main UI, ESP, combat, world editor, chat, JS corner scripts, built-in fun features | Yes (`Ctrl+F1` or menu) |
| `gear-mail` (`LocalGearMail.dll`) | SPT server mod: item mail, SQLite backups & raid history, TLS 1.3 chat relay | Server restart |
| `gear-admin-module` (`LocalGearAdmin.module`) | Reloadable server admin logic (profile data, story, Cultist Circle, world editor) | Yes (from menu/script) |
| `raid-controls-server` (`LocalRaidControls.dll`) | Server rules: insurance, repair, container nesting, BTR, per-map AI/raid presets | Server restart |
| `universal-host` / `universal-sdk` | General hot-reload loader (`Ctrl+F9`) for new standalone client mods | Host / Module (`Ctrl+F9`) |

---

## Feature Overview

Press **Right Shift** to open the panel (`F12` for legacy config menu, `中文 · English` at the top-right to switch language at any time). Controls are organized into three lanes:

### Common Assists
- **Home (`主页面`)**: PMC profile summary, currencies, combat statistics, quest progress, and quick shortcuts.
- **Visuals (`视觉`)**: Player/AI/loot/container/vehicle/airdrop/BTR-stop ESP, optic-scope adaptive boxes (`ScopeBoxes`), muzzle aim line, AI route & destination overlays, battle/hit/kill info, mine/tripwire highlights, third-person camera, performance monitor (`FPS` / `P99`), and layout/opacity customization.
- **Navigation (`导航`)**: Extraction markers & force/ignore controls, map transit tools, quest objective locations, ground navigation, and teleportation.
- **Shooting (`射击`)**: Aim assist, micro-aim, target exclusions, recoil/weapon comfort, hit sounds, kill effects, and grenade trajectories.
- **Character (`人物`)**: God mode (`F8`), AI ignore (`F9`), body-part health/energy/hydration editing, full-body bullet ricochet, door breaching, noclip, buffs, and death retention.
- **Raid (`战局`)**: Map/event modes, raid clock & day/night freeze, rolling timeout protection, pre-raid start time, in-raid stash access, proximity force-loot, AI difficulty, and per-map AI/raid parameter presets.
- **Raid History (`历史战局`)**: Persistent SQLite log of entry gear, extracted/kept/new items, playtime, exfil status, and detailed kill records.
- **Vehicles (`载具`)**: BTR controls (free ride, free/instant delivery, 10×100 transfer container, call to nearest stop), Armored Train arrival/departure controls, and airdrop management.
- **Items (`物品`)**: Item search & mail delivery, player weapon presets, full gear kits, and repair/durability rules (max-durability trader repair, armor care, container nesting, infinite keys/keycards/injectors, instant container search).
- **Fun Features (`趣味功能`)**: Built-in **Laser Eyes** and generic module import, settings, hot reload, and removal.
- **Story (`剧情`)**: Main story chapter progress, trader quest replay, mutually exclusive branch switching, endings, and skipping eligible quest wait timers.
- **Data (`数据`)**: Skill & achievement editors, wallet/TC transfers, PMC/Scav equipment management, [SQLite profile backups & cross-character restoration](docs/profile-backup.md), profile JSON editing, and Cultist Circle preview & instant finish.
- **Server (`服务端`)**: 30+ server configuration switches (insurance, repair, hideout, traders, raid) plus PMC/Scav karma and flea market reputation.
- **Miscellaneous (`杂项`)**: Independent switches including Scav **Diplomatic immunity**, **In high demand** (instant flea sales), **Oversupply**, Trader/Flea clearance sale, flea fee waiver, boundary sniper immunity, and fun-item policy.

### Developer Tools
- **AI Development (`AI 开发`)**: Live AI list, weapon swapping, spawning, behavior/route editing, equipment/buff editing, teleportation, scene templates, corpse cleanup, and live server AI spawn info.
- **Weapon Editor (`枪械编辑`)**: Live inspection and tuning of held/slotted weapons and attachments.
- **Model & Camera (`模型与镜头`)**: Free camera, character model options, and pose/action locks.
- **Toy Gun Development (`玩具枪开发`)**: Configurable toy gun spawning, hit actions (teleport/kill/etc.), fire rate/ammo, and bullet impact markers.
- **Developer Tools (`开发工具`)**:
  - Opt-in [World Editor](docs/world-editor.md) (`F6`): crosshair & `Alt` marquee selection for creating, moving, copying, cloning, locking, and editing locally controlled AI, loose loot, corpses, landed airdrops, containers, doors, and claymores, with undo & blueprints.
  - [Chat & Slash Commands](docs/CHAT.md) (`T` / `/`): configurable overlay with contextual Tab completion, local slash commands (also scoped for Fika hosts/members), and mutually verified **TLS 1.3** text chat with session-level forward secrecy.

### Settings & Diagnostics
- **Debug (`调试`)**: Runtime diagnostics and opt-in development tools.
- **Settings & Hotkeys (`设置与热键`)**: Searchable [hotkey editor](docs/hotkeys.md) (single keys, combos, mouse buttons), config profiles, theme & layout editor, [JavaScript corner scripts](docs/corner-version-scripts.md) (embedded Jint engine with `fetch`, live `game` snapshot, and `@game-thread` access), GitHub Release update checker (HTTP/HTTPS/SOCKS5 proxy support), and sanitized diagnostics ZIP export.

---

## Building from Source & Documentation

Building requires the **.NET 10 SDK** and locally installed SPT 5.0 BE / EFT 48558 interop assemblies (not included in this repository). See [Installation & Troubleshooting](docs/INSTALL.md) and [Universal Mod Host](docs/universal-mod-host.md) for build and packaging instructions.

```powershell
# Run from the SPT game root with the repository placed at _spt5_esp_fix
dotnet build _spt5_esp_fix/esp-cover/Esp.csproj -c Release
dotnet build _spt5_esp_fix/gear-mail/GearMail.csproj -c Release
dotnet build _spt5_esp_fix/raid-controls-server/RaidControls.csproj -c Release
dotnet run --project _spt5_esp_fix/probe/Probe.csproj -c Release
```

### Documentation Index
- [Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases) · [Changelog](CHANGELOG.md)
- [Installation & Troubleshooting](docs/INSTALL.md) · [Hotkeys](docs/hotkeys.md)
- [Chat & Slash Commands](docs/CHAT.md) · [Command Options](docs/COMMAND-OPTIONS.md)
- [World Editor](docs/world-editor.md) · [SQLite Profile Backups](docs/profile-backup.md)
- [JavaScript Corner Scripts](docs/corner-version-scripts.md)
- [Raid Parameters](docs/raid-parameters.md) · [Universal Hot-Reload Host](docs/universal-mod-host.md)

---

## Third-Party Attributions & License

Project code is licensed under the [MIT License](LICENSE). Third-party assets retain their original rights and licenses.

### Embedded JavaScript libraries

[Jint](docs/licenses/Jint.txt) and [Acornima](docs/licenses/Acornima.txt) retain their original licenses.
