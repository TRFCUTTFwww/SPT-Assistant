# SPT Assistant 2.1 — Unstable / 不稳定

## 简体中文

**适配：SPT 5.0 BE / EFT 1.3.0.1.48558，Windows x64，Unity IL2CPP / .NET 10。**

- 对外版本统一为 **2.1**；移除侧栏 SPT Assistant 标题下的“控制中心 / 版本号”小字。
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

**Target: SPT 5.0 BE / EFT 1.3.0.1.48558, Windows x64, Unity IL2CPP / .NET 10.**

- Set the public version to **2.1** and remove the small “Control center / version” subtitle below SPT Assistant in the sidebar.
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
