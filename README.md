# SPT Assistant

当前版本：**1.37.4**。目标环境：EFT 1.2.0.0.47888 / SPT 5.0 BE，IL2CPP，.NET 10。

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

当前工程保留了本机原有引用路径：`D:/EFT SPT 5.0BE/EFT SPT 5.0BE`。在其他目录构建时，先调整 `esp-cover/Esp.csproj` 和 `esp-host/Host.csproj` 中的引用路径。

将本仓库放在游戏目录中的 `_spt5_esp_fix` 文件夹；从**游戏根目录**运行：

```powershell
dotnet build _spt5_esp_fix/esp-cover/Esp.csproj -c Release
dotnet build _spt5_esp_fix/gear-mail/GearMail.csproj -c Release
dotnet build _spt5_esp_fix/raid-controls-server/RaidControls.csproj -c Release
dotnet run --project _spt5_esp_fix/probe/Probe.csproj -c Release
```

`dotnet` 必须指向 SDK，而非仅运行环境。本机已使用的 SDK 位于 `D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe`。

## 当前验证状态

1.37.4 修复普通人物上色：改为共享人物网格和骨骼的原生场景渲染层，绕开主相机绘制命令在当前渲染管线不显示的问题；用户已在当前战局确认未开镜上色恢复。保留原有 IMGUI 菜单、图标与滚动控件，加入玩具枪命中本局 AI 尸体复活、尸体传送、人物模型旋转、枪械击杀特效、投掷物轨迹、镜内上色和遮挡区分。Release 编译成功（0 错误、2 条空引用警告），69 组离线检查通过，客户端热加载成功。界面由用户自行检查；普通上色已实测，其余新增玩法与渲染细节尚未完成实战验证。详见 [验证记录](docs/validation-1.37.4.json) 和 [使用说明](docs/release-1.37.2-zh.txt)。

活动开关位于“战局 → 活动控制”，保存后需退出游戏、重启服务端，再重新启动游戏。1.32.8 之后为实际 Git 开发版本；此前 65 个恢复档案的来源说明仍保留。

运行模块与安装包在 Releases；游戏本体、日志、存档和运行时文件不属于版本库。
