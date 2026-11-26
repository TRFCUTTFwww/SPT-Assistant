# 通用热更新加载器 1.0.0

这不是任意 BepInEx DLL 的热插拔工具。旧插件需要先拆成常驻依赖和符合契约的逻辑模块。原生 DLL、IL2CPP 类型注册、宿主和 SDK 自身更新仍需要重启游戏。服务端 MOD 不由这个客户端宿主管理。

## 安装

关闭客户端，从源码仓库执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Install-UniversalHost.ps1 -GameRoot 'D:/SPT-5.0.0-48558' -Dotnet 'D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe'
```

安装脚本构建宿主、SDK、示例与测试夹具，运行生命周期检查，然后安装并核对哈希；已有宿主文件备份到 `mod-backups`，已有模块目录与数据保留。首次会装入一个仅记录启动时间、没有游戏功能的 `example` 模块，便于确认发现和加载流程。

```text
BepInEx/plugins/UniversalModHost/
  Local.UniversalModHost.dll
  Local.ModHost.Contracts.dll
  modules/
    example/
      module.json
      releases/<revision>/Example.HotModule.module
  state/
    example/
      status.json
      last-good.json
      data/example-started.txt
  host-status.json
```

宿主仅注册一个常驻 Unity 驱动。每秒检查模块目录，发布清单至少稳定 0.5 秒才开始读取；SHA-256 和文件读取在后台完成，模块创建、初始化、更新、绘制与清理在 Unity 主线程运行。

## 新 MOD 的开发与发布

复制 `universal-example` 工程，引用 `universal-sdk/Contracts.csproj`，按需要加入本机 Unity/EFT/Harmony 引用。入口实现 `Local.ModHost.IHotModule`，不要添加 `BepInPlugin` 或继承 `MonoBehaviour`。需要逐帧逻辑时使用 `Tick` / `LateTick` / `Draw`。

`ApiVersion` 必须为 1，`Version` 必须与发布参数一致。构造函数及这两个属性必须无副作用。`CanReload` 在操作进行中返回 false，宿主会让旧模块继续运行，等待该模块允许后再切换；其他模块仍能独立更新。

```powershell
dotnet build universal-example/Example.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-HotModule.ps1 -Id example -Assembly universal-example/bin/Release/net10.0/Example.HotModule.dll -EntryType Example.ExampleModule -Version 1.0.0
```

脚本会创建新的、不可覆盖的版本目录，复制代码与资源、计算各文件哈希，最后原子替换 `module.json`。以后更新模块时只需再次构建和发布。`-HostRoot` 可指定另一套宿主目录；`-AssetsDirectory` 把资源放入该版本的 `Assets` 子目录；`-PrivateLibraries` 接收私有托管依赖路径并发布为 `.module`，避免被 BepInEx 当成普通插件扫描。

不要发布 Unity、BepInEx、Harmony、EFT 或 SDK 的副本。不要在原版本目录内直接替换文件：旧版代码回退需要它原来的资源。版本目录按需在退出游戏后人工归档；宿主不自动删除它们。

## 生命周期与资源

`ModuleContext.ModuleDirectory` 指向本次版本的只读资源目录，`DataDirectory` 是跨版本保留的模块数据目录。JSON 配置、用户设置等放入 `DataDirectory`；回退不自动撤销用户数据迁移或游戏世界中的操作。

每代都有唯一的 `PatchId`。Harmony 补丁必须使用 `new Harmony(context.PatchId)` 创建，宿主会按该 owner 清理。取得资源后立即调用 `context.OnShutdown(...)` 登记清理：事件取消订阅、Unity 对象销毁、材质/纹理/音源释放、临时状态恢复等。`Shutdown` 即使遇到部分初始化失败也可能被调用，应能处理这种情况。

清理时宿主先调用 `Shutdown`，再取消 `context.Stopping`，并逆序执行所有登记的清理，某一项失败也继续其余项。原生回调应检查 `context.IsActive`；后台工作应使用停止令牌，不能在取消后继续修改 Unity 状态。初始化、登记清理和状态修改均使用主线程；后台任务退出也应由模块妥善安排。

宿主能清理登记的资源和自己的 Harmony owner，不能自动推断插件持有的所有外部资源。模块必须成对关闭 GUI Group/Layout、平衡输入/光标/时间倍率接管；宿主会恢复常见 GUI 属性，但无法修复未闭合的 GUI 栈。不要注册每代新 IL2CPP 类型。DLL 从内存加载，磁盘文件不被锁定；由于原生跳板可能引用元数据，旧代托管程序集保留到游戏退出，不宣称无限次热更新零内存增长。

## 更新失败、禁用与诊断

- 清单、哈希、API、入口类型和模块版本不正确：旧模块继续运行。
- 新版初始化失败：清理候选，恢复上次成功的代码与资源目录。首次启动失败时，也会尝试 `state/<id>/last-good.json` 指向的旧版。
- `Tick`、`LateTick`、`Draw` 或 `CanReload` 抛错：停止并清理该模块，其他模块继续运行。发布修复版或手动重载后重试。
- 清理失败：该模块标记 `cleanup-failed` / `restartRequired=true`，本次游戏内不再启动替代实例，避免残留原生状态与新版本叠加。
- `module.json` 中 `enabled=false`，或移除该清单：在模块可重载时停止。发布脚本 `-Disabled` 可发布默认关闭的版本。
- **Ctrl+F9**：重新检查所有模块。修改某模块目录的 `reload.request` 时间戳可以只重载该模块。显式请求也遵守稳定窗口和 `CanReload`。

`BepInEx/config/local.universal.modhost.cfg` 的 `Loading/Automatic` 控制已发现模块的自动更新；关闭时仍会发现新模块，Ctrl+F9 与单模块请求仍有效。宿主清单只允许 2048 个文件、单文件 256 MiB、总计 512 MiB，以免意外加载超大包。单模块状态在 `state/<id>/status.json`；汇总在 `host-status.json`，包含进程 ID 和 UTC 更新时间。客户端关闭后旧的 ready 状态不能作为当前运行证据。

## 验证范围

独立进程测试使用真实的程序集加载上下文、文件发布与私有依赖，覆盖多模块隔离、反复重载、忙碌等待、代码/资源哈希、非法入口、部分初始化回退、回调故障、清理故障、取消令牌、移除/禁用、重启恢复及手动重载。宿主针对本机实际 BepInEx、Harmony 和 EFT interop 编译。

这不替代游戏内验证。首次启动应检查 `Universal Mod Host 1.0.0 ready`、`example ... loaded` 和 `state/example/data/example-started.txt`。
