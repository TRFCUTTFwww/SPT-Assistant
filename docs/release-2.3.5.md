# SPT Assistant 2.3.5 — Release Notes (`v2.3.1` → `v2.3.5`)

**Target:** `SPT 5.0 BE / EFT 1.3.0.1.48558` (Windows x64, Unity IL2CPP / .NET 10) · **Client Host:** `1.6.2` · **Setup Installer:** `2.3.5.0`

> **Experimental / Unstable.** Back up `SPT_Runtime/user/profiles`, `BepInEx/config`, and existing Assistant files before installing or updating. Passing builds and offline test suites do not replace in-raid visual and gameplay verification.

---

## 简体中文（相对 `v2.3.1` 的完整变更）

本次发布涵盖 `2.3.2`、`2.3.3`、`2.3.4` 与 `2.3.5` 期间的全部客户端、服务端、错误保护、场景编辑及工具链升级（截至提交 `f6eed87` 及打包版本对齐）。

### 1. 独立赛季角色与 PVE 赛季入口（2.3.3 – 2.3.5）
- **独立本地赛季档案**：在角色/模式选择中选择 **Create seasonal character** 时，建立独立空白账号并进入原生新建角色流程；仓库、任务、等级、藏身处、对话、保险和交易记录保存在独立 SPT 档案中，选择 **PVE** 即可切回原角色。服务端在 HTTP 请求边界捕获并路由所选账号 ID，首次选择前自动备份原完整档案，关联关系持久化于 `LocalGearMail/seasons`。不模拟官方联机匹配、排名、远程奖励或自动删档周期。
- **主菜单赛季内容**：支持在 PVE 主菜单展示原生 Seasons 入口（`数据 → 赛季`）。

### 2. 历史战局记录与战局超时保护
- **新增「历史战局」侧栏**：基于服务端 SQLite（`LocalGearMail/history/raids.sqlite`）按账号记录每局地图、角色类型、入场装备（支持折叠展开武器与护甲配件）、已记录的过图路线、原生游玩时长、撤离口、结算状态、带出/保留/新增物品与击杀详情（姓名、阵营、角色、等级、武器、部位、距离、时间），支持分页、按结算状态筛选与详情分页签。
- **滚动战局超时保护与 O 键计时同步**：使用滚动原生超时缓冲与无上限原生加班计时器，保留暂停与时间修改，禁用/卸载时恢复期限；原生 O 键计时面板每秒同步世界计时器与可见倒计时。
- **撤离点与载具队列修复**：重试已进入撤离点的原生条件状态并在 Proceed 前排队本地载具；缓存装甲列车查找与状态，消除 `OnGUI` 异常。

### 3. 邪教徒仪式圈与藏身处同步
- **仪式圈控制（`数据 → 仪式圈`）**：默认开启奖励预览（仅打开页面时查询）；新增 **立即完成本次仪式** 与默认关闭的 **自动秒完成**，操作前自动备份并校验角色、配方与请求身份。
- **藏身处物品栏同步与转移界面**：将奖励同步写入藏身处 `CircleOfCultistsGrid1`，独立同步实时仪式计时器，支持从藏身处直接点击并等待打开原生仪式圈奖励转移界面。
- **藏身处生产与配装**：改进藏身处制作/升级同步，配装方案与备份支持 Windows 原生文件/目录选择器。

### 4. 分地图 AI / 战局参数、AI 信息与路线显示
- **分地图参数与预设（`AI 开发 → 本局 AI 信息` / `战局 → 战局信息`）**：可按地图编辑 AI 上限、波次人数与时间、Boss/PMC 概率、助手阵营倍率、各模式战局时长、物资倍率及高价值权重，支持保存/载入预设方案（`LocalRaidControls/parameters.json` 与 `parameter-presets/`）与恢复默认，下一次地图生成生效。完成全部 13 张已安装地图的中英文翻译与参数说明。
- **只读服务端 AI 信息与自然生成暂停**：改为读取服务端内存快照说明，避免原生 `BotSettings` 的 `IntPtr` 反射序列化错误；支持全局暂停自然 AI 生成。
- **AI 路线叠加显示（`视觉 → AI 路线`）**：独立开关显示聚焦 AI 的地面寻路路线与移动目标点。

