黑墙脉冲已内置于 2.3 的“趣味功能”，安装包包含所需动作、贴图与音频，安装时会备份并停用旧独立插件。详见[黑墙说明](docs/blackwall-pulse.md)。
# SPT Assistant

新独立客户端 MOD 的开发入口：[通用热更新加载器](docs/universal-mod-host.md)。提供稳定接口、示例工程、按模块发布与失败回退；适用于本机 EFT 48558 / SPT 5.0。

[English](README.md) · [简体中文](README.zh-CN.md)

为 **SPT 单机塔科夫**开发的游戏内控制面板，使用 C# 与 Unity 构建。角色、战局、装备、任务与服务端设置，都可以在同一界面中管理。

**版本 2.3.1** · **当前适配：SPT 5.0 BE / EFT 1.3.0.1.48558** · Unity IL2CPP / .NET 10

> **不稳定，属于实验版本。** 本项目没有经过广泛测试，可能出现功能失效、崩溃、多人不同步或存档被意外修改。安装及更新前请备份存档和配置。编译通过、热加载成功，不代表每项功能都已在实战中验证。
>
> 后续开发以 SPT 最新版为适配目标，**不保证向后兼容**。每次发布都会注明具体适配的 SPT/EFT 版本；本次仅以以上版本为目标。Fika 联机支持同样处于实验阶段，尚未覆盖不同房主、成员和模组组合的广泛测试。

## 安装：双击即可

1. 在 [发布下载页](https://github.com/TRFCUTTFwww/SPT-Assistant/releases) 的 **Assets / 资源** 中，下载 **`SPT-Assistant-2.3.1-Setup.exe`**。不要下载 `Source code`，那是开发者用的源码。
2. 双击安装 EXE，选择你平时使用的 **SPT 游戏目录中的 `EscapeFromTarkov.exe`**。安装器也会尝试自动找到游戏位置。
3. 关闭游戏和 SPT 服务端，点击 **开始安装**。完成后照常打开 SPT，进入游戏按 **右 Shift** 打开辅助菜单。

单人游戏和联机房主保留默认选项；加入朋友的联机则选择“仅安装客户端”。安装程序自带全部模组文件，可离线安装；自动检查版本、校验文件并备份覆盖内容，保留已有设置和密钥。不需要输入命令、安装 SDK 或手动复制文件。

仅当你确认下载的是本项目正式发布附件时，才处理 Windows 对未知发布者的提示。安装器没有数字签名，不会绕过系统保护。若提示目录无写入权限，可使用安装界面的“管理员重试”。

需要技术细节或保留命令行安装方式时，请看[安装与故障排查](docs/INSTALL.md)。完整安装包仍保留 ZIP 形式，供手动部署使用。

## 2.3.1 更新

新增装甲列车时刻控制、行动前战局时长、BTR 站点标记、能量与水分编辑、独立门卡破解开关，以及 [SQLite 存档备份与跨角色还原](docs/profile-backup.md)。修正零车费仍需现金的原生校验，强化边界狙击保护与特殊 AI 的武器故障拦截。详见 [2.3.1 发布说明](docs/release-2.3.1.md)。

## 功能

新增[聊天与斜杠命令](docs/CHAT.md)：可配置的 T 键聊天框、命令历史、Tab 补全与单人/FIKA 本地输入命令（[支持范围](docs/CHAT.md#fika-命令范围--fika-command-scope)）。同一升级服务端的模组用户可在双方核验指纹后使用 TLS 1.3 加密聊天，提供会话级前向保密。请先阅读核验步骤和限制。旧多人分支未更新，main 不接受远程游戏操作命令。

新增默认关闭的[场景编辑模式](docs/world-editor.md)：通过准星右键菜单创建、移动、复制与修改本机控制的 AI 和散落物品。目前不稳定，仍需战局实测。

| 分类 | 功能介绍 |
| --- | --- |
| **主页面** | 查看当前 PMC 存档、资金、战绩与任务进度，通过快捷入口打开常用工具。 |
| **视觉与 HUD** | 人物、物品与载具 ESP、场景标记、信息悬浮窗和性能监测，可按需配置。 |
| **导航** | 定位任务目标与撤离点，查看距离并使用导航工具。 |
| **枪械与战斗** | 枪械编辑、瞄准辅助，以及弹药、弹道与装甲防护分析。 |
| **人物** | 生命与移动辅助，包括穿墙移动和可独立设置的双眼激光。 |
| **战局** | 管理战局时间与昼夜时钟、暂停计时、生成 AI，并操作战局物资。 |
| **物品与装备** | 物品搜索与邮件发放、装备管理，以及修理、耐久、钥匙和门禁卡设置。 |
| **任务与剧情** | 查看任务进度、重做任务、管理剧情分支，并跳过符合条件的等待时间。 |
| **角色数据** | 管理技能、成就、钱包，以及 PMC 和 Scav 角色设置。 |
| **服务端** | 调整保险、修理、藏身处、交易和战局配置，管理业力与跳蚤市场信誉。 |
| **界面定制** | 中英文即时切换，自定义快捷键、面板布局、不透明度与主题。 |
| **热更新** | 从面板重载受支持的客户端与服务端模块，保留宿主。 |

[版本发布](https://github.com/TRFCUTTFwww/SPT-Assistant/releases) · [更新记录](https://github.com/TRFCUTTFwww/SPT-Assistant/blob/main/CHANGELOG.md)

## 2.3：BTR、MC 整合与更新检测

载具 → BTR 控制：免费乘车、免费物品转移、秒转移和 10×100（1000 格）扩展转移仓均为独立开关；叫车指派至玩家最近的可达原生站点。秒转移在战局结算上传配送物品后立即发信；扩容用于新建的转移仓。设置 → 版本更新可检查 GitHub 正式 Releases，支持 HTTP、HTTPS、SOCKS5 或系统代理；不自动下载安装。

MC 像素剑、ProjectE、转换桌、图腾和相关 HUD 已整合进辅助 MOD 的客户端与服务端。MC 总显示、爱心、饱食度、口渴、经验和物品快捷栏可以分别开关。只停用旧的 MC 独立加载包，其他独立模块和辅助本体热重载保留。3-X 武器及其独立模块已移除。完整安装须关闭客户端和服务端；安装器备份旧代码与存档，保留 MC 配置、钱包、知识、终端数据和未完成操作日志。

### MC 原模组仓库与素材来源

| Project | Repository | Contribution |
| --- | --- | --- |
| AllTheModium | [AllTheModium](https://github.com/AllTheMods/AllTheModium) | Pixel swords / 像素剑 |
| ProjectE | [ProjectE](https://github.com/sinkillerj/ProjectE) | Transmutation, tools, artifacts / 转换桌、工具与饰品 |
| Modern Industrialization | [Modern Industrialization](https://github.com/AztechMC/Modern-Industrialization) | Quantum sword / 量子剑 |
| Applied Energistics 2 | [Applied Energistics 2](https://github.com/AppliedEnergistics/Applied-Energistics-2) | Wireless terminal artwork / 无线终端素材 |
| Thirst Mod | [Thirst Mod](https://github.com/ghen-git/Thirst-Mod) | Hydration HUD artwork / 口渴 HUD 素材 |

Minecraft 原版 UI、图腾素材归 Mojang/Microsoft 所有；本项目不将其声明为上述开源模组素材。固定版本、文件哈希及第三方许可随 MC Assets 的 provenance 和许可文件保留。见 [2.3 说明](docs/release-2.3.md)。
