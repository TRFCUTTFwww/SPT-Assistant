# SPT Assistant 2.4
[简体中文](release-2.4.zh-CN.md)

Target: SPT 5.0 BE / EFT 1.3.0.1.48558, Windows x64. Changes since GitHub v2.3.5.

## Release refresh — 2026-11-28

- Speed up AI spawning with warm donor profile cloning, concurrent PMC preparation and immediate settlement when the spawn is ready.
- Add fast client hot reload with persistent Harmony hooks; preserve initialization order and harden fallback hook cleanup.
- Load missing item/attachment pools before switching hands and avoid the shared generic IL2CPP Proceed stub when installing the guard.
- Add solid/custom scope sticker colors with presets, RGB/HEX input and synchronization across saved optics.
- Fix online sale freezing at native inventory roots, support multiple selected items, and use synced flea prices for ESP valuation.
- Add native inventory context actions and unrestricted JSON editing; improve empty-cell item creation, BTR friendliness, injector use, grenade trajectories and map-clock refresh.

The refresh includes the full current source snapshot, changelog and linked documentation. Update with the full installer or ZIP when server components change. Game visuals remain subject to in-game verification by the user.

## Downloads

- `SPT-Assistant-2.4-Setup.exe`: full offline installer. Chinese and English appear together throughout Setup; the language selector sets the in-game Assistant language.
- `Local.Spt5.ESP.Core.module`: standalone core for an existing installation. Load it from Settings & Hotkeys → Version Update, or replace the installed core while the game is closed. Server components and external resources require the full package.
- `SPT-Assistant-2.4-experimental.zip`: manual installation package.
- `SHA256SUMS.txt`: SHA-256 checksums for all three downloads.

## Added and improved

- Fixed paused flea offers displaying permanent lifetime, including when unlimited stock is enabled. The editor reads the current offer state; resuming restores the remaining countdown, and saving immediately refreshes the open offer list. Removed the editor's explanation footer.
- Reworked the single-page installer for bilingual labels, modes, progress, errors and recovery actions, with a scrollable layout for smaller windows. Setup title, executable metadata and application manifest use the release version.
- Added scope sticker import/editing, per-optic layouts, Up/Down cycling, automatic contrast and confirmed-hit flashes.
- Added optional native extended optical zoom through Alt+wheel, including fixed optics and sensitivity scaling. The native reticle/magnification key retains the optic's original endpoints.
- Added steady aim, optional bullet wall penetration and independent mixed-caliber magazine/chamber loading. Fixed native feeding and chambering compatibility and expanded Infinite Launcher support.
- Added separate No Bush Slowdown and No Swamp Slowdown switches. Fixed missed bush colliders. Magazine information shows the chambered ammunition specification, including revolvers and empty chambers.
- Added owned-item and dogtag editing, removed the 10,000 dogtag level cap, and added individual buffs, profile JSON editing/backups, raid-history redelivery and the Nw11 chat contact.
- Added the dedicated flea market page, PvP/PvE prices, per-item prices/permissions, named presets, rule switches, automatic pricing and net-return comparisons.
- Added in-raid listings, backpack purchases with overflow mail, separate listing limits and Smuggler restrictions controls. Fixed purchase retries and save-failure rollback.
- Added raid event notifications, map-clock pause, RGB/HEX sky colors, camera-view marker filtering, loot colors and ammunition statistics.
- Improved continuous container placement, optional menu artwork and precise AI difficulty input.
- Added a six-tab memory editor with scanning, address freezing, hardware access monitoring, register snapshots, disassembly/assembly, code caves, AOB patches, pointer scans and structure dissection.
- Added local module selection and Gitea CI artifacts. Reduced repeated inventory scans, JavaScript snapshots and ESP work; fixed hit feedback, hot-reload duplicates, input focus and raid cleanup.

- Fixed reinstall backups and rollback for resource paths beyond the traditional Windows path limit.
- Mascot backgrounds now default to off.

- Added persistent custom flea offers with edit/withdraw controls and pausable remaining time; aligned the offer toolbar and rows.
- Added a dedicated Night vision page with Nw11 green, blue and Nw11昼 profiles for global and optic vision; renamed the optic page Scope stickers.
- Added module uninstall with saved-data retention and reimport support. Moved Map mode to the end and added the supplied Server page background.

## Removed

- Built-in scope sticker artwork. Image import, editing and cycling remain available; empty libraries draw no sticker.

- Independent seasonal profiles and entry points.
- Automatic pausing on ordinary game errors, the recovery overlay, emergency rescue extraction and the external crash monitor. Hot-update failure handling, rollback and diagnostic logging remain available.

The detailed source comparison is available in [English](release-since-github-20261125.en.md) and [Chinese](release-since-github-20261125.zh-CN.md).
