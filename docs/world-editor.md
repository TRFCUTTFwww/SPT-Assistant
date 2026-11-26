# World editor / 场景编辑

## 简体中文

“开发工具 → 编辑模式”新增默认开启的“移动中的 AI 被其他 AI 无视”，移动开始时立即清除其他 AI 对目标的敌人记忆，移动期间持续抑制敌对识别，放下、取消或退出后解除。AI 开发/管理 → 行为与伤害可为所选 AI 独立开启“被其他 AI 无视”，持续到关闭或离开本局；不会覆盖原来的 A/B 和玩家关系。

已落地、物资就绪的空投箱支持点选、Alt 框选、右键移动/复制/删除，以及 G、Ctrl+C、Ctrl+V、Ctrl+D、Del。复制保留整棵物资树并重新分配所有物品 ID；移动取消恢复原位置、朝向和物理状态。空投箱与 AI、散落物品和尸体混选时先选类别，穿墙选择开关同样生效。仍在飞机上或降落中的箱体暂不进入编辑选择。

创建物品时先从列表选择物品，进入参数页，可设置堆叠数量及物品支持的耐久、医疗/食物/资源余量、钥匙次数、开关和搜刮标记，再点“应用参数并生成”。“返回列表”保留搜索和列表位置。生成前异步加载本体与附件资源，等待完成后才调用原生掉落接口并确认注册成功；失败时保留窗口并显示原因。物品复制/粘贴共用资源加载队列，切换战局会取消尚未生成的物品。

右键菜单的暂停/恢复、无敌开/关、A/B 敌对/停战、对玩家敌对/停战在状态一致时各显示一个反向操作按钮。在“自定义右键菜单”中可切换“混合状态显示两个按钮”（默认开启）：开启时，状态不一致会同时显示两个明确操作；关闭时只显示一个按钮，先统一暂停、开启无敌或对玩家停战，A/B 则统一敌对，再次点击反向切换。旧配置自动迁移并保留首个位置。A/B 标记和恢复默认关系独立保留。


右键菜单末尾的“自定义右键菜单”或“开发工具 → 编辑模式”可勾选显示项目、用 ↑↓ 排序、恢复默认。支持删除所有武器（含近战）、设为 A/B 方、清除队伍标记、暂停和恢复活动；批量作用于所选 AI。A/B 标记不自动更改敌对关系。菜单较长时滚轮或方向键会滚动。

复活前要求尸体至少保留一把枪械；不满足时提示并保留尸体。原生激活失败停止自动补生成。

编辑模式默认用 F6 开关，可在编辑模式设置或全部热键中改键。可开启“移动中的 AI 临时无敌”（默认关闭）：只在编辑器移动期间保护选中 AI，放下、取消、退出或卸载时解除临时保护；角色原有无敌不变。

扩展菜单还包括 A ↔ B 双方敌对/停战、对玩家停战/敌对/恢复默认关系、开启/关闭无敌、清除可移除的异常状态。先标记双方，再对任意所选 AI 打开右键菜单执行双方关系，作用于本局全部有效 A/B 标记角色，不扩展到未标记的原生队友。只标记不改变关系；缺少任意一方时禁用双方关系按钮。敌对不会自动取消角色暂停或动作锁定。

编辑操作现支持完整的可配置热键与当前绑定提示，默认按键及修改方法见 [热键补全说明](hotkeys.md)。

适配 SPT 5.0 BE / EFT 1.3.0.1.48558。

按右 Shift，打开 **开发工具 → 编辑模式**，启用默认关闭的开关，再关闭主面板。显示中心小十字准星，不显示常驻操作文字；Alt 鼠标操作或弹窗中隐藏准星。编辑时暂停自动瞄准、玩具枪自动开火和镭射眼，并拦截本地射击、瞄准及武器滚轮命令；可以移动和转动镜头。按住 Alt 或打开编辑窗口时拦截游戏镜头及移动输入，松开并释放鼠标后恢复。

