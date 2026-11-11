# 热键补全 / Hotkey coverage

适配 SPT 5.0 BE / EFT 48558，功能模块版本 2.1。

打开 **设置与热键 → 全部热键**，或某个分类右上角的 **热键**。单键与组合键均显示在列表中，可以搜索动作名或当前按键，清除绑定或恢复默认。组合键录入时先按住 Ctrl、Shift 或 Alt，再按主键；左右修饰键等效，也支持鼠标中键、右键和侧键。已有按住类仍按住生效；原本要求 Ctrl 的旧切换项显示实际的 Ctrl 前缀，保留原有操作方式。

本次增加 17 个可配置入口，包括 11 个编辑相关入口、配置方案切换、两组详情翻页、两组撤离翻页以及命令输入。固定操作作为只读参考列出，避免将可查阅与可修改混为一谈。

| 编辑操作 | 默认热键 |
|---|---|
| 创建 | Insert |
| 移动所选类别 | G |
| 复制 | Ctrl+C |
| 粘贴 | Ctrl+V |
| 复制并粘贴 | Ctrl+D |
| 删除 | Del |
| AI 重命名 | F2 |
| 属性 | F4 |
| 杀死 AI | 未绑定 |
| 解除 AI 空中固定 | 未绑定 |
| 切换编辑模式 | F6（旧配置保留已有绑定，可在全部热键中修改） |

复制并粘贴采用独立快照，在当前准星位置生成副本；Alt 下使用鼠标指向，菜单打开时沿用菜单记录的落点。AI 创建与副本仍走原异步生成队列。重命名、杀死、解除空中固定只支持 AI。混合选区操作前选择类别；无选区时不会自动删除准星对象。创建和粘贴允许无选区。

编辑组合键允许额外按住 Alt 进行鼠标操作；其他 Ctrl/Shift 条件精确匹配。属性、创建、重命名、聊天等文字输入，主菜单、失焦、自由镜头、正在移动或框选时，场景编辑快捷键不触发。编辑动作消耗当前帧输入，避免同时触发原生蹲下等动作。菜单及 HUD 提示读取当前绑定，改键后不再显示旧默认值。

其他新增入口默认保持现有按法：配置方案 Ctrl+F2；物品与准星 AI Buff 详情 PageUp/PageDown；撤离面板 Ctrl+PageUp/Ctrl+PageDown；命令输入 /。清除后可禁用对应入口。详情翻页共用一组配置，撤离翻页仅在面板可见时生效，同一帧的多次 GUI 绘制只处理一次。

固定参考包括宿主热加载 Ctrl+F1，编辑 Alt/鼠标/滚轮/方向键/Enter，Esc，以及聊天候选与历史操作。宿主热加载仍由现有宿主管理，未改宿主 DLL。

验证：客户端编译、129 组客户端 Probe 与实际 BepInEx 配置保存/重读检查通过。组合键、鼠标组合、左右修饰键显示、清除及恢复默认通过离线检查。实际按键、窗口布局及战局表现由用户验证；编译或热加载不代表游戏内验收。

## English

Open **Settings & hotkeys → All hotkeys** or a category's hotkey page. Single keys and combinations are discoverable, searchable, clearable and resettable. For combination entries, hold Ctrl/Shift/Alt and press the main key; either side of a modifier works, and mouse buttons are supported. Legacy Ctrl toggles retain their behavior and show their actual prefix. Fixed controls are listed as read-only reference.

Editor defaults: Insert create, G move, Ctrl+C copy, Ctrl+V paste, Ctrl+D duplicate, Del delete, F2 rename AI, F4 properties. F6 toggles editor mode (existing bindings are preserved). Kill AI and release air anchors start unbound. Mixed selections request a category. Input dialogs, other input owners, focus loss and active movement/marquee dragging suppress scene shortcuts. Extra Alt is permitted for editor cursor use. Context menus and HUD hints reflect current bindings.

Other newly configurable defaults: Ctrl+F2 cycle profiles, PageUp/PageDown item/AI-buff details, Ctrl+PageUp/PageDown extraction pages, and / command input. Paging is processed once per frame. Ctrl+F1 remains a fixed host shortcut. Build and offline checks do not establish in-game acceptance.