### 5. 左下角 JavaScript 动态文字与静态版本文字
- **独立页面「设置与热键 → JavaScript」**：内嵌 **Jint 4.4.1 / Acornima 1.2.0** 引擎（无 Node/Chrome 依赖），支持 `fetch`、`URL`、`URLSearchParams`、`Headers`、`Response` 与 `httpGet`，支持 `await`、顶层表达式、单次「测试运行一次」输出框及选中复制、毫秒至小时刷新间隔。
- **实时 `game` 快照与 `@game-thread`**：向脚本暴露 `game`（`raid`、`weather`、`population`、`exits`、`train`、`airdrop`、`player`、`vitals`、`weapon`、`hideout`、`performance`、`client`）只读快照；选择实时模式或首行写 `// @game-thread` 可在主线程访问 `world`、`player`、`profile`。
- **普通静态文字**：主题与布局页继续保留最多 160 字符的静态左下角文字替换。

### 6. 场景编辑模式扩展（`开发工具 → 编辑模式`）
- **地图容器目录与浮空连续放置**：列出本机数据库全部地图容器（标注当前地图模型可用状态），支持 `F7` 连续放置、默认开启的 **容器浮空放置**（`Ctrl+滚轮` 调距、三轴旋转、`X` 网格吸附）、空容器/服务端配置/自定义候选池重刷，以及容器重命名（`F2`）。
- **门状态与阔剑地雷工具**：新增门锁定/解锁/开/关分组操作（AI 寻路与穿行遵循编辑器门锁状态），新增阔剑地雷放置/旋转/移动/复制/删除与原生资源提取选项。
- **撤销、蓝图与悬停高亮**：新增编辑器撤销栈、JSON 蓝图导入/导出与红色目标悬停高亮。

### 7. 热更新安全、错误恢复、调试与杂项修复
- **错误保护屏与外部崩溃监控（宿主 `1.6.2` + `Assistant.CrashMonitor.exe`）**：可捕获运行时异常显示全屏 `:(` 恢复界面（支持日志导出、SQLite 救援备份、幸存结算、关闭或重启），热更新失败保持暂停并支持后台预检后回退上一版本；原生崩溃退出由独立 `Assistant.CrashMonitor.exe` 提示。
- **新增「调试」侧栏与诊断导出（`设置与热键 → 日志与诊断`）**：提供默认关闭的 5 种可控错误/热更新模拟；新增 `ALL`–`OFF` 日志分级与脱敏诊断 ZIP 导出（可选硬件、性能、游戏与服务端日志）。
- **其他重要修正**：
  - **人物 → 全身枪弹跳弹**：支持未穿护甲部位的全身原生跳弹判定与极端弹道本地枪弹伤害拦截。
  - **行动前时间与装甲列车**：行动前设置改为地图昼夜时刻 `00:00:00–23:59:59`；修改停站时间或立即发车后刷新列车原生运动插值。
  - **Scav 外交豁免权与物品 FiR**：避免不安全的 Scav 敌对度重算；切换战局内发现标记（FiR）后立即刷新原生图标并防止右键菜单重复生成按钮。
  - **黑墙脉冲与趣味物品隔离**：修复黑墙施放音效与阔剑网格；关闭趣味物品时自动备份并将已有物品树隔离暂存，重新启用后邮件返还。
  - **开发构建号与聊天日志降噪**：界面与日志展示嵌入式 `AssistantBuildNumber`；过滤普通聊天轮询请求日志并降低空闲轮询频率。

---

## English (Full Comparison from `v2.3.1` to `v2.3.5`)

This release brings together all client, server, safety, world-editor, and tooling updates across `2.3.2`, `2.3.3`, `2.3.4`, and `2.3.5` (through commit `f6eed87` and release packaging alignment).

