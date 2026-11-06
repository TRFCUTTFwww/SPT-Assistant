# SPT Assistant

当前版本：**1.38.3**。当前目标环境：EFT 1.3.0.1.48558 / SPT 5.0 BE，IL2CPP，.NET 10；保留 1.2.0.0.47888 的条件编译路径。

这是当前模组的源码仓库，包含客户端功能、热加载宿主、两个服务端组件、验证工具和安装器源码。

## 版本与恢复

- [历史版本索引](history/README.md)：65 个可确认版本，按真实保留文件整理。
- 每个 `v版本` 标签对应本次导入的恢复记录；它们不是原始开发时的 Git 历史。
- 历史源码保留在 `history/v版本/`；缺少完整源码的版本会明确标注。无法确切对应版本的代码片段位于 `history/recovery/`。
- Releases 提供历史安装包或仅存模块及校验信息；同版本的不同本地构建单独标为 `archived-builds`。
- `main` 包含当前开发源码。旧标签的主要用途是访问对应版本与恢复档案；不要将缺失依赖的旧快照视为已验证可重建的项目。

## 源码目录

| 目录 | 内容 |
|---|---|
| `esp-cover` | 客户端功能及编队 AI |
| `esp-host` | IL2CPP 热加载宿主 |
| `gear-mail` | 服务端物品邮件与角色相关功能 |
| `raid-controls-server` | 战局与服务端设置 |
| `shared` | 共用规则与数据结构 |
| `probe` | 离线回归与本地游戏 API 检查 |
| `gear-mail-tests`、`raid-controls-tests` | 服务端规则检查 |
| `installer-1.33.0` | 最近打包版本的安装器源码 |

## 构建

需要 .NET 10 SDK，以及本机合法安装的目标游戏、SPT 和 BepInEx/IL2CPP 互操作程序集。仓库不包含这些依赖。

`Directory.Build.props` 默认使用 `D:/SPT-5.0.0-48558` 和 `EftBuild=48558`。可通过 `-p:GameRoot=游戏目录 -p:EftBuild=版本号` 覆盖，所有客户端、服务端及检查工程共用该路径。

将本仓库放在游戏目录中的 `_spt5_esp_fix` 文件夹；从**游戏根目录**运行：

```powershell
dotnet build _spt5_esp_fix/esp-cover/Esp.csproj -c Release
dotnet build _spt5_esp_fix/gear-mail/GearMail.csproj -c Release
dotnet build _spt5_esp_fix/raid-controls-server/RaidControls.csproj -c Release
dotnet run --project _spt5_esp_fix/probe/Probe.csproj -c Release
```

源码不在游戏目录下时，为 Probe 设置 `SPT_SOURCE_ROOT`（源码根目录）及 `SPT_GAME_ROOT`（游戏目录）。服务端测试使用 `SPT_GAME_ROOT` 读取对应版本的原生数据库。旧版客户端构建示例：

```powershell
dotnet build esp-cover/Esp.csproj -c Release '-p:GameRoot=D:/EFT SPT 5.0BE/EFT SPT 5.0BE' -p:EftBuild=47888
```

`dotnet` 必须指向 SDK，而非仅运行环境。本机已使用的 SDK 位于 `D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe`。

## 当前验证状态

1.38.3 改为修正原生踹门动画实际读取的结果，移除破门全场景扫描，缓存容器组件并减少上色维护和静默瞄准空闲扫描。80 组 Probe 通过，48558/47888 分别编译通过。1.38.2 的 ESP 与画质已获用户确认；本次破门交互与帧率改善仍待用户战局验收。详见 [1.38.3 说明](docs/release-1.38.3-zh.txt)。


1.38.2 修复缩放渲染下的世界标记投影及破门字段访问适配，支持分别构建 48558 和 47888；79 组 Probe、12 组服务端、2 组邮件检查通过。已在 48558 热加载并通过原生接口保存画质修复。对齐、上色和破门的游戏内效果待用户验收，详见 [1.38.2 说明](docs/release-1.38.2-zh.txt)。


1.38.1 已在 48558 客户端热加载并重新启动成功，钥匙消耗接口及原生活动路径均已挂接。LocalRaidControls 1.5.0 已在服务端重启后加载，36 种箱子模板应用嵌套规则。77 组 Probe 检查、12 组战局服务端检查及 2 组物品邮件检查通过；原生商人修理与原生物品箱模板已离线执行验证。新旧客户端编译均为 0 错误、2 条原有空引用警告，战局服务端编译无警告。新增玩法尚未完成战局实测。详见 [验证记录](docs/validation-1.38.1.json) 和 [使用说明](docs/release-1.38.1-zh.txt)。

- “战局 → 范围拾取”：原生库存事务转移范围内物品，可调整距离并绑定快捷键；默认不绑定快捷键。
- “物品 → 修理与耐久”：商人修复保留最大耐久、当前穿戴头甲不掉耐久、物品箱嵌套、钥匙不消耗次数。钥匙保留原来的剩余次数显示。商人和箱子规则需要 LocalRaidControls 1.5.0；箱子规则在服务端启动时应用，修改后须重启服务端并重新登录游戏。
- “战局 → 地图模式”：1.3 使用原生活动及实验室资源；仅 1.2 使用旧移植路径。原生活动开关在服务端和客户端重启后生效；灯光在原生实验室中实时应用。

保留用户选定的原有 IMGUI 菜单、图标与滚动控件。1.37.4 的普通未开镜上色已由用户实测；本次适配不代表所有旧功能均已在 48558 实战验证。

活动开关位于“战局 → 活动控制”，保存后需退出游戏、重启服务端，再重新启动游戏。1.32.8 之后为实际 Git 开发版本；此前 65 个恢复档案的来源说明仍保留。

运行模块与安装包在 Releases；游戏本体、日志、存档和运行时文件不属于版本库。
