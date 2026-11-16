# 安装与故障排查 / Installation

适用 Windows 11 / SPT 5.0 BE / EFT 1.3.0.1.48558。辅助版本为 2.3；安装程序版本为 2.3.0.1。实验版的战局效果仍需按发布说明验证。

## 最简单的安装方法

1. 打开 [Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases)，在 **Assets（资源）** 中下载 `SPT-Assistant-2.3-Setup.exe`。
2. 双击 EXE，不需要先解压，也不需要安装 .NET SDK。安装器使用 Windows 自带的 .NET Framework 和 PowerShell，全部模组资源都在 EXE 内。
3. 选择 **SPT 目录里的 EscapeFromTarkov.exe**。可以拖入游戏文件夹或 EXE；误选 `SPT_Runtime` 等子目录时会尝试定位上一级的游戏。
4. 单人 / 房主用默认模式；连接朋友的服务器时选择“仅安装客户端”。Fika 仍需另行安装。
5. 关闭游戏和 SPT 服务端，点击 **开始安装**。安装程序不会强制结束它们。
6. 出现“安装完成”后，照常启动 SPT；在游戏里按 **右 Shift** 打开辅助菜单。

更新时重新运行新版安装程序并选择同一个游戏目录即可。安装程序不会要求普通用户输入路径命令或复制 DLL。无需全程管理员权限；只有目标文件夹拒绝写入时，才使用“管理员重试”。安装器未做数字签名，Windows 可能显示未知发布者提示；请先确认下载来源，不应关闭系统保护来运行来路不明的文件。

## 常见问题

| 提示 / 情况 | 怎么处理 |
| --- | --- |
| 没找到游戏 | 选择 SPT 目录内的 `EscapeFromTarkov.exe`，不要选官方在线版目录或下载压缩包。 |
| 版本不匹配 | 使用 EFT 48558 / SPT 5.0 BE 对应的游戏。不要强行覆盖其他版本。 |
| 缺少 SPT 必要文件 | 先把 SPT 安装完整，通过 SPT 启动器进入一次游戏，关闭后重试。 |
| 游戏或服务端仍在运行 | 自行关闭对应程序，再点“开始安装”。 |
| 安装文件损坏 | 从正式 Releases 重新下载完整 Setup EXE。不要下载 Source code 代替安装程序。 |
| 无写入权限 | 点击安装界面的“管理员重试”，或使用自己有写入权限的游戏目录。 |
| 文件被占用 / 无法写入 | 关闭游戏、服务端和正在打开相关文件的程序后重试，必要时查看日志。 |
| 安装失败 | 安装器会尝试恢复已经替换的文件。保留备份和日志；若提示部分恢复未完成，不要删除备份。 |

安装日志位于 `%LOCALAPPDATA%/SPTAssistant/Logs`，可在界面点击“查看日志”。备份位于游戏目录的 `mod-backups/assistant-2.3-*`，成功或失败后都不自动删除。设置、已有管理密钥、MC 钱包和角色数据不随安装包分发；完整安装在替换前备份角色存档。旧 MC 与 3-X 加载包的迁移规则见 [2.3 说明](release-2.3.md)。

## English quick start

Download **SPT-Assistant-2.3-Setup.exe** from release **Assets**, double-click it, select the SPT game's **EscapeFromTarkov.exe**, close the game/server and click **Install now**. Use the default mode for single-player/hosting, or client only when joining a friend. In game, press **Right Shift**. Setup is offline and uses Windows components; the .NET SDK is not needed.

Wrong version: use SPT 5.0 BE / EFT 48558. Missing dependencies: complete SPT installation and launch the matching game once. Access denied: use Retry as admin or select a writable game folder. Damaged package: download the EXE again. Existing files are backed up and rollback is attempted on failure; retain backups and the installation log if recovery needs attention.

## 命令行与源码构建（可选）

旧 ZIP 安装方式继续保留。解压 `SPT-Assistant-2.3-experimental.zip` 后，可执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT" -Language ch
```

联机成员增加 `-ClientOnly`。`-VerifyOnly` 仅检查包和游戏目录，不安装。客户端模式仍需对应 SPT 安装中的本地数据库，以生成名称与价格缓存；不安装服务端模组或创建管理密钥。

开发者使用 .NET 10 SDK 和匹配的游戏依赖构建模组；普通使用者不需要 SDK。完整打包会同时生成 ZIP 和自包含资源的图形安装 EXE：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-ExperimentalRelease.ps1 -GameRoot "D:\SPT" -OutputDirectory "D:\Assistant-build" -Dotnet dotnet
```

仅重建安装程序可使用 `scripts/Build-Setup.ps1 -PackageDirectory <已校验的安装包目录> -OutputDirectory <输出目录>`。GUI 源码在 `installer-ui`；使用 Windows 自带的 .NET Framework 编译器。安装包不携带游戏数据库、账户信息或 GitHub 凭据。

卸载前关闭游戏 / 服务端，仅移除 `BepInEx/plugins/LocalSpt5ESP`、`SPT_Runtime/user/mods/LocalGearMail`、`SPT_Runtime/user/mods/LocalRaidControls`；不要删 Fika 或 SPT。已整合的 MC 自定义物品依赖服务端模板，卸载前保留角色备份。移除模组不会撤销已经做过的角色编辑。