- **Alt + 左键框选**：按住左/右 Alt 显示鼠标，拖动左键选择当前画面内的多个 AI、散落物和尸体。按对象中心投影选取，排除镜头后方和屏幕外的中心点，遮挡检查由“穿墙选中”开关控制；已选对象显示标记。Alt + 左键单击改为点选，空白单击或 Esc 清空；松开 Alt 恢复镜头，已选对象保留。
- **混合选择**：右键先显示“AI / 物品 / 尸体”类别及数量，选类别后进入编辑菜单；操作只作用于该类别。单一类别选择直接进入编辑菜单。
- **右键**：对着存活 AI、散落物品或空地打开场景菜单。有选区时编辑已选对象；没有选区时按准星点选。Alt 下菜单显示在鼠标旁，否则显示在准星旁；滚轮循环选择可用功能，左键确认进入，右键或 Esc 关闭，也支持鼠标指向和方向键/Enter。**创建**默认位于顶层第一项，可以搜索 AI 类型或物品名称/模板 ID，点击后在右键时记录的位置生成。AI 使用原生异步生成队列，需要有效导航地面；不是立即生成。
- **移动**：对象跟随准星，滚轮调整距离（1～100 米）。左键放下；右键或 Esc 取消并回到原位置。单类多选支持整体移动，保留三维相对间距，选区中心跟随准星。AI 移动期间临时暂停；放下使用精确坐标，不再要求导航落点。无导航地面时固定在空中并保持暂停，避免掉落或尝试寻路；重新移到导航地面或执行“解除空中固定”后恢复原暂停状态。取消逐个恢复位置、原有空中固定及暂停/刚体状态，即使其中对象已消失也继续恢复其他对象。需要先解除模型动作锁定。
- **快捷键**：Ctrl+C 复制所选类别，Ctrl+V 粘贴到当前准星位置（Alt 下使用鼠标指向），Del 删除所选类别；右键菜单打开时也可直接按快捷键，粘贴沿用该菜单打开时记录的落点。混选 AI 和物品时，复制／删除先弹出类别选择，选择后执行。无选区时不会自动选择并删除准星对象。移动、框选拖动、属性／重命名／创建输入、聊天和其他界面占用输入时不触发场景快捷键。
- **复制 / 粘贴**：单类多选保存整组独立快照，在新的右键落点按相对间距生成副本。物品保留完整内容树、配件、堆叠和组件状态，所有物品使用新 ID。AI 复用场景模板生成新档案，保存穿戴、武器配件、外观、血量和动作设置；沿用模板限制，不保存背包储物格内物资。
- **重命名 / 杀死**：仅 AI 可见。重命名采用 380 × 174 的小弹窗（小屏幕自动缩小），多选时略增高以容纳上一项/下一项，逐个修改且应用后保留窗口；切换对象丢弃当前对象未应用的输入。名字为 1～64 字符；杀死批量作用于所选 AI，使用游戏原生死亡流程，仍可能受无敌保护影响。
- **删除**：批量调用所选类别的游戏物品移除或现有 AI 清理流程；未选类别保留。没有撤销功能。
- **属性**：多选时使用上一项/下一项逐个查看和修改，应用只影响当前对象，切换对象丢弃未应用草稿。AI 分为“基本、生命、行为、战斗、装备、Buff”六页。基本页修改名字、朝向、俯仰与暂停；生命页修改七个部位最大/当前血量；行为页设置姿势、固定朝向、跟随和禁止开门/治疗/移动；战斗页设置单个 AI 无敌与承受本地玩家伤害倍率。装备页复用装备、武器和储物编辑器，Buff 页查看、添加和移除该 AI 的状态。各页独立应用，翻页保留草稿，关闭丢弃未应用修改，已应用修改继续生效；物品显示模板详情，可修改堆叠及该物品支持的耐久、资源、使用量、开关和搜刮标记。勾选组件参数后才会修改。

仅允许编辑本机控制且仍存活的 AI，排除玩家、队友和 BTR 射手；物品支持战局散落物，尸体单独支持复制、移动、删除和有本局资料的 AI 复活，地图容器操作见下文。打开主面板、关闭模式、死亡、切换战局或卸载模块会结束当前编辑；可解析的存活对象会恢复临时移动状态。不会跨战局保留选区和剪贴板，也不修改服务端生成配置。已经提交的空中固定可在关闭编辑模式后继续保持；退出战局清理记录，卸载/热加载模块时解除固定并恢复暂停状态。

玩具枪 **选择／传送 AI** 同样使用精确坐标：先命中 AI 选中，再命中目标位置（包括墙面等无导航位置），整组保留间距，无地面时固定在空中。仍需真实弹丸命中；对着空白天空射击不会生成传送事件。AI 的创建和粘贴仍使用异步原生生成与原有导航要求。

