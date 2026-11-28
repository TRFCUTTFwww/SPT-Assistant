# Changelog

## 2.4 — Silence startup feature toggles and default kill effects off (2026-11-28)

- Subscribe to toggle notifications after startup config restoration, prevent duplicate listeners, and detach before module teardown.
- Default firearm kill effects and the center kill marker to off.
- Add real BepInEx configuration and compiled startup/default regression checks.

[Details / 更新说明](docs/release-startup-feature-toasts-20261128.md)


## 2.4 — Refresh GitHub release from current source (2026-11-28)

- Rebuild the full installer, manual ZIP and standalone core from source snapshot `04f9e0f`, including the latest AI spawn, hot-reload, inventory, trading and scope sticker changes.
- Synchronize the packaged changelog and release documentation, preserving the root README files.

[Details / 更新说明](docs/release-refresh-20261128.md)


## 2.4 — Fix raid stash weapon stuck hands & add scope sticker custom color settings (2026-11-27)

- Add `RaidItemPoolGuard` to verify and load missing `ObjectsFactory` Raid asset bundles and object pools (`AllResources`, `Prefab`, `UsePrefab`) across the full item/attachment/magazine/ammo tree before `Player.Proceed` (`Weapon`, `ThrowWeap`, `KnifeComponent`, `Meds`, `FoodDrink`, `RecorderItem`, usable/quick items) removes the current hands controller.
- Pre-warm missing item pools on inventory hover/drag/context-menu, `RaidStash` close, and in-raid item/preset additions, refresh `PlayerBody.SlotView` models that missed initial creation, and recover orphaned local hands (`HandsController == null`, `ProcessStatus == Scheduled`) while releasing `_nextProcessLocked`.
- Add static custom color mode (`固定颜色` / Solid color), 8 quick preset swatches, live R/G/B sliders, RGB/HEX inputs, and mask/multiply tinting in both the in-scope sticker editor (`Ctrl+F10`) and the `镜头与画面 → 瞄准镜贴纸` settings page, plus one-click color sync across all saved scope profiles.

[Raid stash hands fix & scope sticker color / 修复战局仓库枪械卡手与瞄准镜贴纸颜色设置](docs/release-raid-stash-weapon-pool-guard-20261127.md)

## 2.4 — Fix online sale freezing before submission (2026-11-27)

- Stop the multi-item offer ancestry check at the native inventory owner. EFT roots have an address whose container returns the root itself; traversing until null previously locked the main thread even for a single item.
- Reject unexpected cycles and excessive nesting before submission, while retaining duplicate and parent/child selection validation.
- Add 11 ancestry regression checks, including native-style self-referencing roots and bounded failure cases.

[Details / 更新说明](docs/release-online-sale-freeze-20261127.md)

## 2.4 — Use synced flea prices for ESP valuation (2026-11-27)

- ESP now follows the existing flea price source and manual overrides, reading the shared PVE/PVP cache in the background every five seconds. Offline use retains cached prices, with per-item local fallback.
- Invalidate container estimates and raid loot scans when prices change, keeping totals, sorting, value filters and high-value highlighting current. Preserve the existing live Bitcoin price.
- Add 19 regression checks for refresh, mode changes, overrides, malformed/missing feeds, offline restart and fallback.

[Details / 更新说明](docs/release-esp-online-prices-20261127.md)

## 2.4 — Keep paused selection time visible after switching maps (2026-11-27)

- Include the selected map in the pre-raid clock display cache, so reused native labels are refreshed with the paused time after switching maps.

[Details / 更新说明](docs/release-selection-clock-map-switch-20261127.md)

## 2.4 — Preserve initialization dependencies during fast reload (2026-11-27)

- Execute lifecycle actions before enumerating dependent steps within a batch. This fixes the startup null reference caused by enumerating `plugin.LoadSteps()` before the action that constructs the plugin.
- Construct the persistent hook patcher with the plugin so the early player visual guard can install before regular feature initialization.
- Add regression coverage for dependent initialization, batch boundaries, iterator disposal and failure ordering; retain host rollback and persistent hook router checks.

[Details / 更新说明](docs/release-fast-reload-init-order-20261127.md)

## 2.4 — Sub-second client hot-reload & persistent Harmony hook router (2026-11-27)