### 1. Independent Seasonal Character & PVE Seasons Entry (`2.3.3` – `2.3.5`)
- **Separate local seasonal profile**: Selecting **Create seasonal character** creates an independent blank local account and runs the native character creation flow. Stash, quests, level, hideout, dialogues, insurance, and trade history are stored in a separate full SPT profile while **PVE** returns to the original character. The server captures the selected account ID at the HTTP request boundary, backs up the original full profile before first selection, and persists the link under `LocalGearMail/seasons`. Does not add online matchmaking, rankings, remote rewards, or automatic wipe schedules.
- **PVE Seasons menu entry**: Exposes native Seasons access under **Data → Seasons** when supported by the local controller.

### 2. SQLite Raid History & Rolling Raid Timeout Protection
- **New `Raid history` (`历史战局`) sidebar**: Records per-account raid entries in `LocalGearMail/history/raids.sqlite` with map, character side, entry gear (collapsible attached weapon/armor parts), recorded transit routes, native playtime, exfil point, outcome, extracted/kept/new items, and detailed player/bot kills, with status filters and pagination.
- **Rolling timeout protection & 1-second timer sync**: Keeps a rolling native timeout buffer while displaying the chosen deadline, uses the native uncapped clock for overtime, preserves pauses/time edits, restores deadlines on disable/unload, and syncs the native `O` timer panel every second.
- **Exfil & vehicle fixes**: Retries entered exits against native requirement state, queues local vehicles before Proceed, and caches Armored Train lookups/status without `OnGUI` exceptions.

### 3. Cultist Circle Controls & Hideout Synchronization
- **Cultist Circle (`Data → Cultist circle`)**: Enables reward preview by default (queried only when the page is open) and adds **Finish current ritual now** plus opt-in **Auto instant finish** with pre-operation backups and ownership/recipe validation.
- **Hideout stash sync & transfer screen**: Syncs reward items into hideout `CircleOfCultistsGrid1`, tracks the live ritual timer independently of the saved completion flag, and opens the native Cultist reward transfer screen directly from the hideout.
- **Hideout craft & loadouts**: Improves hideout craft/upgrade sync and adds Windows native file/folder pickers for loadout plans, backups, blueprints, and JS scripts.

### 4. Per-Map AI & Raid Parameters, AI Info & Route Overlays
- **Per-map parameters & presets (`AI development → Current raid AI info` / `Raid → Raid info`)**: Edit AI caps, wave counts/timings, Boss/PMC chances, Assistant faction multipliers, mode durations, loot counts, and high-value weights per map; save/load validated presets (`LocalRaidControls/parameters.json` & `parameter-presets/`) and restore defaults for the next map generation. Fully localized in Chinese and English across all 13 installed maps.
- **Server-only AI spawn info & spawn pause**: Replaces unsafe `BotSettings` `IntPtr` reflection with a read-only server snapshot view and adds natural AI spawn pause controls.
- **AI route & destination overlays (`Visuals → AI routes`)**: Independent toggles to render the focused bot's ground navigation route and movement target.

### 5. JavaScript Corner Scripts & Custom Corner Text
- **Dedicated `Settings & hotkeys → JavaScript` page**: Embeds **Jint 4.4.1** and **Acornima 1.2.0** (no Node.js/Chrome required) with `fetch`, `URL`, `URLSearchParams`, `Headers`, `Response`, and `httpGet`, supporting `await`, display expressions, one-off **Test run** output with selection copy, and `ms`–`hour` intervals.
- **Live `game` snapshot & `@game-thread`**: Exposes frozen `game` telemetry (`raid`, `weather`, `population`, `exits`, `train`, `airdrop`, `player`, `vitals`, `weapon`, `hideout`, `performance`, `client`), plus optional main-thread `// @game-thread` access to `world`, `player`, and `profile`.
- **Static corner text**: Retains plain static corner-version text replacement (up to 160 chars) under **Theme & layout**.

