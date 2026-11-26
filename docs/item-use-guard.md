# 吃喝 / 医疗物品使用卡死修复

适用 EFT 1.3.0.1.48558 / SPT 5.0 BE，使用已有 Universal Mod Host。入口 `ItemUseGuard.ItemUseModule`，版本 1.0.2。

## 故障证据

2026-11-15 WIN11-GPU 的当前客户端先记录格瓦斯 `item_drink_kvas_container.bundle is not loaded`，随后 `CreateItemUsablePrefabAsync`、`ItemHandsController.Setup` 和 `MedsController.CreateController` 空引用。角色留在 `ProcessStatus=Scheduled`，`HandsController=null`，`ScheduledProcess=null`，之后 `MouseLook / VisualPass` 持续报错。

只恢复空手控制器后，`ProcessStatus` 正常，但库存 `_nextProcessLocked` 仍为 true，仍无法使用物品。因此修复同时覆盖资源准备、手部状态和失败后残留的库存锁。

## 行为

- 每秒发现本地角色随身携带的食物和医疗物品，每批最多 16 种模板，使用原生 `LoadBundlesAndCreatePools(Raid, Local, ..., JobYieldPriority.General)` 加载。明确包含 `Template.UsePrefab`；原生池负责资源生命周期。
- 在原生 `Player.Proceed(Meds, ...)` 和 `Player.Proceed(FoodDrink, ...)` 开始收起旧控制器前检查准备状态。未准备好的使用请求保留原参数和回调，资源就绪后只恢复一次。等待期间再点击、加载失败、30 秒超时、死亡、离开战局、换手或物品离开携带库存，都通过失败回调结束等待。
- 延后请求恢复时校验原战局、角色、物品所有权、手部控制器及空闲调度状态。旧战局的加载完成结果不会成为新战局的 ready 缓存。
- 仅当存活的本地角色持续 5 秒处于 Scheduled、没有手部控制器、没有排队流程、没有本模块加载或使用请求时，解除孤立调度状态与库存下一操作锁，再通过原生空手和物品选择流程恢复。正常医疗操作不快进；不修改医疗效果、物品余量或物品归属。
- 当前客户端如果已被旧故障卡住、但手部已由其他恢复措施重建，可显式请求一次库存锁恢复。此路径要求原生调度空闲，没有排队流程、装弹或验弹流程；不会在模块初始化或重载时重放旧请求。

## 构建与发布

在源码根目录执行，替换为自己的游戏根目录：

```powershell
dotnet build item-use-guard/Guard.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558
dotnet run --project item-use-tests/Tests.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558 -- item-use-guard/bin/Release/net10.0/Local.ItemUseGuard.HotModule.dll
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-HotModule.ps1 -Id item-use-guard -Assembly item-use-guard/bin/Release/net10.0/Local.ItemUseGuard.HotModule.dll -EntryType ItemUseGuard.ItemUseModule -Version 1.0.2 -HostRoot D:/SPT-5.0.0-48558/BepInEx/plugins/UniversalModHost
```

发布清单会持久保留，后续启动自动加载，不需要每局重新安装。无需更新或重启已有通用宿主。

## 状态和显式检查

`UniversalModHost/state/item-use-guard/data/status.json` 记录实际客户端 PID、UTC、加载/恢复/拒绝计数、资源状态、手部与库存锁。

更新数据目录内的 `check.request` 时间戳，触发一次实际使用模型创建检查：对每种随身消耗品调用原生 `CreateItemUsablePrefab`，确认有 `WeaponPrefab` 后归还对象池，将结果写入 `model-check.json`。不启动物品使用，不消耗物品；记录前后手部及库存锁供核对。旧检查请求不在热加载时重放。

更新 `recover-inventory.request` 时间戳，触发上述空闲状态下的一次性库存锁恢复。正常游戏不需要该文件；未来同类无手部故障的自动恢复同时处理库存锁。

## 已验证范围

- 模块构建 0 警告、0 错误；95 项离线检查通过，覆盖角色和战局边界、排队与正常过渡、延迟使用所有权、原生资源入口和异常库存锁边界。
- 当前客户端原孤立 Scheduled 状态已恢复；M4 控制器有效、原生调度空闲、库存 `_nextProcessLocked=false`、`Locked=false`，无残留 set/remove hands 回调。
- 11 类实际随身消耗品的原生使用模型创建与归还全部通过，包括格瓦斯、Surv12、Salewa、IFAK、夹板和多种注射剂；检查前后持枪物品及手部状态一致。
- 修复后运行日志不再持续产生此次丢失手部导致的 MouseLook / VisualPass 空引用。

2026-11-15 ????????????????????????????? 11 ?????????????????????????????????????????????????????
