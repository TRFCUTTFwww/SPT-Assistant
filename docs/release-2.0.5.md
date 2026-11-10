# SPT Assistant 2.0.5 — Unstable / 不稳定

## English

> **UNSTABLE. This project has not undergone broad testing.** Bugs, crashes, Fika desynchronization and unexpected profile changes are possible. Back up your profiles and settings. Passing builds and offline checks does not mean all in-raid behavior has been verified.

**Current target:** SPT 5.0 BE / EFT 1.3.0.1.48558, Windows x64, Unity IL2CPP / .NET 10. Development follows the latest SPT version. **Backward compatibility is not guaranteed.** Other builds are not certified by this release. Fika compatibility is experimental and varies with the host, client and mod combination.

This first GitHub prerelease includes the current client, mail/administration and raid-settings components. Recent changes include Fika teammate recognition with green ESP and optional aim exclusion, item details, optic vehicle markers, crosshair AI effects, local teleport to another player, and a fix for backpack-restricted extraction checks. Train, lever and secret-exit activation flows remain native. See the repository history for earlier changes.

Download **SPT-Assistant-2.0.5-experimental.zip**, extract it, close the target client/server, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language en
```

Fika members add `-ClientOnly` and connect to the host without starting a local server. This package does not install Fika. Start the SPT server and client after full installation; press **Right Shift** to open the panel. See [installation details](https://github.com/TRFCUTTFwww/SPT-Assistant/blob/main/docs/INSTALL.md). SHA256SUMS.txt provides archive verification. Source archives are for building, not direct installation.

Validation passed: client, mail/admin and raid-settings builds; 121 client Probe groups, 23 mail/admin groups, 12 raid-settings groups and 5 hot-reload groups. Installation tests cover read-only preflight, client-only and full installation, local cache/key generation, preservation of existing keys/configuration, backups, and rejection of a tampered package. The target installation also passed read-only preflight. The client full build retains two existing nullable warnings. Client core 2.0.5 was hot-loaded with its extraction hooks installed. Actual ventilation-exit extraction, optic visuals, Fika effect synchronization and player teleport behavior still require in-game confirmation. No claim of broad multiplayer stability is made.

## 简体中文

> **不稳定，没有经过广泛测试。** 可能出现功能失效、崩溃、Fika 不同步或存档被意外修改。请先备份存档与设置。编译和离线检查通过，不代表所有战局行为均已实测。

**当前适配：**SPT 5.0 BE / EFT 1.3.0.1.48558，Windows x64，Unity IL2CPP / .NET 10。后续以 SPT 最新版为适配目标，**不保证向后兼容**。本次发布没有认证其他游戏版本。Fika 支持为实验性质，兼容性受房主、成员及模组组合影响。

首次 GitHub 预发布包含当前客户端、邮件/管理服务端组件与战局设置组件。近期改动包括 Fika 队友识别、绿色 ESP 和可选瞄准排除、物品详细信息、倍率镜载具标记、准星 AI 状态、本地玩家传送至其他玩家附近，以及背包限制撤离点的条件检查修复。列车、拉闸和暗号撤离点保留原生启动流程；早期变更见提交历史。

下载 **SPT-Assistant-2.0.5-experimental.zip**，解压后关闭目标客户端和服务端，再执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language ch
```

Fika 成员增加 `-ClientOnly`，连接房主，不启动本地服务端。本包不安装 Fika。完整安装后先启动 SPT 服务端再启动客户端，按 **右 Shift** 打开面板。详细步骤见[安装说明](https://github.com/TRFCUTTFwww/SPT-Assistant/blob/main/docs/INSTALL.md)。压缩包校验值见 SHA256SUMS.txt；源码压缩包需要自行构建，不能直接安装。

验证通过：客户端、邮件/管理组件及战局组件构建；121 组客户端 Probe、23 组邮件/管理检查、12 组战局检查和 5 组热加载检查。安装测试覆盖只读预检、仅客户端及完整安装、本地缓存/密钥生成、保留原有密钥和配置、文件备份，以及拒绝被篡改的安装包；实际目标目录只读预检也通过。客户端完整构建保留两处原有可空警告。客户端 2.0.5 已热加载，撤离条件挂钩加载成功。通风管道实际撤离、倍率镜画面、Fika 效果同步和玩家传送仍需游戏内确认，不保证多人稳定性。
