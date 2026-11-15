# 终端切换手术包后战斗 HUD 消失

ProjectE 客户端 1.2.7，EFT 48558 / SPT 5.0 BE。

## 原因及现场证据

无线终端打开的是游戏原生 InventoryScreen。原收尾代码在玩家切换掉终端时直接调用 `InventoryScreen.Close()`。此方法只关闭视图，没有完整退出屏幕控制器及角色的库存界面状态。模块卸载时也存在相同的直接关闭调用。

2026-11-15 现场：角色仍存活并手持无线终端；屏幕控制器登记为 `Inventory`，`Player.IsInventoryOpened=true`，但 InventoryScreen 和 EftBattleUIScreen 的 GameObject 都已停用，鼠标锁定。原生 QuickAccessPanel 的 alpha 为 1，其父战斗界面被停用。自定义 MC HUD 也遵循 IsInventoryOpened 隐藏，因而血量、水分、经验、快捷栏一同消失。

使用角色原生 `GamePlayerOwner.CloseInventoryIfOpen()` 后，同一局读回 `BattleUI`、`IsInventoryOpened=false`、战斗界面和 QuickAccessPanel 恢复激活；游戏截图确认血量、水分和经验条重新出现。没有改血量、物品数量或跳过手术动画。

## 修复

- 终端在持物切换及退出模块时统一走 `InventoryScreenLifecycle.Close`，校验本地角色、库存界面标记及当前 Inventory 屏幕，再调用原生 owner 的完整退出流程；不再直接关闭库存视图。
- 每 0.25 秒核对屏幕生命周期，仅在存活的本地 PMC、Inventory 屏幕登记仍存在、背包及战斗视图均隐藏、鼠标锁定、没有转换桌模态界面、没有原生手部切换或医疗控制器的条件下，连续稳定 1.5 秒才尝试同一原生关闭流程。
- 世界或角色更换会重置稳定计时；正常可见背包、战斗界面、其他屏幕、菜单、手术过程以及排队换手均不触发。

## 验证及发布

```powershell
dotnet build projecte-client/Client.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558
dotnet run --project surgery-ui-tests/Tests.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558 -- projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-HotModule.ps1 -Id projecte -Name ProjectE -Assembly projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll -EntryType ProjectE.Client.ProjectModule -Version 1.2.7 -AssetsDirectory projecte-client/Assets -HostRoot D:/SPT-5.0.0-48558/BepInEx/plugins/UniversalModHost
```

构建零警告、零错误；524 项检查覆盖 512 种屏幕/角色/输入/原生操作组合、稳定计时、世界更换，以及编译后的终端 Tick/Shutdown 调用关系。61 个既有资源逐个核对哈希一致。当前客户端 PID 23072 已热加载 1.2.7。离线检查与现场恢复证据不替代完整的终端、手术、恢复持物和背包切换复测。