- Replace per-generation IL2CPP `Harmony.Unpatch` / `Harmony.Patch` (`DobbyDetour`) cycles with a process-wide `PersistentHarmonyRouter` (`Local.Spt5.HookBridge`) that keeps native detours alive once and swaps managed delegates in `< 1 ms` across `PayloadContext` reloads (including atomic zero-gap swap for `PlayerVisualGuard`).
- Upgrade `Local.Spt5.ESP.dll` host (`1.7.0`) and `RuntimeModule` with `FileSystemWatcher` instant detection, time-budgeted multi-step frame execution (`25 ms/frame`), silent fast-swap mode (suppressing modal black-screen delay unless reload exceeds `150 ms` or hits safety pause), and immediate unpausing ahead of asynchronous `last-good.module` / `previous-good.module` backups.
- Add cross-generation `AppDomain` read-only caches for `items.json` (~35 MB), `esp5-names.json`, `esp5-prices.json`, `item-name-fallbacks.json` and `en*.json` catalogs, skip redundant `ConfigurationManager.BuildSettingList()` reflection rebuilds when the settings window is closed, and add `scripts/Deploy-HotReload.ps1` for atomic publishing.

[Sub-second client hot-reload / 毫秒级客户端热更新与持久化 Hook 路由](docs/release-fast-hotreload-20261127.md)

## 2.4 — Clear item actions before showing the empty-cell menu (2026-11-27)

- Release injected Edit, FIR and online-offer buttons from the shared native menu before opening empty-cell creation actions. Right-clicking an item and then an empty cell no longer carries the previous item's actions into the creation menu.

[Details / 更新说明](docs/release-grid-context-cleanup-20261127.md)

## 2.4 — Show empty-cell native context actions (2026-11-27)

- Use `BaseEmptyContextInteractions` for grid creation actions. The previous `EmptyContextInteractions` returns `HasInteractions=false` unconditionally, so EFT refused to show the menu even with dynamic actions attached. Report a failed menu show instead of silently doing nothing.

## 2.4 — Native inventory context menu, live item synchronization and multi-item online offers (2026-11-27)

- Complete native inventory add events and search-state updates so empty-cell additions can appear and become interactive during a raid; report failures and roll back failed placement batches before publishing events.
- Use EFT's native context menu for empty-cell catalog, JSON and clipboard additions. Add independent persistent controls for right-click creation, editing and FIR changes under Items → Inventory context menu.
- Label the existing grenade trajectory toggle explicitly as Show grenade trajectory while preserving saved settings.
- Allow native online-trading selection of multiple matching items/stacks, combine root quantities without counting attachments, and remove every selected item after a successful listing.

[Details and validation / 更新说明与验证](docs/release-inventory-context-online-multi-20261127.md)

## 2.4 — Unrestricted JSON editors, empty-cell item creation, BTR hostility reset, dynamic scope stickers & raid fixes (2026-11-27)

- Remove artificial limits, field locks, size caps and template/range blocks across the Item/Tree JSON Editor, Profile JSON Editor, Server JSON Editor and numeric/spawner/flea/backup/raid/hideout tools.
- Support right-clicking any empty inventory grid cell `(x, y)` to add items or weapon presets directly via catalog search or JSON (single object, flat tree, `{ "Items": [...] }` build or Base64) with live grid refresh and profile persistence.
- Add one-click BTR hostility clearing and a persistent Keep BTR Friendly toggle in `18. BTR`.
- Add dynamic color modes (Rainbow, Two-color gradient, Breathing pulse) and adjustable animation speed (`0.01`–`50x` slider + exact input) to `73. 倍镜贴纸`.
- Fix Infinite Injectors for both single-use and multi-use stimulators by zeroing `MedEffect` resource usage and blocking `MedEffectHelper.RemoveItem` consumption removal.
- Fix Grenade Trajectory preview lag, hand-bone skew and stamina random jitter by updating every `LateUpdate` along camera aim with native throw force and 3D `LineRenderer` rendering.

[Unrestricted JSON, grid add, BTR, scope sticker & raid fixes / 移除限制、空格添加物品、BTR 清仇恨、贴纸动态变色与战局修复](docs/release-unrestricted-json-btr-sticker-fixes-20261127.md)

## 2.4 — Updated GitHub release package (2026-11-27)

- Include the current changelog, flea pause fixes and linked release notes in both the offline installer and manual ZIP. Update the English and Chinese 2.4 notes for current offer state, unlimited-stock pause/resume and immediate list refresh.

## 2.4 — Refresh native offers immediately after editing (2026-11-27)

- Force the open native flea list to reload immediately after an offer is saved. Pausing or resuming replaces the native offer ID; the visible list now loads that replacement and applies the already updated state without waiting for the periodic metadata poll or a manual refresh.

[Immediate offer refresh / 报价状态立即刷新](docs/release-flea-immediate-refresh-20261127.md)

## 2.4 — Preserve paused offers with unlimited stock (2026-11-27)

