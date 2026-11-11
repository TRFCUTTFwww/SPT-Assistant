# World editor / 场景编辑

## 简体中文

适配 SPT 5.0 BE / EFT 1.3.0.1.48558。**不稳定，尚未经过战局视觉验收或广泛测试。** 后续以最新版 SPT 为准，不保证向后兼容。

按右 Shift，打开 **开发工具 → 编辑模式**，启用默认关闭的开关，再关闭主面板。屏幕中央出现编辑准星。编辑时暂停自动瞄准、玩具枪自动开火和镭射眼，并拦截本地射击、瞄准及武器滚轮命令；可以移动和转动镜头。按住 Alt 或打开编辑窗口时拦截游戏镜头及移动输入，松开并释放鼠标后恢复。

- **Alt + 左键框选**：按住左/右 Alt 显示鼠标，拖动左键选择当前画面内的多个 AI 和散落物。按对象中心投影选取，排除镜头后方、屏幕外和被地图墙体遮挡的中心点；已选对象显示标记与数量。Alt + 左键单击改为点选，空白单击或 Esc 清空；松开 Alt 恢复镜头，已选对象保留。
- **混合选择**：右键先显示“AI / 物品”类别及数量，选类别后进入编辑菜单；操作只作用于该类别。纯 AI 或纯物品选择直接进入编辑菜单。
- **右键**：对着存活 AI、散落物品或空地打开场景菜单。有选区时编辑已选对象；没有选区时按准星点选。Alt 下菜单显示在鼠标旁，否则显示在准星旁；滚轮循环选择可用功能，左键确认进入，右键或 Esc 关闭，也支持鼠标指向和方向键/Enter。**创建**始终是顶层第一项，可以搜索 AI 类型或物品名称/模板 ID，点击后在右键时记录的位置生成。AI 使用原生异步生成队列，需要有效导航地面；不是立即生成。
- **移动**：对象跟随准星，滚轮调整距离（1～100 米）。左键放下；右键或 Esc 取消并回到原位置。单类多选支持整体移动，保留三维相对间距，选区中心跟随准星。AI 移动期间临时暂停；放下使用精确坐标，不再要求导航落点。无导航地面时固定在空中并保持暂停，避免掉落或尝试寻路；重新移到导航地面或执行“解除空中固定”后恢复原暂停状态。取消逐个恢复位置、原有空中固定及暂停/刚体状态，即使其中对象已消失也继续恢复其他对象。需要先解除模型动作锁定。
- **复制 / 粘贴**：单类多选保存整组独立快照，在新的右键落点按相对间距生成副本。物品保留完整内容树、配件、堆叠和组件状态，所有物品使用新 ID。AI 复用场景模板生成新档案，保存穿戴、武器配件、外观、血量和动作设置；沿用模板限制，不保存背包储物格内物资。
- **重命名 / 杀死**：仅 AI 可见。重命名采用 380 × 174 的小弹窗（小屏幕自动缩小），多选时略增高以容纳上一项/下一项，逐个修改且应用后保留窗口；切换对象丢弃当前对象未应用的输入。名字为 1～64 字符；杀死批量作用于所选 AI，使用游戏原生死亡流程，仍可能受无敌保护影响。
- **删除**：批量调用所选类别的游戏物品移除或现有 AI 清理流程；未选类别保留。没有撤销功能。
- **属性**：多选时使用上一项/下一项逐个查看和修改，应用只影响当前对象，切换对象丢弃未应用草稿。AI 分为“基本、生命、行为、战斗、装备、Buff”六页。基本页修改名字、朝向、俯仰与暂停；生命页修改七个部位最大/当前血量；行为页设置姿势、固定朝向、跟随和禁止开门/治疗/移动；战斗页设置单个 AI 无敌与承受本地玩家伤害倍率。装备页复用装备、武器和储物编辑器，Buff 页查看、添加和移除该 AI 的状态。各页独立应用，翻页保留草稿，关闭丢弃未应用修改，已应用修改继续生效；物品显示模板详情，可修改堆叠及该物品支持的耐久、资源、使用量、开关和搜刮标记。勾选组件参数后才会修改。

仅允许编辑本机控制且仍存活的 AI，排除玩家、队友和 BTR 射手；物品仅支持战局散落物，不包含尸体和静态容器。打开主面板、关闭模式、死亡、切换战局或卸载模块会结束当前编辑；可解析的存活对象会恢复临时移动状态。不会跨战局保留选区和剪贴板，也不修改服务端生成配置。已经提交的空中固定可在关闭编辑模式后继续保持；退出战局清理记录，卸载/热加载模块时解除固定并恢复暂停状态。Fika 的编辑权限与结果同步尚未验证。

玩具枪 **选择／传送 AI** 同样使用精确坐标：先命中 AI 选中，再命中目标位置（包括墙面等无导航位置），整组保留间距，无地面时固定在空中。仍需真实弹丸命中；对着空白天空射击不会生成传送事件。AI 的创建和粘贴仍使用异步原生生成与原有导航要求。

布局编辑的 **完成并返回 / 恢复默认位置** 工具栏在所有布局预览框之后绘制，预览框不能截获工具栏区域的点击。

