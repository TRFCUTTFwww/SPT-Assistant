# SPT Assistant 2.0.6 — Unstable / 不稳定

## 简体中文

**适配版本：SPT 5.0 BE / EFT 1.3.0.1.48558，Windows x64，Unity IL2CPP / .NET 10。** 后续以 SPT 最新版为适配目标，不保证向后兼容。没有经过广泛测试；Fika 同步及不同模组组合仍需测试。安装前备份存档和配置。

- 修复“激光穿墙”开启后光束消失：改用写入 RGBA 的光束材质、显式颜色网格和原生光束回退，保留人物遮挡，命中和伤害逻辑不变。
- 新增“物品 → 修理与耐久 → 无限注射器”，默认关闭，仅保护本地玩家仍有使用量的针剂，保留注射动画、效果与副作用。
- 新增“杂项 → 供大于求”，描述为“商人购买无上限”，默认关闭。启用后解除商品库存和个人限购；价格、忠诚度及任务解锁条件照常，关闭后恢复原规则。重新打开商人页面刷新。

版本号、界面和打包脚本已同步为 2.0.6。本次源码提交不代表 GitHub 安装附件已经发布；已发布的安装包见 [Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases)，请核对附件版本。

安装包解压后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language ch
```

Fika 成员添加 `-ClientOnly`，连接房主，不启动本地服务端。本包不包含 Fika。先启动服务端，再通过 SPT 启动器启动游戏，按右 Shift 打开面板。完整说明见 [安装指南](INSTALL.md)。

EFT 48558 客户端及管理组件编译、121 组客户端 Probe、24 组邮件/管理回归通过。相同功能代码在升版本前已成功热加载；2.0.6 版本号更新未执行新的实机验收。激光的 GPU 画面、针剂实际使用及 Fika 同步仍需战局确认。2.0.6 安装包流程没有重新执行完整安装回归。

## English

**Target: SPT 5.0 BE / EFT 1.3.0.1.48558, Windows x64, Unity IL2CPP / .NET 10.** Development follows the latest SPT version; backward compatibility is not guaranteed. Unstable and not broadly tested. Fika synchronization and mod combinations need further testing. Back up profiles and configuration before installing.

- Fix beams disappearing with laser wall penetration: use an RGBA beam material, explicitly colored geometry and native scene fallback, while preserving character occlusion. Hit selection and damage are unchanged.
- Add opt-in **Items → Repair & durability → Infinite injectors** for non-exhausted injectors carried by the local player. Normal animations, effects and side effects remain.
- Add opt-in **Miscellaneous → Oversupply**, described as **Unlimited trader purchases**. Remove stock and personal purchase limits while retaining prices, loyalty and quest requirements. Disabling restores native rules. Reopen the trader screen to refresh.

The source, UI and packaging scripts use version 2.0.6. A source commit does not imply an installation asset has been published. Check the version of available assets on [Releases](https://github.com/TRFCUTTFwww/SPT-Assistant/releases).

Extract a matching installation package and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language en
```

Fika members add `-ClientOnly` and connect to the host without starting a local server. Fika is not bundled. Start the server, launch the game through SPT, and press Right Shift. See the [installation guide](INSTALL.md).

EFT 48558 client/admin builds, 121 client Probe groups and 24 mail/admin regression groups passed. The same feature code was hot-loaded before the version bump; no new in-game acceptance was performed for the 2.0.6 version stamp. Laser GPU output, actual injector use and Fika synchronization still need in-raid confirmation. Full installation regression has not been rerun for the 2.0.6 package.
