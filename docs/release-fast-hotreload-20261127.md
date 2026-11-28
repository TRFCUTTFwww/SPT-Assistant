# Sub-second Client Hot-Reload & Persistent Harmony Hook Router (2026-11-27)

## Summary / 概述

### English

Client hot-reload (`Local.Spt5.ESP.Core.module`) previously took **38–45 seconds** per swap (`485` lifecycle steps, `peakStepMs: 2390 ms`), freezing the game behind a modal pause screen. Profiling `assistant.log` and `hotreload-status.json` isolated three bottlenecks:

1. **Native IL2CPP detour thrashing (~80% / ~35 s)**: Every hot-reload called `Harmony.Unpatch` on ~180 IL2CPP methods during unload and `Harmony.Patch` again during load, triggering **281 `DobbyDetour` + `Il2CppMethodPatcher` native codegen cycles** (`125–233 ms` each, growing across generations).
2. **1-step-per-frame serialization (~15% / ~8 s)**: `ReloadSequence` executed strictly one step per Unity `Update()` across `485` granular steps (`485 frames` @ 60 FPS ≈ `8.1 s`) plus modal `painted < 2` frame barriers.
3. **Synchronous I/O & catalog reparsing (~2–3 s)**: `Finish()` kept the game paused while writing two 8.3 MB backup binaries (`last-good.module` + `previous-good.module`), `ConfigurationManager.BuildSettingList()` ran twice via reflection, and `items.json` (~35 MB) plus embedded localization/item catalogs were re-parsed from scratch on every reload.

Key optimizations implemented:

- **Persistent Harmony Hook Router (`esp-cover/PersistentHarmonyRouter.cs`)**: Emits a process-wide dynamic assembly (`Local.Spt5.HookBridge`) stored in `AppDomain.CurrentDomain` using pure BCL/`0Harmony` state so all collectible `PayloadContext` generations share the same native IL2CPP trampolines. Unchanged hook signatures rebind via volatile static delegate fields and lightweight `DynamicMethod` adapters (supporting `ref` parameters, generalized `object`/`out object` `__state` for private structs/records/`Nullable<T>`, and finalizers) with **zero `DobbyDetour` calls** on reload (`< 0.5 ms` total unload + rebind). `PatchSwapSafe` swaps `PlayerVisualGuard` atomically with zero unguarded window.
- **Time-Budgeted Silent Fast-Swap (`esp-host/HotHost.cs` v`1.7.0`, `esp-cover/RuntimeModule.cs`)**: Adds `FileSystemWatcher` instant wakeup for `Local.Spt5.ESP.Core.module` and `reload.request`, batches `UnloadSteps` / `InitializeSteps` from `485` down to `~12` steps (also accelerating legacy `1.6.3` hosts without requiring a restart), executes multiple steps within a `25 ms` per-frame budget, suppresses the black-screen modal unless reload exceeds `150 ms` or hits `SafetyBlocked`, and calls `EndPause()` immediately while writing backup modules asynchronously in the background.
- **Cross-Generation Read-Only Caches & Atomic Publisher (`ItemCatalog.cs`, `LootDisplay.cs`, `I18n.cs`, `scripts/Deploy-HotReload.ps1`)**: Caches parsed `items.json`, `esp5-names.json`, `esp5-prices.json`, `item-name-fallbacks.json` and `en*.json` dictionaries across generations keyed by file timestamp / SHA-256, skips `ConfigurationManager.BuildSettingList()` when the window is closed, and publishes `Core.module` atomically via Win32 `MoveFileExW`.

### 中文说明

此前客户端 `Local.Spt5.ESP.Core.module` 热更新每次耗时约 **38–45 秒**（共 `485` 个步骤，单步峰值 `2390 ms`），并伴随长时间黑屏暂停。通过对 `assistant.log` 与 `hotreload-status.json` 的采样分析，定位并消除了三大核心瓶颈：

1. **持久化 Harmony Hook 路由桥接（消除 ~35 秒原生 Detour 开销）**：新增进程级动态程序集桥接器 `PersistentHarmonyRouter`（`Local.Spt5.HookBridge`），将 IL2CPP 原生跳板保留在默认上下文中，跨 `PayloadContext` 热更新时仅通过 `volatile` 静态委托字段与 `DynamicMethod` 适配器切换托管逻辑（完整支持 `ref` 参数、私有结构体/类/`Nullable<T>` 的 `__state` 传递以及 `Finalizer`），卸载与重绑合计耗时 **`< 1 ms`**、**0 次重复 `DobbyDetour`**；`PlayerVisualGuard` 采用 `PatchSwapSafe` 单指针原子切换，全程无空窗。
2. **帧时间预算多步执行与无感恢复（消除 ~8 秒帧排队与 ~2 秒备份阻塞）**：宿主升级至 `1.7.0`，支持 `FileSystemWatcher` 零延迟监听、每帧 `25 ms` 时间片连续推进步骤、在 `150 ms` 内静默完成切换（不闪黑屏遮罩；若超时或触发安全拦截仍立即展示回滚界面），并在切换完成瞬间恢复游戏（`last-good.module` / `previous-good.module` 改为后台异步落盘）。同时将模块步骤从 `485` 步精简合并至约 `12` 步，兼容旧版 `1.6.3` 宿主。
3. **跨代只读数据缓存与原子发布脚本**：对 `items.json`（约 35 MB）、`esp5-names.json`、`esp5-prices.json`、`item-name-fallbacks.json` 及英文语言包建立基于时间戳/哈希的 `AppDomain` 跨代缓存，跳过配置窗口未打开时的重复反射扫描，并提供 `scripts/Deploy-HotReload.ps1` 一键原子发布脚本与毫秒级耗时提示（`热更新完成 · XX ms`）。

## Verification / 验证结果

- `PersistentHarmonyRouterChecks` (`gear-mail-tests --hook-router`): Passed (`unload = 0.255 ms`, `rebind = 0.224 ms`, 0 detours on reload, swap-safe singleton guard, struct/nullable/private-class `__state`, `ref` args, finalizers).
- `host-tests` (`HostTests.dll` + `Fixture.dll`): Passed both standard 1-step-per-frame safety mode (`161.0 ms`) and `FastMode` (`16.4 ms`) plus failure pause/rollback guards.
- `ai-combat-tests`: Passed all 62 combat regression checks.
- `Probe`: Passed full client/native signature, localization, scope sticker, recoil, optic zoom, night vision and raid regression suite.
