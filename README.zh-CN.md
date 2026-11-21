# SPT Assistant

[English](README.md) · [简体中文](README.zh-CN.md)

为 **SPT 单机塔科夫**打造的游戏内控制面板、场景编辑器与客户端/服务端扩展工具集，基于 C#、Unity IMGUI/Harmony 与 .NET 10 构建。在一个统一界面中管理角色、战局、AI、物品、任务、剧情与服务端规则。

**当前源码版本：** `2.3.5`（开发中） · **最新打包发布：** `v2.3.1` · **目标环境：** `SPT 5.0 BE / EFT 1.3.0.1.48558`（Windows x64，Unity IL2CPP / .NET 10）

> **不稳定 — 实验版本**
>
> 本项目尚未经过广泛测试，可能出现功能失效、崩溃、联机不同步或存档被意外修改。**安装或更新前请务必备份存档与配置文件。** 离线编译通过或热加载成功，不代表所有功能均已在实战局中完成验证。
>
> 开发始终以最新 SPT 版本为目标，**不保证向后兼容**。Fika 联机支持同样处于实验阶段，尚未覆盖不同房主、成员和模组组合的广泛测试。

---

## 快速安装（推荐）

1. 前往 [Releases 发布页](https://github.com/TRFCUTTFwww/SPT-Assistant/releases)，在 **Assets / 资源** 中下载 **`SPT-Assistant-2.3.1-Setup.exe`**（不要下载 `Source code`，那是开发者源码）。
2. 双击安装程序，选择你平时使用的 **SPT 游戏目录中的 `EscapeFromTarkov.exe`**。
3. 关闭游戏客户端与 SPT 服务端，点击 **开始安装**。
4. 照常启动 SPT 服务端与游戏，进入游戏后按 **右 Shift** 打开辅助面板。

- **单人游戏 / Fika 房主：** 保持默认完整安装模式。
- **Fika 联机成员（加入朋友服务端）：** 选择 **仅安装客户端**。
- Setup EXE 为离线自包含安装包：自动校验目标版本与文件哈希，将覆盖文件备份至 `mod-backups/`，并保留现有配置与管理密钥，无需安装 `.NET SDK` 或手动复制文件。
- 安装程序未做数字签名。仅在确认下载自本项目官方 Releases 时处理 Windows 未知发布者提示；若目录拒绝写入，可点击安装界面中的 **管理员重试**。

如需手动 ZIP / PowerShell 部署、故障排查与卸载说明，请见 [安装与故障排查](docs/INSTALL.md)。

---

## 架构与热更新支持

SPT Assistant 采用“常驻轻量宿主 + 可热更新功能模块”结构：

| 组件 | 职责 | 热更新支持 |
| --- | --- | --- |
| `esp-host` (`Local.Spt5.ESP.dll`) | BepInEx IL2CPP 客户端宿主、暂停重载恢复、错误边界 | 宿主（更新需重启客户端） |
| `esp-cover` (`Local.Spt5.ESP.Core.module`) | 主界面、ESP、战斗、场景编辑、聊天、JS 动态文字、内置趣味功能 | 支持（`Ctrl+F1` 或面板触发） |
| `gear-mail` (`LocalGearMail.dll`) | 服务端模组：物品邮件、SQLite 备份与战局历史、TLS 1.3 聊天中继、赛季 | 需重启服务端 |
| `gear-admin-module` (`LocalGearAdmin.module`) | 可热替换服务端管理模块（角色数据、剧情、仪式圈、场景编辑） | 支持（面板或脚本触发） |
| `raid-controls-server` (`LocalRaidControls.dll`) | 服务端规则：保险、修理、容器嵌套、BTR、分地图 AI/战局参数方案 | 需重启服务端 |
| `quickhack-mod` (`Quickhacks.Core.module`) | 独立赛博朋克快速破解模块（由趣味功能桥接管理） | 支持 |
| `universal-host` / `universal-sdk` | 通用客户端热更新加载器（`Ctrl+F9`），面向新独立客户端 MOD | 宿主 / 模块（`Ctrl+F9`） |
| `crash-monitor` & `safety-ui` | 全屏 `:(` 错误恢复界面与独立崩溃提示程序 | 随宿主配套运行 |

---

## 功能总览

游戏内按 **右 Shift** 打开主面板（`F12` 为原配置菜单，主菜单右上角 `中文 · English` 可随时切换语言）。功能分为三个工作区：

### 常用辅助
- **主页面**：展示当前 PMC 存档概览、资金、战绩、任务进度与常用功能快捷入口。
- **视觉**：人物/AI/物资/容器/载具/空投/BTR 站点 ESP、瞄准镜内自适应方框、枪口瞄准线、AI 路面路线与目标点显示、战斗/受击/全战局击杀信息、地雷与绊雷高亮、第三人称镜头、性能监测（`FPS` / `P99`），以及悬浮窗布局与不透明度设置。
- **导航**：撤离点定位与强行撤离控制、地图过图工具、任务目标定位、路面导航与角色传送。
- **射击**：射击辅助、微自瞄、瞄准排除、后坐力与枪械手感优化、命中音效、击杀特效与投掷物轨迹。
- **人物**：无敌（`F8`）、AI 忽略（`F9`）、各部位血量/能量/水分编辑、全身枪弹跳弹、破门强开与独立门卡破解、穿墙飞行、Buff 管理与死亡保留。
- **战局**：地图与活动模式、昼夜时钟控制与暂停、滚动战局超时保护、行动前开局时刻、战局内访问仓库、范围强制拾取、AI 难度，以及按地图持久化的 AI/战局参数方案。
- **历史战局**：基于 SQLite 记录每局入场装备、带出/保留/新增物品、游玩时长、撤离口与结算状态及详细击杀名单。
- **载具**：BTR 控制（免费乘车、免费/秒转移、10×100 扩展转移仓、叫车至最近站点）、装甲列车到发时刻控制，以及空投管理。
- **物品**：物品搜索与邮件发放、玩家枪械预设、整套装备方案，以及修理与耐久规则（商人修理保留最大耐久、护甲保护、容器嵌套、无限钥匙/门卡/注射器、容器秒搜索）。
- **趣味功能**：内置 **黑墙脉冲**（`F11` 施放 / `Alt+F11` 预览）、**镭射眼**、**像素剑** 与 **ProjectE**（转换桌、EMC、工具、图腾、MC HUD），以及 **CP2077 快速破解** 桥接设置。
- **剧情**：主线章节进度、商人任务重做、互斥剧情分支切换、结局切换，以及跳过符合条件的任务等待时间。
- **数据**：技能与成就编辑、钱包/TC 转账、PMC/Scav 装备管理、[SQLite 存档备份与跨角色还原](docs/profile-backup.md)、仪式圈奖励预览与秒完成，以及 PVE 独立赛季角色入口。
- **服务端**：30 余项服务端配置（保险、修理、藏身处、交易、战局）及 PMC/Scav 业力、跳蚤市场信誉编辑。
- **杂项**：独立规则开关，包括 Scav **外交豁免权**、**供不应求**（新挂单立即售出）、**供大于求**、商人/市场清仓甩卖、免手续费、边界狙击免疫与趣味物品开关。

### 开发工具
- **AI 开发**：本局 AI 列表、换武器、生成 AI、行为与路线编辑、装备与 Buff 编辑、传送、AI 模板、尸体清理及服务端实时生成参数说明。
- **枪械编辑**：实时查看与调整当前手持或装备槽中的枪械与配件参数。
- **模型与镜头**：自由镜头、人物模型调节与动作锁定。
- **玩具枪开发**：玩具枪生成与模板、命中动作（传送/击杀等）、弹药与射速及普通枪弹着点标记。
- **开发工具**：
  - 默认关闭的[场景编辑模式](docs/world-editor.md)（`F6`）：准星右键菜单与 `Alt` 拖框多选，支持创建、移动、复制、克隆、锁定及编辑本机控制的 AI、散落物品、尸体、已落地空投箱、地图容器、门与阔剑地雷，支持撤销与蓝图。
  - [聊天与斜杠命令](docs/CHAT.md)（`T` / `/`）：可配置聊天窗与按参数位置的 Tab 补全、MC 风格本地命令（含 Fika 权限范围控制），以及双方核验指纹的 **TLS 1.3** 会话级前向保密文字聊天。

### 设置与诊断
- **调试**：默认关闭的错误模拟工具，用于测试 `:(` 错误恢复界面与重载回退流程。
- **设置与热键**：可搜索的[全部热键设置](docs/hotkeys.md)（单键、组合键、鼠标按键）、配置方案、主题与布局编辑、[左下角 JavaScript 动态文字](docs/corner-version-scripts.md)（内置 Jint 引擎，支持 `fetch`、`game` 只读快照及 `@game-thread` 实时对象访问）、GitHub Releases 版本检测（支持 HTTP/HTTPS/SOCKS5 代理）以及脱敏诊断日志 ZIP 导出。

---

## 源码构建与文档索引

源码构建需要 **.NET 10 SDK** 与本机合法安装的 SPT 5.0 BE / EFT 48558 互操作程序集（仓库不包含游戏依赖）。详细构建、打包与独立模块开发流程见[安装与故障排查](docs/INSTALL.md)与[通用热更新加载器](docs/universal-mod-host.md)。

```powershell
# 将本仓库放在游戏根目录下的 _spt5_esp_fix，并在游戏根目录运行：
dotnet build _spt5_esp_fix/esp-cover/Esp.csproj -c Release
dotnet build _spt5_esp_fix/gear-mail/GearMail.csproj -c Release
dotnet build _spt5_esp_fix/raid-controls-server/RaidControls.csproj -c Release
dotnet run --project _spt5_esp_fix/probe/Probe.csproj -c Release
```

### 文档导航
- [Releases 下载页](https://github.com/TRFCUTTFwww/SPT-Assistant/releases) · [更新记录 (Changelog)](CHANGELOG.md)
- [安装与故障排查](docs/INSTALL.md) · [热键说明](docs/hotkeys.md)
- [聊天与斜杠命令](docs/CHAT.md) · [详细命令参数](docs/COMMAND-OPTIONS.md)
- [场景编辑模式](docs/world-editor.md) · [SQLite 存档备份](docs/profile-backup.md)
- [左下角 JavaScript 脚本](docs/corner-version-scripts.md) · [黑墙脉冲说明](docs/blackwall-pulse.md)
- [AI 与战局参数](docs/raid-parameters.md) · [通用热更新加载器](docs/universal-mod-host.md)

---

## 第三方素材声明与开源协议

本项目基于 [MIT License](LICENSE) 开源。

### 整合的 Minecraft 模组素材与内置库
| 项目 | 仓库 | 贡献内容 |
| --- | --- | --- |
| AllTheModium | [AllTheModium](https://github.com/AllTheMods/AllTheModium) | 像素剑 |
| ProjectE | [ProjectE](https://github.com/sinkillerj/ProjectE) | 转换桌、工具与饰品 |
| Modern Industrialization | [Modern Industrialization](https://github.com/AztechMC/Modern-Industrialization) | 量子剑 |
| Applied Energistics 2 | [Applied Energistics 2](https://github.com/AppliedEnergistics/Applied-Energistics-2) | 无线终端素材 |
| Thirst Mod | [Thirst Mod](https://github.com/ghen-git/Thirst-Mod) | 口渴 HUD 素材 |
| Jint / Acornima | [Jint](docs/licenses/Jint.txt) · [Acornima](docs/licenses/Acornima.txt) | 内嵌 JavaScript 解析与执行引擎 |

Minecraft 原版 UI 与图腾素材版权归 Mojang/Microsoft 所有，不视为上述开源模组资产。固定版本、文件哈希及第三方许可保留在仓库的 provenance 与 license 文件中。