装备页按宽度换行显示槽位；短窗口内装备、武器与储物子页采用裁切和整页滚动，底部操作不会覆盖参数区。

布局编辑的 **完成并返回 / 恢复默认位置** 工具栏在所有布局预览框之后绘制，预览框不能截获工具栏区域的点击。


原编辑器验证：EFT 48558 编译通过，127 组客户端 Probe 通过，新增框选方向/屏幕/深度/有限值边界、空中批量相对坐标、编辑/玩具枪路由与物理拦截检查，并保留物品树复制、ID 冲突/环/孤立节点拒绝、根地址脱离、组件及格位保留、距离限制和鼠标释放检查。

操作完成后清空单选、多选和混合选择，移动放下或取消后也清空；属性和重命名窗口内仍可切换多个对象，关闭窗口时清空。右键菜单按桌面习惯，点击菜单外取消菜单和选中，鼠标移出本身不关闭；标题、底栏和禁用项目不会误触发之前高亮的操作。Alt 下空白单击也取消选中。

**穿墙选中**：在 **开发工具 → 编辑模式** 设置，默认开启以延续之前行为。关闭后，点选和 Alt 框选均检查遮挡，适用于 AI、物品和尸体。


### 地图容器与连续放置

容器支持 **F2 或右键 → 重命名**，复制时保留名称；同一柜子的抽屉统一命名。名称保留在当前战局，热更新后仍可读取。容器目录移除了外层多余滚动条，容器列表仍可滚动。

连续放置默认开启“容器浮空放置”：无需地面支撑或平坦坡度，按准星前方距离放置，**Ctrl + 滚轮**调整 0.5～20 米距离，Shift 配合 Ctrl + 滚轮精调。模型靠近障碍物时限制预览距离；空间不足仍会显示红色并阻止放置。X 开关 0.25 米网格，浮空时对三轴吸附；放下的容器固定在空中。可在编辑模式设置关闭浮空放置，恢复地面支撑检查。Q/E 左右旋转，PgUp/PgDn 上下旋转，R 切换滚轮 X/Y/Z 旋转轴，Shift 精调，Home 归零。

放置模型会恢复克隆中被禁用的网格渲染器，并移除原地图的场景渲染注册引用。删除、重刷及开关操作检查实际搜索界面和当前开关动画，不把关闭后残留的交互者或历史交互状态当作占用。

开启编辑模式（默认 F6），右键 → 创建 → 容器，或按 Insert 后选择容器分类。目录列出本机数据库的全部地图容器，可按名称、内部代号或模板 ID 搜索，并展示选中模型的预览。实心圆表示当前已加载可用模型，空心圆表示当前地图没有加载对应模型；同型变体可以复用已加载模型。没有模型的项目保留说明并禁止放置，不用替代箱体冒充。

选择初始物资模式后进入连续放置：当前服务端配置、空容器或自定义候选池。默认 F7 进入/退出；首次未选容器时打开目录。右键每次放置一个，左键删除准星处 20 米内的容器；Q/E 或滚轮旋转 15°，按住 Shift 改为 1°；X 切换 0.25 米网格，Esc 退出。保留走动和转动视角，拦截开火、瞄准及旋转键对应的侧身。绿色预览表示可放，红色说明空间不足，地面模式另检查坡度和支撑。碰撞检查使用保守包围盒，旋转后可能需要留出更多空间。

普通地图容器支持点选、Alt 框选与混合类别选择。G 移动、Ctrl+C 复制、Ctrl+V 粘贴、Ctrl+D 复制并粘贴、Del 删除；右键另有旋转、开/关及两种物资重刷。关闭旋转窗口或 Esc 恢复原方向。复制包含容器及完整物资树并重新分配全部物品 ID。创建的新容器固定物理位置，使用原生容器交互与物品所有者注册。此功能只修改当前战局，不保存为下局地图建筑。

物资重刷会替换全部库存；正在被玩家交互的容器拒绝替换或删除。当前服务端模式调用原生装箱器，并使用生成时最新的助手服务端物资开关、倍率和权重。自定义模式设置抽取次数、物品候选及正数相对权重，可控制“战局中找到”；实际装箱受原生容量、兼容性及生成规则限制，抽取次数不保证等于最终件数。空容器可在客户端直接创建。物资模型异步加载完成后才替换库存，失败保留原库存，切换战局会取消旧操作。