### 6. World Editor Expansions (`Developer tools → Editor mode`)
- **Map container catalog & floating placement**: Lists all database containers with per-map model availability, `F7` continuous floating placement (`Ctrl+Wheel` distance, 3-axis rotation, `X` grid snap), empty/server/custom loot pool rerolls, and `F2` container renaming.
- **Door controls & claymore tools**: Grouped door lock/unlock/open/close actions (respected by bot navigation and traversal), plus claymore placement/editing/undo with native mesh extraction.
- **Undo, blueprints & highlights**: Adds editor undo stack, JSON blueprints, and red hover highlights for editor targets.

### 7. Hot-Reload Safety, Error Recovery, Debug & Fixes
- **Error recovery screen & external crash monitor (Host `1.6.2` + `Assistant.CrashMonitor.exe`)**: Displays a themed full-screen `:(` recovery overlay for caught faults (with log export, SQLite rescue backup, best-effort native survival settlement, close/restart), holds failed reloads paused for background-checked rollback, and launches `Assistant.CrashMonitor.exe` on native process crashes.
- **Debug sidebar & diagnostics ZIP export (`Settings & hotkeys → Logs & diagnostics`)**: Adds opt-in error/reload simulation tools, `ALL`–`OFF` log levels, and redacted diagnostic ZIP exports.
- **Other key fixes**:
  - **Full-body bullet ricochet** (`Character → Survival`): Native collider ricochet checks and extreme-ballistic local bullet damage blocking even on unarmored limbs.
  - **Pre-raid time of day & Armored Train**: Sets pre-raid time as `00:00:00–23:59:59` map clock and refreshes native train interpolation after dwell/departure changes.
  - **Scav diplomacy & FIR icons**: Avoids unsafe Scav hostility recalculation, refreshes native FIR icons immediately after toggling, and prevents duplicate FIR context menu buttons.
  - **Blackwall audio, claymore meshes & fun-item quarantine**: Restores Blackwall cast audio and claymore meshes; quarantines owned fun-item trees when disabled and mails supported items back when re-enabled.
  - **Build ID & quiet chat polling**: Displays embedded `AssistantBuildNumber` and suppresses noisy routine chat polling logs.

---

## Verification Summary (`2.3.5`)

- **Release builds (`Build-ExperimentalRelease.ps1`)**: Client (`Local.Spt5.ESP.dll`, `Local.Spt5.ESP.Core.dll`, 0 errors / 3 existing warnings), Server (`LocalGearMail.dll`, `LocalGearAdmin.module`, `LocalRaidControls.dll`, 0 errors / 0 warnings), Quickhacks (`Local.Cp2077.Quickhacks.dll`, `Local.Cp2077.Quickhacks.Core.dll`, 0 errors / 0 warnings), `Assistant.CrashMonitor.exe`, `SPT-Assistant-2.3.5-Setup.exe`, and `SPT-Assistant-2.3.5-experimental.zip` all built cleanly.
- **Offline verification suites passed (`EXIT 0`)**:
  - `Install.ps1 -VerifyOnly`: package hashes, target dependencies, and local item database verified.
  - `probe`, `hideout-sync-tests` (14 checks), `corner-script-tests` (73 script/HTTP checks), `ai-info-tests` (23 checks), `raid-history-tests` (59 checks), `raid-controls-tests` (12 EFT 48558 maps + parameter/loot/BTC/death/repair suites), `gear-mail-tests` (40 flea/diplomacy + 28 clearance/FIR + mail/admin checks), `profile-backup-tests` (42 checks), `chat-tests`, `timeout-guard-tests` (35 checks), `container-editor-tests`, `diagnostics-tests` (19 checks), `safety-ui-tests`, and `host-tests` (8 reload/recovery/simulation checks).
- **Manual / in-game boundary**: Offline builds, probes, and isolated suites do not replace in-game visual, UI, and live raid verification; native first-time seasonal creation, hideout Cultist Circle reward claiming, World Editor interactions, and live Fika synchronization remain for user verification in game.
