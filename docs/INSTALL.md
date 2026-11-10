# Installation / 安装说明

## English

**Current target:** SPT 5.0 BE, EFT 1.3.0.1.48558, Windows x64, Unity IL2CPP / .NET 10. Development follows the latest SPT version; backward compatibility is not guaranteed. This is an unstable experimental release without broad testing. Back up profiles and settings first.

Use the `SPT-Assistant-2.0.5-experimental.zip` release asset. Extract it outside the game directory, close the target client and server, and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language en
```

Add `-ClientOnly` for a Fika member who connects to another machine. This installs the client host/core and generates local item caches; it does not install server mods or create an administration key. Fika itself must already be installed separately. Do not start a local SPT server on a member's machine. For authorized server administration, the client reads the host-provided key from its own `SPT_Runtime/user/mods/LocalGearMail/api-key.txt`; the key grants administrative access and is not required for local ESP. Keep it out of shared archives.

The full installation uses these paths relative to the game root:

| File | Purpose |
| --- | --- |
| `BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll` | Client hot-reload host, version 1.5.0 |
| `BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module` | Assistant client core, version 2.0.5 |
| `BepInEx/plugins/LocalSpt5ESP/esp5-names.json` | Generated local item names |
| `BepInEx/plugins/LocalSpt5ESP/esp5-prices.json` | Generated local price snapshot |
| `SPT_Runtime/user/mods/LocalGearMail/LocalGearMail.dll` | Mail and administration API host |
| `SPT_Runtime/user/mods/LocalGearMail/LocalGearAdmin.module` | Reloadable profile/story/stash administration |
| `SPT_Runtime/user/mods/LocalRaidControls/LocalRaidControls.dll` | Server and raid settings |
| `SPT_Runtime/user/mods/LocalGearMail/api-key.txt` | Locally generated administration secret; existing key preserved |

Start the server first, then launch the client through SPT. Press **Right Shift** for the panel. BepInEx lists the host as 1.5.0; the panel/core version is 2.0.5. On first installation or a host/server DLL update, restart the relevant processes. Later core-only updates can use the existing hot-reload facility; do not replace a host DLL while the game is running. For this prerelease, closing both processes and rerunning the installer is the recommended update path.

The installer verifies the package's file checksums before installation and backs up replaced files under `mod-backups/assistant-2.0.5-*`. Existing configuration and profiles are not supplied by the archive. Item caches come from your installed SPT database; no game database or personal data is redistributed.

To remove the mod, close the client/server and remove only `BepInEx/plugins/LocalSpt5ESP`, `SPT_Runtime/user/mods/LocalGearMail`, and `SPT_Runtime/user/mods/LocalRaidControls`. Preserve any backups you need. Removing the mod does not undo profile edits; restore your own profile backup to reverse those edits. Leave Fika and SPT installed.

### Build from source

Install the .NET 10 **SDK** and matching SPT. Launch SPT once to generate BepInEx interop assemblies. From a checkout, build a package using:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-ExperimentalRelease.ps1 -GameRoot "D:\SPT-5.0.0-48558" -OutputDirectory "D:\SPT-Assistant-build" -Dotnet dotnet
```

The output directory must be new or empty. Use the resulting ZIP with the installation command above. Native game/BepInEx/SPT dependencies are resolved from `GameRoot` and are not included in this repository or package. The package contains only this project's binaries, installation scripts, license and documentation.

## 简体中文

**当前适配：**SPT 5.0 BE、EFT 1.3.0.1.48558、Windows x64、Unity IL2CPP / .NET 10。后续以 SPT 最新版为适配目标，不保证向后兼容。当前版本不稳定，没有经过广泛测试，请先备份存档和设置。

下载发布附件 `SPT-Assistant-2.0.5-experimental.zip`，解压到游戏目录外，关闭目标目录的客户端和服务端，再执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language ch
```

Fika 成员连接其他电脑的房主时，增加 `-ClientOnly`：只安装客户端宿主、功能模块及本地物品缓存，不安装服务端模组，也不生成管理密钥。Fika 需自行提前安装。成员电脑不要启动本地 SPT 服务端。需要房主授权的服务端管理时，客户端从自己目录下的 `SPT_Runtime/user/mods/LocalGearMail/api-key.txt` 读取房主提供的密钥；本地 ESP 不需要该密钥。该密钥具有管理权限，请勿打包公开。

完整安装的文件位置见上表。先启动服务端，再通过 SPT 启动器启动游戏，按 **右 Shift** 打开面板。BepInEx 显示宿主版本 1.5.0，面板与功能模块版本为 2.0.5。首次安装、宿主或服务端 DLL 更新需要重启对应进程；后续仅功能模块更新可使用现有热加载。本实验版建议关闭两端后重新运行安装脚本更新。

脚本安装前核对包内文件校验值，覆盖的文件备份到 `mod-backups/assistant-2.0.5-*`，保留已有设置及 API 密钥。压缩包不含个人存档、配置、密钥或游戏数据库；物品名称和价格从安装者自己的 SPT 数据库生成。

卸载时关闭两端，只移除 `BepInEx/plugins/LocalSpt5ESP`、`SPT_Runtime/user/mods/LocalGearMail`、`SPT_Runtime/user/mods/LocalRaidControls`，保留需要的备份。移除模组不会自动恢复已经修改的存档；需要撤销时使用自己的存档备份。保留 Fika 和 SPT。

源码构建使用 .NET 10 **SDK**，不是仅运行环境；准备对应 SPT 并启动一次以生成互操作程序集，在源码根目录运行上方构建命令。输出目录必须是新建或空目录，生成的 ZIP 使用同样的安装方式。构建依赖从 `GameRoot` 读取，不随源码或发布包分发。