新增菜单首次迁移时自动加入，以后保留自定义隐藏/顺序。连续放置热键可在编辑模式设置或全部热键修改。

### 本局 AI 信息与生成暂停

AI 工作台 → 本局 AI 信息是只读的服务端 AI 生成参数说明页。进入战局后自动读取当前地图，顶部显示地图、读取状态和时间，可手动刷新。分为人数与上限、生成时间、难度与阵营、普通波次、Boss / PMC 波次、助手刷新规则；每项显示中文名称、服务端值、影响说明和原始参数路径。普通及特殊波次可逐批翻阅，搜索同时匹配中文解释、英文参数名、角色和值，并跨批次查找。

页面说明人数上限与累计人数的区别、PVE 上限、时间单位、-1 特殊时间、波次适用模式、随从人数候选、触发条件、概率与忽略上限。缺失字段显示“未提供”，不会当作 0；助手倍率关闭时，保存的倍率不会被描述为已生效。

数据来自服务端当前内存配置，不是进局时响应的历史快照，也不是已经生成的 AI 统计。其他 MOD、游戏模式、事件条件和生成点仍可能改变实际结果；进局后修改服务端配置不保证本局重新应用。客户端不再反射并序列化原生 BotSettings（其继承的 Pointer 会导致 System.Text.Json 抛错），也不再每秒扫描场上 AI。请求失败后限频重试，同地图的下一局也会清除旧结果。

暂停/恢复自然 AI 生成保留在全局控制，参数说明页不放生成控制按钮。暂停保留场上 AI，助手手动生成不受影响；恢复不补发已拦截请求，离开战局重置。

验证：`ai-info-tests` 复现原生 BotSettings 的 IntPtr 序列化问题，检查缺失字段、模式上限、候选随从人数、特殊时间、只读转换和中文搜索，并可接收本机保存的真实接口响应路径。

### 更新与验证边界

本次涉及客户端核心、错误保护宿主、LocalGearMail 路由和 LocalRaidControls 生成接口。完整更新应在关闭客户端和 SPT 服务端后替换这四个组件；仅热更客户端不足以注册新服务端路由。

离线检查覆盖编译、物资树复制、参数拒绝、网格/角度边界、原生 48558 容器注册契约、队列生命周期、生成暂停与删除隔离，以及实际 Harmony 装箱器在当前“无物资”规则下的行为和异常后作用域恢复。容器外观、盖子动画、搜刮、物理碰撞及全部地图模型差异需要在游戏内确认。

## English

Editor actions now have configurable combinations and live binding hints. See [hotkey coverage](hotkeys.md) for defaults, rebinding and fixed controls.

Editor shortcuts: **Ctrl+C** copies the selected category, **Ctrl+V** pastes at the crosshair (or Alt cursor), and **Del** deletes the selected category. They also work in context menus; paste then uses the position recorded when the menu opened. Mixed AI/item selections open a category chooser before copying/deleting. With no selection, Delete never targets a crosshair object automatically. Scene shortcuts are suppressed while moving, marquee dragging, typing in properties/rename/create/chat, or another interface owns input.


Press Right Shift, open **Development tools → Editor mode**, enable the opt-in setting, then close the main panel. A small center crosshair is shown while persistent instruction text stays hidden. In Customize context menu, **Show both actions for mixed states** defaults to on: mixed selections show both explicit operations. Turn it off to show one action that first applies pause, invincibility or player ceasefire to all (hostility for A/B). Uniform selections always show one reverse operation. Aim assistance, automatic toy firing and laser eyes pause during editing. Local firing, aiming and weapon-wheel commands are blocked while camera and movement axes remain available. Holding Alt or opening an editor window captures camera/movement input; releasing Alt and mouse buttons restores it.

Completed operations clear single, multiple and mixed selections, including movement placement/cancellation. Properties and rename retain their target navigation until the dialog closes. Clicking outside a context menu cancels it and clears selection; moving the pointer outside alone keeps it open. Mouse clicks activate only the pointed enabled row. Alt-clicking empty space also clears selection. **Select through walls** on the editor settings page defaults to on; disabling it checks occlusion for both point and marquee selection of AI, items and corpses.

