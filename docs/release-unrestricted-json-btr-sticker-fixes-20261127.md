# Unrestricted JSON Editing, Empty-Cell Item Add, BTR Hostility Reset, Dynamic Scope Stickers & Raid Fixes (2026-11-27)

## Summary / 更新摘要

1. **Unrestricted JSON Editors & Limit Removals / 移除 JSON 编辑器与各项人工限制**
   - **Item & Item-Tree JSON Editor (`ItemInventoryEditor.Json.cs`, `InventoryItemJsonRules.cs`, `InventoryItemJson.cs`)**: Removed size caps, depth caps, field locks (`_id`, `_tpl`, `parentId`, `slotId`, `location`), child add/delete restrictions, Float32 durability narrowing rejections, and raid `upd`-only blocks. Added toolbar helpers (copy, paste, tree FIR, full durability, in-text search, component jump selector) and live in-place tree reconstruction when `_tpl`, attachments, or deleted `upd` components change.
   - **Profile JSON Editor (`ProfileJsonEditorRules.cs`, `ProfileJsonEditorService.cs`, `ProfileJsonEditorPanel.cs`, `ProfileFieldCatalog.cs`)**: Removed `ExactLockedPaths`, `LockedIdentityChains`, `UndeletablePaths` (except root `$`), core identity guards, and uninstalled-template blocks. Added full inline expansion (`展开完整文本`), clipboard paste, and node export/import.
   - **Server JSON Editor (`ServerJsonStore.cs`, `ServerJsonEditorPanel.cs`)**: Opened all `.json` files across `SPT_Runtime` without directory whitelists (blocking only path traversal/reparse points and `.git`/`node_modules`), raised search/depth limits, and added `展开完整文本` (`serverJsonLoadFull`), copy, and paste.
   - **Additional Artificial Cap Removals**: Removed artificial caps across mail/spawner item counts, magazine & key usage limits, durability `> max` caps, dogtag length caps, wallet/overheal limits, flea direct-buy caps, backup size/template caps, raid duration/parameter range clamps, Scav editor item caps, and hideout level downgrade blocks.

2. **Right-Click Empty Grid Cell to Add Items (`GridCellItemCreator.cs`) / 空格子右键添加物品（支持目录与 JSON）**
   - Right-click any empty `(x, y)` cell in an inventory `GridView` (stash, backpack, rig, pockets, secure container, cases, sorting table, raid stash, or in-raid containers) to open a quick context popup:
     - **Add from Catalog**: Search by Chinese/English name, short name, or `_tpl`, filter by category, set quantity, orientation, FIR, default weapon preset, container-rule bypass, auto-fit, and custom component values.
     - **Add from JSON / Clipboard**: Supports single item `{ "_tpl": "..." }`, flat item trees `[...]`, weapon build wrappers `{ "Items": [...] }`, and Base64 strings, automatically generating fresh unique 24-hex `_id`s and remapping `parentId` links while persisting out-of-raid stash/equipment additions to the server and refreshing `GridView` immediately.

3. **Clear BTR Hostility (`BtrControl.cs`) / 清除 BTR 对玩家的敌对状态**
   - Added **清除 BTR 对玩家的敌对状态** (1-click reset of `AiActorControl.Forget`, `EnemiesController.EnemyInfos`, `BotsGroup.BotGroupHostility` enemy/warn records, neutral restoration, and `BotBTRService._betrayers`) and **BTR 始终友好（忽略误伤）** (persistent toggle intercepting `BtrController.HitFromPlayer`, `BtrController.ExplosionCheck`, `ShooterBTRLayersStrategy.ProcessBTRBeingHit`, and `BotBTRService.AddBetrayer`).

4. **Dynamic Scope Sticker Color & Speed (`ScopeSticker*.cs`) / 倍镜贴纸动态变色与速度调节**
   - Added dynamic color animation modes (**Off**, **Rainbow HSV cycle**, **Two-color gradient**, **Breathing pulse**), adjustable speed (**变色速度** slider + exact numeric input `0.01`–`50x`), solid silhouette mask vs original-art tinting toggle, and custom HEX colors persisted per optic and image variant.

5. **Fix Infinite Injectors (`InfiniteInjectors.cs`) / 修复无限注射器不起作用**
   - Hooked `PerkManager.GetItemResourceUsageMultiplier`, `ActiveHealthController.MedEffect.RegularUpdate`, `ActiveHealthController.MedEffect.Residue` (zeroing `_itemResourceUsageMultiplier` and restoring native `HpResource`), and `EFT.HealthSystem.MedEffectHelper.RemoveItem(Item)` so both single-use stimulators (`_medKit == null`) and multi-use stimulators retain full charges and remain in inventory after use.

6. **Fix Grenade Trajectory Preview (`GrenadeArc.cs`) / 修复手雷抛物线显示异常**
   - Moved trajectory sampling to per-frame `LateUpdate()` without throttling lag, aligned preview direction with `MainCamera` aim while reading exact native throw force (`BaseGrenadeHandsController.ThrowGrenadeInner`) without non-elite stamina `Random.insideUnitSphere` jitter or cocked-hand-bone skew, and rendered via a smooth world-space 3D `LineRenderer` plus impact marker.