验证：EFT 48558 编译通过，126 组客户端 Probe 通过，新增框选方向/屏幕/深度/有限值边界、空中批量相对坐标、编辑/玩具枪路由与物理拦截检查，并保留物品树复制、ID 冲突/环/孤立节点拒绝、根地址脱离、组件及格位保留、距离限制和鼠标释放检查。游戏内移动、模型、拾取持久性及窗口层级需实测；编译成功不代表视觉已验证。

## English

Target: SPT 5.0 BE / EFT 1.3.0.1.48558. **Unstable; not broadly tested or visually verified in a raid.** Development follows the latest SPT version; backward compatibility is not guaranteed.

Press Right Shift, open **Development tools → Editor mode**, enable the opt-in setting, then close the main panel. A crosshair appears. Aim assistance, automatic toy firing and laser eyes pause during editing. Local firing, aiming and weapon-wheel commands are blocked while camera and movement axes remain available. Holding Alt or opening an editor window captures camera/movement input; releasing Alt and mouse buttons restores it.

- **Alt + left drag** shows the cursor while either Alt key is held and selects multiple AI/loose items by their projected centers. Behind-camera, off-screen and map-wall-occluded centers are excluded. Selection markers and counts remain visible. Alt + left-click selects a single target; an empty click or Esc clears selection. Releasing Alt restores camera control and retains the selection.
- **Mixed selection** first offers AI / Items and their counts. Choose a category, then an editing action; actions affect only that category. A single category opens the editing menu directly.
- **Right-click** an AI, loose item or empty ground to open the menu. An existing selection takes priority over crosshair picking. The compact menu appears beside the mouse while Alt is held, otherwise beside the crosshair. Scroll to cycle through enabled actions, left-click to confirm, or right-click/Esc to close. Pointer selection and arrow keys/Enter also work. **Create** is always the first top-level action. Search AI roles or item names/template IDs and click a result to spawn at the recorded location. AI uses the asynchronous native queue and requires walkable navigation ground.
- **Move** makes the object follow the crosshair. Scroll to adjust distance from 1 to 100 meters; left-click to place, right-click or Esc to cancel. Cancellation restores its original position. Single-category batches preserve relative XYZ spacing and follow the crosshair by their centroid. AI pauses during dragging. Final placement uses exact coordinates without requiring navigation: points away from navigation are anchored in the air with AI/physics movement paused. Moving onto navigation or choosing Release air anchor restores the prior pause state. Cancellation restores each surviving object and its original anchor/pause/rigidbody state even if another target has disappeared. Release pose locks before moving AI.
- **Copy / Paste** creates independent snapshots of the selected category and preserves relative spacing when pasting. Loose item copies preserve the complete contents tree, attachments, stacks and component state with fresh IDs. AI copies use scene templates and fresh profiles, preserving worn equipment, weapon attachments, appearance, health and scene settings. The existing AI template limitation applies: items inside backpack grids are not copied.
- **Rename / Kill** apply only to AI. Rename uses a small 380 × 174 dialog that shrinks on smaller screens. Multi-selection adds a small Previous/Next target row; edits apply to one target and leave the window open. Switching targets discards unapplied input. Names must contain 1–64 characters. Kill acts on all selected AI using native death handling and can still be blocked by invincibility.
- **Delete** acts on the selected category using native loot removal or the existing AI cleanup path, retaining the other category’s selection. There is no undo.
- **Properties** uses Previous/Next to edit one selected object at a time. Apply affects only that object; switching objects discards unapplied drafts. It has six AI pages: General, Health, Behavior, Combat, Equipment and Buff. Edit name, yaw/pitch, pause, seven body-part health values, stance/facing/following and door/healing/movement restrictions. Combat controls per-AI invincibility and incoming local-player damage multiplier. Equipment embeds the existing equipment/weapon/storage editors; Buff displays, adds and removes effects on the selected AI. Apply each page separately. Drafts survive page changes; closing discards unapplied drafts and retains applied changes. Item properties edit stack size and supported durability, resources, remaining uses, toggles and found-in-raid state. Enable a component field to change it. Item template details remain read-only.

Only locally controlled living AI can be edited; players, teammates and BTR shooters are excluded. Only loose loot is supported, excluding corpses and static containers. Opening the main panel, disabling editing, death, world changes or unloading ends the active edit and restores temporary move state where the original object remains resolvable. Selection and clipboard state do not carry across raids. Committed air anchors survive editor mode being disabled; raid exit discards records and module unload/hot reload releases anchors and restores prior pause state. Server spawn settings are unchanged. Fika editor authorization and synchronization remain unverified.

Toy **Select / teleport AI** uses the same exact-coordinate placement. Hit AI to select it, then hit a destination, including walls or other points without navigation. Groups retain relative spacing and air destinations stay anchored. A real projectile impact is still required; firing into an empty sky does not trigger teleportation. Creating/pasting AI retains the asynchronous native spawn/navigation requirements.

The layout **Finish and return / Reset positions** toolbar draws after all layout previews, and preview hit tests exclude its bounds.

Validation: EFT 48558 build and 126 client Probe groups passed, including reverse marquee dragging, screen/depth/finite boundaries, airborne batch offsets and free-placement/physics routing, alongside nested item cloning, ID collision/cycle/orphan rejection, root detachment, component/grid preservation, distance bounds and mouse-release gating. In-raid movement, visuals, pickup persistence and window stacking still require manual verification.