- **Alt + left drag** shows the cursor while either Alt key is held and selects multiple AI, loose items and corpses by their projected centers. Behind-camera and off-screen centers are excluded; walls do not block selection. Selection markers remain visible. Alt + left-click selects a single target; an empty click or Esc clears selection. Releasing Alt restores camera control and retains the selection.
- **Mixed selection** first offers AI / Items / Corpses and their counts. Choose a category, then an editing action; actions affect only that category. A single category opens the editing menu directly.
- **Right-click** an AI, loose item or empty ground to open the menu. An existing selection takes priority over crosshair picking. The compact menu appears beside the mouse while Alt is held, otherwise beside the crosshair. Scroll to cycle through enabled actions, click an enabled row to confirm, or right-click/Esc to close. Pointer selection and arrow keys/Enter also work. **Create** is first by default and can be reordered. Search AI roles or item names/template IDs and click a result to spawn at the recorded location. AI uses the asynchronous native queue and requires walkable navigation ground.
- **Move** makes the object follow the crosshair. Scroll to adjust distance from 1 to 100 meters; left-click to place, right-click or Esc to cancel. Cancellation restores its original position. Single-category batches preserve relative XYZ spacing and follow the crosshair by their centroid. AI pauses during dragging. Final placement uses exact coordinates without requiring navigation: points away from navigation are anchored in the air with AI/physics movement paused. Moving onto navigation or choosing Release air anchor restores the prior pause state. Cancellation restores each surviving object and its original anchor/pause/rigidbody state even if another target has disappeared. Release pose locks before moving AI.
- **Copy / Paste** creates independent snapshots of the selected category and preserves relative spacing when pasting. Loose item copies preserve the complete contents tree, attachments, stacks and component state with fresh IDs. AI copies use scene templates and fresh profiles, preserving worn equipment, weapon attachments, appearance, health and scene settings. The existing AI template limitation applies: items inside backpack grids are not copied.
- **Rename / Kill** apply only to AI. Rename uses a small 380 × 174 dialog that shrinks on smaller screens. Multi-selection adds a small Previous/Next target row; edits apply to one target and leave the window open. Switching targets discards unapplied input. Names must contain 1–64 characters. Kill acts on all selected AI using native death handling and can still be blocked by invincibility.
- **Delete** acts on the selected category using native loot removal or the existing AI cleanup path, retaining the other category’s selection. There is no undo.
- **Properties** uses Previous/Next to edit one selected object at a time. Apply affects only that object; switching objects discards unapplied drafts. It has six AI pages: General, Health, Behavior, Combat, Equipment and Buff. Edit name, yaw/pitch, pause, seven body-part health values, stance/facing/following and door/healing/movement restrictions. Combat controls per-AI invincibility and incoming local-player damage multiplier. Equipment embeds the existing equipment/weapon/storage editors; Buff displays, adds and removes effects on the selected AI. Apply each page separately. Drafts survive page changes; closing discards unapplied drafts and retains applied changes. Item properties edit stack size and supported durability, resources, remaining uses, toggles and found-in-raid state. Enable a component field to change it. Item template details remain read-only.

Only locally controlled living AI can be edited; players, teammates and BTR shooters are excluded. Loose loot, corpses, landed airdrops and map containers have separate editor actions. The container catalog lists all templates. Models can come from the current map or a local, asynchronously loaded cross-map model library; it never loads a source map scene. F7 toggles continuous container placement: right-click places, left-click deletes, Q/E or the wheel rotates, Shift selects fine rotation, X toggles grid snapping, and Esc exits. Opening the main panel, disabling editing, death, world changes or unloading ends the active edit and restores temporary move state where the original object remains resolvable. Selection and clipboard state do not carry across raids. Committed air anchors survive editor mode being disabled; raid exit discards records and module unload/hot reload releases anchors and restores prior pause state. Server spawn settings are unchanged. The AI workspace also exposes live server configuration and a raid-only natural-spawn pause that retains existing AI and permits manual spawning.
Toy **Select / teleport AI** uses the same exact-coordinate placement. Hit AI to select it, then hit a destination, including walls or other points without navigation. Groups retain relative spacing and air destinations stay anchored. Creating/pasting AI retains the asynchronous native spawn/navigation requirements.

Equipment slots wrap by available width. In short windows, equipment/weapon/storage subpages are clipped and scroll as a whole so action controls do not overlap parameter fields.

The layout **Finish and return / Reset positions** toolbar draws after all layout previews, and preview hit tests exclude its bounds.

Validation: EFT 48558 build and 127 client Probe groups passed, including reverse marquee dragging, screen/depth/finite boundaries, airborne batch offsets and free-placement/physics routing, alongside nested item cloning, ID collision/cycle/orphan rejection, root detachment, component/grid preservation, distance bounds and mouse-release gating.
## 2.1 覆盖更新 / Replacement build

