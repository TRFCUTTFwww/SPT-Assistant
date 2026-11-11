# World editor / 场景编辑

## 简体中文

适配 SPT 5.0 BE / EFT 1.3.0.1.48558。**不稳定，尚未经过战局视觉验收或广泛测试。** 后续以最新版 SPT 为准，不保证向后兼容。

按右 Shift，打开 **开发工具 → 编辑模式**，启用默认关闭的开关，再关闭主面板。屏幕中央出现编辑准星。编辑时暂停自动瞄准、玩具枪自动开火和镭射眼，并拦截本地射击、瞄准及武器滚轮命令；可以移动和转动镜头。

- **右键**：对着存活 AI、散落物品或空地打开场景菜单。菜单紧凑显示在准星旁；滚轮循环选择可用功能，左键确认进入，右键或 Esc 关闭，也支持鼠标指向和方向键/Enter。**创建**始终是顶层第一项，可以搜索 AI 类型或物品名称/模板 ID，点击后在右键时记录的位置生成。AI 使用原生异步生成队列，需要有效导航地面；不是立即生成。
- **移动**：对象跟随准星，滚轮调整距离（1～100 米）。左键放下；右键或 Esc 取消并回到原位置。AI 移动期间临时暂停，结束后恢复之前的暂停状态；最终落点经过原生传送和导航检查。需要先解除模型动作锁定。
- **复制 / 粘贴**：保存独立快照，在新的右键落点生成副本。物品保留完整内容树、配件、堆叠和组件状态，所有物品使用新 ID。AI 复用场景模板生成新档案，保存穿戴、武器配件、外观、血量和动作设置；沿用模板限制，不保存背包储物格内物资。
- **重命名 / 杀死**：仅 AI 可见。重命名采用 380 × 174 的小弹窗（小屏幕自动缩小），名字为 1～64 字符；杀死使用游戏原生死亡流程，仍可能受无敌保护影响。
- **删除**：调用游戏物品移除或现有 AI 清理流程。没有撤销功能。
- **属性**：AI 分为“基本、生命、行为、战斗、装备、Buff”六页。基本页修改名字、朝向、俯仰与暂停；生命页修改七个部位最大/当前血量；行为页设置姿势、固定朝向、跟随和禁止开门/治疗/移动；战斗页设置单个 AI 无敌与承受本地玩家伤害倍率。装备页复用装备、武器和储物编辑器，Buff 页查看、添加和移除该 AI 的状态。各页独立应用，翻页保留草稿，关闭丢弃未应用修改，已应用修改继续生效；物品显示模板详情，可修改堆叠及该物品支持的耐久、资源、使用量、开关和搜刮标记。勾选组件参数后才会修改。

仅允许编辑本机控制且仍存活的 AI，排除玩家、队友和 BTR 射手；物品仅支持战局散落物，不包含尸体和静态容器。打开主面板、关闭模式、死亡、切换战局或卸载模块会结束当前编辑；可解析的存活对象会恢复临时移动状态。不会跨战局保留剪贴板，也不修改服务端生成配置。Fika 的编辑权限与结果同步尚未验证。

布局编辑的 **完成并返回 / 恢复默认位置** 工具栏在所有布局预览框之后绘制，预览框不能截获工具栏区域的点击。

验证：EFT 48558 编译通过，124 组客户端 Probe 通过，其中新增物品树复制、ID 冲突/环/孤立节点拒绝、根地址脱离、组件及格位保留、距离限制和鼠标释放检查。游戏内移动、模型、拾取持久性及窗口层级需实测；编译成功不代表视觉已验证。

## English

Target: SPT 5.0 BE / EFT 1.3.0.1.48558. **Unstable; not broadly tested or visually verified in a raid.** Development follows the latest SPT version; backward compatibility is not guaranteed.

Press Right Shift, open **Development tools → Editor mode**, enable the opt-in setting, then close the main panel. A crosshair appears. Aim assistance, automatic toy firing and laser eyes pause during editing. Local firing, aiming and weapon-wheel commands are blocked while camera and movement axes remain available.

- **Right-click** an AI, loose item or empty ground to open the menu. The compact menu appears beside the crosshair. Scroll to cycle through enabled actions, left-click to confirm, or right-click/Esc to close. Pointer selection and arrow keys/Enter also work. **Create** is always the first top-level action. Search AI roles or item names/template IDs and click a result to spawn at the recorded location. AI uses the asynchronous native queue and requires walkable navigation ground.
- **Move** makes the object follow the crosshair. Scroll to adjust distance from 1 to 100 meters; left-click to place, right-click or Esc to cancel. Cancellation restores its original position. AI pauses temporarily and regains its previous pause state afterward; final placement uses native teleport/navigation checks. Release pose locks before moving AI.
- **Copy / Paste** creates independent snapshots. Loose item copies preserve the complete contents tree, attachments, stacks and component state with fresh IDs. AI copies use scene templates and fresh profiles, preserving worn equipment, weapon attachments, appearance, health and scene settings. The existing AI template limitation applies: items inside backpack grids are not copied.
- **Rename / Kill** apply only to AI. Rename uses a small 380 × 174 dialog that shrinks on smaller screens. Names must contain 1–64 characters. Kill uses native death handling and can still be blocked by invincibility.
- **Delete** uses native loot removal or the existing AI cleanup path. There is no undo.
- **Properties** has six AI pages: General, Health, Behavior, Combat, Equipment and Buff. Edit name, yaw/pitch, pause, seven body-part health values, stance/facing/following and door/healing/movement restrictions. Combat controls per-AI invincibility and incoming local-player damage multiplier. Equipment embeds the existing equipment/weapon/storage editors; Buff displays, adds and removes effects on the selected AI. Apply each page separately. Drafts survive page changes; closing discards unapplied drafts and retains applied changes. Item properties edit stack size and supported durability, resources, remaining uses, toggles and found-in-raid state. Enable a component field to change it. Item template details remain read-only.

Only locally controlled living AI can be edited; players, teammates and BTR shooters are excluded. Only loose loot is supported, excluding corpses and static containers. Opening the main panel, disabling editing, death, world changes or unloading ends the active edit and restores temporary move state where the original object remains resolvable. Clipboard state does not carry across raids. Server spawn settings are unchanged. Fika editor authorization and synchronization remain unverified.

The layout **Finish and return / Reset positions** toolbar draws after all layout previews, and preview hit tests exclude its bounds.

Validation: EFT 48558 build and 124 client Probe groups passed, including nested item cloning, ID collision/cycle/orphan rejection, root detachment, component/grid preservation, distance bounds and mouse-release gating. In-raid movement, visuals, pickup persistence and window stacking still require manual verification.
