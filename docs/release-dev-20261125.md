# 当前开发版：20261125-084012.6524897Z

本记录归档用户当前运行的 SPT Assistant 2.3.5 开发构建。构建号末尾为 UTC 标识 Z，对应北京时间 2026-11-25 16:40:12。源码基线为 e7d6cb0bdb843d0ca6f7f4685e79fa3e3df8de54，加上本次归档的后座、稳定瞄准及只读诊断改动。

## 保留的实现

- 射击后座与镜头后座挂钩在调用时检查当前本地持枪控制器；不缓存旧武器标识。
- 稳定瞄准仅在一次 ProcessEffectors 调用内临时调整呼吸与持枪晃动强度，并由 finalizer 分别恢复；不跨帧保留恢复闭包。
- recoil-state 提供当前持枪控制器、临时参数处理与收尾次数、错误计数和挂钩状态。
- 按用户要求撤回晚于此运行构建的实验改动，保留当前控制器判定。后续按动画玩家归属覆盖新旧控制器重叠帧的 CameraOwnedByLocal 方案不在此归档中。

## 运行模块核对

运行模块 SHA-256：57A598393DB749AECFD078F42A269EA4CDE8478B85A86F0D9F94589DB9C880A6。

恢复源码后以相同开发构建号复编，逐项比较运行模块与复编模块：14,601 个方法全部匹配，包括指令、局部变量、异常处理范围及方法标志；无新增或删除方法，字段和程序集引用无差异。复编文件与原运行文件的二进制哈希不同，此处确认的是上述编译实现一致。

## 验证

- EFT 48558 / .NET 10 Release 构建通过：0 错误，3 条既有警告（CS8600、CS8602、CA1416）。
- 完整 Probe：162 组检查通过，包括当前控制器隔离、镜头后座入口和单次稳定瞄准参数恢复。
- 当前运行模块保持原样；本次仅归档源码并推送 Gitea。

从源码仓库目录复编及检查：

    dotnet build esp-cover/Esp.csproj -c Release -m:1 -p:UseSharedCompilation=false -p:GameRoot=<游戏目录> -p:EftBuild=48558 -p:AssistantBuildNumber=20261125-084012.6524897Z
    dotnet run --project probe/Probe.csproj -c Release -p:GameRoot=<游戏目录>

运行 Probe 时设置 SPT_SOURCE_ROOT 为源码仓库绝对路径、SPT_GAME_ROOT 为游戏目录绝对路径。游戏程序集、编译模块、日志、存档及本机访问凭据不纳入 Git。