- 显示中心编辑准星、隐藏常驻操作说明，保留选中标记、拖框和操作弹窗。点选与 Alt 框选的穿墙行为可开关；屏幕后方及屏幕外对象仍排除，点选优先最近的目标。
- AI 属性 → 装备 → 武器：空槽可添加第一主武器、第二主武器或手枪；近战页可添加近战武器。自动装配模板默认配件，原生槽位规则检查兼容性，已有武器先删除或用换枪功能替换。
- AI 属性 → 战斗：标记 A 队、B 队或清除标记，接入全局控制的关系双方选择；标记本身不改变原生分组或自动开战。
- 尸体可右键复活、复制、粘贴、移动、删除，也可框选并使用既有复制/粘贴/移动/删除热键。混选先选择 AI、物品或尸体类别。移动取消恢复姿态与物理状态；复制生成独立库存 ID，不改变源尸体。复制体可继承已有的 AI 复活资料，玩家尸体和缺失本局原始资料的尸体不能复活。


The replacement 2.1 build hides the idle instruction HUD, supports through-wall point/marquee selection, adds weapons to empty AI equipment slots, exposes A/B relationship marks in Combat properties, and adds corpse revival/copy/paste/move/delete. Mixed selections choose a category first. Copies use fresh inventory identities; revival requires original AI data from this raid. A/B marking does not itself regroup actors or change hostility. 

## 容器与性能修复（2026-11-17）

- 容器目录显示全部模板，● 为已就绪模型，↓ 为可从本机库加载，○ 为暂缺模型。选择 ↓ 后自动异步加载并生成实物预览；不加载来源地图，不运行其 AI、灯光或地图脚本。本机这次生成 48 个有效模板；缺少可用交互模型的特殊模板仍不可放置。
- 抽屉柜按整柜选择、移动、复制、旋转、删除；四个抽屉保留独立库存、ID 和交互。重新生成会分别处理每个抽屉。该操作仅在当前战局生效。
- 连续放置仍由 F7 进入/退出，右键放置、左键删除；Q/E 朝向，PgUp/PgDn 俯仰，R 切换滚轮 X/Y/Z 旋转轴，Shift 细调，Home 三轴归零。选中容器的旋转窗口也支持三轴和取消恢复。
- 服务端物资模式在放置前检查接口及当前地图的物资表；缺表/未就绪时禁用并默认空容器，自定义物资仍可使用。
- 离线 GameWorld 的 NetworkWorld 可为空；跳过仅适用于联网 World 的交互注册，仍初始化原生库存 owner 和碰撞体。
- 主面板改用缓存圆角蒙版和主题色；主页货币改为异步读取服务端快照，消除逐帧枚举原生全库存。容器原型、模型树和缩略图复用，目录只处理可见行，全局预制体扫描每个战局一次。
- 后台任务同步优先使用现有 QuestController；空闲角色管理不查询 Scav session。必要的应用对象查找缓存并限频。详细耗时分开记录编辑器、任务、Scav 和面板，方便区分周期性卡顿与绘制开销。
- 仓库还原修复了修改 SaveServer.GetProfiles() 字典副本导致的假成功：更新现有存档对象，调用原生保存，再比对磁盘 JSON。写入失败回退；还原后仍需退出客户端并重新登录，避免旧客户端库存覆盖恢复内容。

本机模型库由自己的游戏安装生成，资源不进入 Git，也不随模组分发：

```powershell
python scripts/Build-ContainerLibrary.py --game D:/SPT-5.0.0-48558 --output D:/SPT-5.0.0-48558/BepInEx/plugins/LocalSpt5ESP/container-library --modules <安装UnityPy的目录>
```

服务端此更改涉及固定路由 DLL 和管理逻辑 module，必须一起替换并重启服务端。可用 `scripts/Apply-EditorServerUpdate.ps1 -UpdateDirectory <含两个组件和checksums.json的目录> -WaitForExit -StartServer` 在现有服务端关闭后安装；脚本核对哈希、备份、失败回退，不主动结束进程，不修改存档。

验证包含容器离线规则与 48558 API 契约、39 项备份/SQLite 检查，以及真实 SaveServer 在隔离临时目录下的 7 项写盘/回退/拒绝假成功检查。
