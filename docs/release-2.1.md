# SPT Assistant 2.1 — Unstable / 不稳定

## 简体中文

本次修订新增可勾选、隐藏、排序的右键菜单，含删除全部武器、A/B 标记及双方敌对/停战、暂停/恢复、对玩家关系、无敌开关、清除可移除异常状态。A/B 关系只作用于标记角色，不扩展至原生整队；暂停的角色需手动恢复才行动。菜单较长时支持滚动。

编辑模式开关默认 F6，可在全部热键或编辑模式设置中修改。新增默认关闭的“移动中的 AI 临时无敌”，仅在编辑器拖动期间保护该 AI，放下/取消/退出后解除，不改原有无敌设置。

修正与崩溃有关的两条路径：用缓存图标纹理替代转储中出现的原生矩阵运算；复活没有枪械的尸体会提示并保留尸体，原生激活未成功时停止自动补生成。转储只证明菜单矩阵调用是当时崩溃点，尚不能证明此前复活与崩溃的完整因果关系。秒搜索改为在原生执行入口设置 Instant 字段，避免尝试补丁 IL2CPP 字段访问器。

本次覆盖更新（仍为 2.1）：隐藏编辑模式的常驻准星及操作文字，保留选择标记和拖框；点选及 Alt 框选支持穿墙选择。AI 属性的“装备”页可向空的第一/第二主武器、手枪及近战槽添加武器，“战斗”页可标记 A 队、B 队或清除标记，供现有双方关系工具使用。尸体右键菜单支持复活、复制/粘贴、移动和删除；复活需要本局原始 AI 资料，复制使用独立物品 ID 和外观/姿态快照。尸体、装备及联机表现仍需游戏内验证。

**适配：SPT 5.0 BE / EFT 1.3.0.1.48558，Windows x64，Unity IL2CPP / .NET 10。**

- 对外版本统一为 **2.1**；侧栏 SPT Assistant 标题下只显示 `v2.1`，移除“控制中心”小字。
- 新增“物品 → 修理与耐久 → 秒搜索容器”，默认关闭。仅本地玩家主动搜索时使用原生即时搜索流程，保留原生物品发现和完成通知；关闭后新搜索恢复原规则。
- 完善“设置与热键 → 全部热键”：支持搜索、单键和组合键、清除绑定及恢复默认，增加 17 个配置入口。
- 编辑模式支持 Insert 创建、G 移动、Ctrl+C 复制、Ctrl+V 粘贴、Ctrl+D 复制并粘贴、Del 删除、F2 重命名 AI、F4 属性。杀死 AI、解除空中固定和编辑模式切换可自行绑定。菜单与提示跟随当前绑定。
- 补齐配置方案切换、详情翻页、撤离翻页和命令输入的改键入口。输入文字、失焦、其他界面占用输入或移动/框选中时抑制编辑快捷键。
- 包含此前的场景编辑器、易用命令输入及受所有权检查限制的 Fika 本地命令。库存/编辑器等未完成联机同步的写入仍受限制，收到的聊天文字不会执行为命令。

下载 `SPT-Assistant-2.1-experimental.zip`，解压到游戏目录外，关闭目标客户端及服务端后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language ch
```

Fika 成员添加 `-ClientOnly`，连接房主，不启动本地服务端。本包不包含 Fika。安装后先启动服务端，再通过 SPT 启动器启动游戏，按右 Shift 打开面板。完整路径和卸载方式见[安装说明](INSTALL.md)，快捷键详见[热键说明](hotkeys.md)。

仍为实验版，未经过广泛测试，不保证向后兼容；安装前备份存档和配置。编译、离线检查和安装包校验不代表游戏内验收。实际按键、界面、战局效果和 Fika 多客户端同步由用户检查。具体执行的检查见[验证记录](validation-2.1.json)。

## English

This revision adds configurable context item visibility and ordering, weapon removal, A/B marks and actor-scoped hostility/ceasefire, pause/resume, player relations, invincibility and removable effect cleanup. Long menus scroll. Paused actors must be resumed manually to act.

Crash mitigations replace the native matrix icon drawing call observed in the crash dump with cached textures, reject corpse revival without a firearm and stop retrying failed revival activation. The dump identifies the crash site, not the full causal chain. Instant search now sets the native Instant field at the execution entry point instead of attempting to patch an IL2CPP field accessor. In-raid confirmation remains pending.

This replacement 2.1 build hides the persistent editor crosshair/instruction HUD while retaining selection markers and marquee outlines. Point and marquee selection work through walls. AI Properties → Equipment can add weapons to empty first/second primary, holster and melee slots; Combat can mark team A/B or clear the marker for the existing relationship tools. Corpse context menus support revival, copy/paste, movement and deletion. Revival requires original AI data from this raid; copies use fresh inventory IDs and appearance/pose snapshots. Native corpse/equipment behavior and multiplayer synchronization still require in-game verification.

**Target: SPT 5.0 BE / EFT 1.3.0.1.48558, Windows x64, Unity IL2CPP / .NET 10.**

- Set the public version to **2.1** and show only `v2.1` beneath SPT Assistant in the sidebar, removing the “Control center” text.
- Add opt-in **Items → Repair & durability → Instant container search** for searches initiated by the local player. Retain native discovery and completion callbacks; new searches follow normal rules after disabling.
- Complete **Settings & hotkeys → All hotkeys** with 17 additional configurable entries, search, combinations, clearing and resetting.
- Editor defaults: Insert create, G move, Ctrl+C copy, Ctrl+V paste, Ctrl+D duplicate, Del delete, F2 rename AI and F4 properties. Kill AI, release air anchors and editor toggle can be assigned. Menus and hints reflect current bindings.
- Add configurable profile cycling, detail paging, extraction paging and command input. Editor shortcuts respect text input, focus, other input owners and active movement/marquee selection.
- Include the world editor, friendly command input and ownership-checked Fika local commands. Writes without multiplayer integration, including inventory/editor operations, remain restricted. Received chat never executes commands.

Extract `SPT-Assistant-2.1-experimental.zip` outside the game directory, close the target client/server, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "D:\SPT-5.0.0-48558" -Language en
```

Fika members add `-ClientOnly` and connect to the host without starting a local server. Fika is not bundled. Start the server, launch the game through SPT and press Right Shift. See the [installation guide](INSTALL.md), [hotkey guide](hotkeys.md) and [validation record](validation-2.1.json).

Experimental, without broad testing or guaranteed backward compatibility. Back up profiles and configuration. Builds, offline checks and package verification do not establish in-game acceptance. Actual key handling, UI, raid behavior and multi-client Fika synchronization require user verification.