- Preserve `RagfairOffer.ExtensionData` when projecting unlimited stock, paused expiry or Rouble-only requirements. SPT's record copy constructor drops that base dictionary; losing its pause marker made the editor report permanent lifetime and kept a permanent expiry when resuming temporary quotes.
- Remove the offer editor's bottom explanation paragraph and shorten its save confirmation.
- Reproduce the failure with unlimited stock, verify metadata survives each projection, and cover saved and temporary pause/resume flows. Repeat both flows through the running server: 1020 frozen seconds resume as 1019 remaining seconds with `paused=false` and `permanent=false`.

[Unlimited-stock pause fix / 无限库存报价暂停修复](docs/release-flea-pause-metadata-20261127.md)

## 2.4 — Read current flea offer state when editing (2026-11-27)

- Read the selected offer from the server whenever either editor entry opens, resolve saved and native IDs, and fill all fields from the current result. Show paused state explicitly and reject failed or missing reads before saving.
- Refresh native offer metadata while the market is visible, including when a previous metadata cache already exists. Read active saved offers from the live pool so pause markers, stock and other editable values reflect the current offer.
- Verify temporary/native offer lookup, stale-ID rejection, pause/resume state, current stock, inactive records and SQLite restoration across two processes.

[Offer editor state fix / 修改窗口报价状态修复](docs/release-flea-editor-state-20261127.md)

## 2.4 — Paused flea countdown display (2026-11-27)

- Project paused offer expiry as `now + pausedRemainingSeconds` in native ragfair responses and quote descriptions so paused quotes never expose `PermanentEndTime` (`9999-12-31`) as permanent lifetime while remaining protected from server expiration.
- Compute native offer expiry directly from `offer.EndTime - DateTimeExtensions.UtcNow` (fixing local timezone offset distortion in `UTC+8`), refresh visible `OfferView` rows immediately when quote metadata arrives, lock paused native countdown labels every frame, and prevent negative permanent minutes from silently overriding paused quotes.

[Paused countdown fix / 暂停报价剩余时间](docs/release-flea-paused-time-20261127.md)

## 2.4 — Main package and menu polish (2026-11-26)

- Keep uninstall/reimport support without deleting saved module data. Discover settings from module metadata, including legacy installed manifests, without compiling optional module identities into the core.
- Move the raid Map mode button to the end, add the supplied server page background and update installer preservation checks.
- Publish 2.4 from the current next source. Publish reviewed main-package documentation and a fresh source snapshot.

## 2.4 — Dedicated night vision page (2026-11-26)

- Add Visuals → Night vision for all full-screen and optic night vision controls, including hotkey, profiles, light suppression and custom rendering parameters. Remove duplicated night vision entries from Character display and the scope settings page.
- Rename Optics to Scope stickers, retaining sticker, zoom and rangefinder controls. Route search results and legacy scope page navigation to the appropriate page while preserving existing configuration keys and values.

[Night vision page / 夜视独立页面](docs/release-night-vision-page-20261126.md)

## 2.4 — Custom flea offer layout (2026-11-26)

- Place Add Offer, Show Edit and Show Withdraw in equal-sized toolbar cells. Use the same control height, spacing and alignment for search and status filters.
- Align each offer’s item, seller, price, stock, rating and status; use equal action buttons in a side column on wide panels and a bottom row on compact panels. Return to the top when changing the search.

[Custom flea offer layout / 自定义报价排版](docs/release-flea-layout-20261126.md)

## 2.4 — Fun module uninstall (2026-11-26)

- Add Delete / Confirm delete next to each installed fun module. Wait for the universal host to finish its existing busy-operation and shutdown lifecycle before removing installed releases.
- Keep module configuration and persistent state for later reimport. Persist pending removals across main-module hot reloads, reject stale host confirmations and protect paths outside the selected module.

[Fun module uninstall / 趣味模块卸载](docs/release-fun-uninstall-20261126.md)

## 2.4 — New repository baseline (2026-11-26)

- Start this repository from the current SPT Assistant source with one independent root commit. Prior Git commits, branches, tags and the old history directory are not imported. The previous repository is archived.
- Include native flea editing and withdrawal behind independent default-off switches, compact custom-offer management, SQLite persistence, complete item-tree restoration and pausable remaining time.
- Remove redundant source archives and legacy installers/packaging scripts; build current modules using the current source tree and CI.

[Withdrawal, cleanup and new repository / 下架、清理与新仓库](docs/release-flea-withdraw-new-repository-20261126.md)
[Flea editing and module separation / 报价编辑与功能拆分](docs/release-flea-edit-20261126.md)
