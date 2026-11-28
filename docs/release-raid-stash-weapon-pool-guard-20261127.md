# Fix Stuck Hands When Equipping Weapons From Raid Stash (2026-11-27)

## Root Cause / 问题根因

### English

In Escape From Tarkov, raid startup (`ObjectsFactory` `PoolsCategory.Raid`) only loads 3D AssetBundles and creates ObjectPools for items present on the player at raid entry, spawned bots, and map loot. Items stored in the out-of-raid stash (`RaidStash`) do **not** have their weapon, attachment, magazine, or ammunition bundles loaded unless another actor or loot spawn in the same raid happened to reference the exact same `ResourceKey`s.

When a weapon (such as `AXMC .338 LM` or any weapon with unpooled attachments/ammo) was moved out of `RaidStash` and drawn into hands:

1. `PlayerBody.SlotView.CreateItemAsync` failed synchronously inside `ObjectsFactory.CreateItem` (`NullReferenceException` when `PoolsDictionary` / `_loadedBundles` lacked the bundle).
2. `Player.Proceed(Weapon, ...)` removed the active `HandsController` and entered `ProcessStatus.Scheduled`, then crashed inside `Player.SpawnInHands -> ObjectsFactory.CreateItem`, leaving `HandsController == null`, `ScheduledProcess == null`, spamming `NullReferenceException` in `Player.MouseLook`, and locking the player's hands.

### 中文说明

战局初始化时，游戏原生 `ObjectsFactory`（`PoolsCategory.Raid`）仅会为进图时角色身上的装备、本局 AI 携带的装备以及地图散落物加载 3D 模型 AssetBundle 与对象池；而通过 **战局仓库（`RaidStash`）** 读取的场外仓库枪械、配件、弹匣和弹药并未预载入当前战局对象池（只有当本局恰好有 AI 或地面物资刷出完全相同的枪械与配件资源时才不会报错，因此表现为**有时卡手、换某些枪必卡手**）。

当取出未加载资源池的枪械并切枪持握时：

1. 装备上身时 `PlayerBody.SlotView.CreateItemAsync` 调用 `ObjectsFactory.CreateItem` 因缺少资源池抛出 `NullReferenceException`；
2. 按键切枪调用 `Player.Proceed` 时，原生流程会先销毁旧的 `HandsController` 并置为 `Scheduled` 状态，随后在 `SpawnInHands -> ObjectsFactory.CreateItem` 中崩溃，导致 `HandsController == null`、`Player.MouseLook` 每帧空指针异常，产生严重卡手。

## Fix / 修复方案

- **`Player.Proceed` 全入口资源池门禁与自动重放（`esp-cover/RaidItemPoolGuard.cs`）**：拦截 `Weapon`、`ThrowWeap`（常规与快捷投掷）、`KnifeComponent`（常规与快捷近战）、`Meds`、`FoodDrink`、`RecorderItem` 及可用物品的全部 `Player.Proceed` 重载，递归检查整棵物品树（枪体、全部改装件、弹匣、膛内弹与弹匣内弹药的 `AllResources` / `Prefab` / `UsePrefab`）在 `ObjectsFactory` `Raid` 池中的就绪状态。若存在未加载资源，先在销毁当前手持控制器之前挂起切换并调用 `LoadBundlesAndCreatePools` 异步补齐资源池，加载完成后自动校验状态并无感重放切枪。
- **悬停/拖拽/入包提前预热与背枪模型补刷**：在战局内悬停、拖拽、右键点击物品、关闭战局仓库以及通过物品生成器/空格右键/AI 换枪添加物品时提前后台预热缺失模型池；加载完成后自动调用缺失模型的 `PlayerBody.SlotView.LoadItem()` 恢复角色背挂显示。
- **孤立空手状态快速自愈**：若检测到本地玩家已处于 `HandsController == null && ProcessStatus == Scheduled` 的孤立卡手态，1 秒内自动解除 `_nextProcessLocked` 并通过 `SetEmptyHands` -> `SetFirstAvailableItem` 恢复持枪（支持在战局中直接热更新生效）。
- **瞄准镜贴纸静态自定义颜色与菜单直接调色（`ScopeSticker`）**：新增 `固定颜色` 模式（与原图颜色、彩虹渐变、双色渐变、呼吸脉冲并列），提供 8 种快捷色板（红/绿/青/黄/橙/粉/紫/白）、R/G/B 实时拖动滑条、精确 RGB/HEX 输入以及纯色剪影/原图叠色切换；同时在 `镜头与画面 → 瞄准镜贴纸` 主设置页直接开放贴纸调色面板与「应用当前颜色到全部已存倍镜」按钮，无需逐个开镜即可配置与同步颜色。

