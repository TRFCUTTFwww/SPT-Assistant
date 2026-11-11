# Changelog

## Unreleased — AI equipment layout / AI 装备排版修复

- 修复 AI 属性内嵌装备编辑页的参数区与底部按钮重叠；装备槽按宽度换行，移除内嵌模式多余标题留白，高度不足时提供整页滚动与边界裁切。
- 武器、储物子页同样限制在所属区域内，短窗口可滚动访问底部操作；切换目标重置整页滚动位置。
- Fix overlapping parameter/action regions in embedded AI equipment editing. Wrap slot buttons by available width, remove redundant embedded header spacing, and provide clipped scrollable equipment, weapon and storage pages in short windows.
- Validation: EFT 48558 build and 127 Probe groups passed, including layout bounds across compact/full widths and heights. In-game visual verification remains manual.

## Unreleased — Marquee selection and air placement / 框选与空中放置

- 编辑模式按住 Alt 显示鼠标，左键点选/拖框多选，提供投影标记与数量；混选时右键先选 AI/物品类别，再进入滚轮编辑菜单。
- 单类批量移动、复制/粘贴、删除与 AI 杀死；移动保留三维间距，取消恢复各对象状态。属性/重命名支持逐个切换，修改只作用于当前对象。
- 编辑移动及玩具枪传送允许精确坐标，无导航落点时固定 AI 于空中；提供解除固定，放回导航地面恢复原暂停状态。生成仍使用原生异步导航要求。
- Hold Alt for cursor/marquee selection; mixed selections choose AI or Items before editing. Batch actions preserve spacing, target navigation isolates property/rename edits, and editor/toy teleport supports exact-coordinate air anchors with reversible physics/pause ownership.
- Validation: EFT 48558 build and 126 Probe groups passed; in-raid selection, movement and Fika synchronization still require manual verification.

## Unreleased — Compact editor menus / 紧凑编辑菜单

- 右键菜单改为准星旁的系统菜单样式；滚轮循环选择、跳过禁用项，左键进入，支持鼠标指向、方向键与 Enter，右键/Esc 关闭。
- 重命名改用小弹窗。AI 属性拆分基本、生命、行为、战斗、装备、Buff 六页，增加姿势/跟随/行为限制、单个 AI 无敌及承受玩家伤害倍率，嵌入装备与状态编辑器。
- Use a compact crosshair context menu with wheel selection, disabled-action skipping and left-click activation. Shrink the rename dialog and add six AI property pages with isolated per-page applies, behavior/combat editing and embedded equipment/Buff tools.
- Validation: EFT 48558 client build and 124 Probe groups passed. Two pre-existing nullable warnings remain. In-raid visual behavior and Fika synchronization require manual verification.

## Unreleased — World editor / 场景编辑

- 新增默认关闭的“开发工具 → 编辑模式”：准星右键菜单，创建为顶层第一项，支持 AI/散落物创建、移动、复制、粘贴、删除、属性，以及 AI 重命名和原生死亡操作。
- 移动临时暂停 AI，滚轮调整距离，取消恢复原位和原暂停状态；物品副本保留完整内容树并分配新 ID。编辑时暂停自动瞄准、玩具枪自动开火和激光。
- 修复布局保存栏被悬浮窗覆盖：工具栏最后绘制，预览框点击排除工具栏区域。新增中英双语操作说明。
- Add an opt-in crosshair editor with top-level Create, move/copy/paste/delete/properties for loose loot and locally controlled AI, plus AI rename and native kill. Preserve temporary pause state and clone complete loose-item trees with fresh IDs.
- Keep layout save/reset controls above overlapping previews and prevent those previews from consuming toolbar clicks. Include Chinese and English instructions.
- Validation: EFT 48558 build and 122 client Probe groups passed. Two pre-existing nullable warnings remain. In-raid behavior, visuals, loot pickup persistence and Fika synchronization remain unverified. See [world editor documentation](docs/world-editor.md).

## 2.0.6 — 2026-11-11

### 简体中文

- 修复开启“激光穿墙”后激光消失的渲染回归：光束改为写入 RGBA，显式构建带颜色的网格，并持续保留原生光束作为回退。穿墙补充层保留人物深度遮挡，命中和伤害逻辑不变。
- 在“物品 → 修理与耐久”新增默认关闭的“无限注射器”：保留本地玩家所持针剂的剩余使用量，继续执行正常动画、效果和副作用，不补回已经耗尽的针剂。
- 在“杂项”新增默认关闭的“供大于求”，描述为“商人购买无上限”。服务端解除商品库存和个人限购，保留价格、忠诚度与任务解锁要求；关闭后恢复原始库存与限购记录。启用后重新打开商人页面刷新。
- 同步客户端、安装脚本和文档版本为 2.0.6。新增功能均提供英文翻译。
- 验证：EFT 48558 客户端与管理组件构建、121 组客户端 Probe 和 24 组邮件/管理回归通过。激光的 GPU 画面、针剂实际使用及 Fika 同步仍需战局实测；不稳定、未经过广泛测试，不保证向后兼容。

### English

- Fix laser beams disappearing with wall penetration enabled by writing RGBA, building explicitly colored geometry and retaining native scene beams as a fallback. The supplemental wall layer preserves character depth; hit selection and damage behavior are unchanged.
- Add opt-in **Infinite injectors** under **Items → Repair & durability**. Preserve remaining uses of injectors carried by the local player while retaining normal animations, effects and side effects. Exhausted injectors are not refilled.
- Add opt-in **Oversupply**, described as **Unlimited trader purchases**, under **Miscellaneous**. Remove stock and personal purchase limits while preserving prices, loyalty and quest requirements. Disabling restores original stock and purchase records. Reopen the trader screen after enabling.
- Align the client, installer and documentation at 2.0.6 and include English translations for the new controls.
- Validation: EFT 48558 client/admin builds, 121 client Probe groups and 24 mail/admin regression groups passed. Laser GPU output, actual injector use and Fika synchronization still require in-raid verification. Unstable, not broadly tested; backward compatibility is not guaranteed.

# 1.38.7

- 新增「数据」侧栏：逐项修改技能等级与成就、查看/修改商城 TC 余额、将角色库存实体 TC 等额转入商城钱包，并复用既有角色管理。
- 新增「剧情」侧栏：查看主线章节及任务状态、重做商人任务、预览相关任务后切换互斥分支、切换当前/展示结局、跳过主线时间门槛。保存前自动备份，禁止战局中写入。
- LocalGearMail 1.7.0 新增固定数据/剧情/重载路由；独立可卸载模块在请求之间替换，候选加载失败保持现有服务，保存最后可用模块。支持菜单按钮与本机脚本触发，不重启服务器进程。
- 清理性能窗、活动、拾取等页面的开发解释，保留实际操作提示；列表只绘制可见行。
- 新旧客户端编译及 85 组 Probe 通过；6 组服务端测试涵盖实际配置、真实存档副本、钱包守恒、重复/过期/溢出拒绝、技能/成就、任务变量以及模块替换/失败保留。实际 UI 和剧情流程仍需游戏内验收。

# 1.38.6

- 新增默认开启的性能监测小窗，位于「镜头与画面 → 性能监测」，支持显示开关、刷新间隔、不透明度及原有布局编辑器移动。
- 逐帧统计最近约 5 秒的平均 FPS、平均帧时间、P99、最慢帧及 >50 ms 帧数；固定容量环形缓冲，默认每 0.5 秒刷新文字和排序，避免逐帧字符串与排序开销。
- 复用现有 Stopwatch 分阶段计时，显示辅助更新、界面及瞄具渲染回调的 CPU 毫秒数；帧首汇总上一完整帧，避免 IMGUI 多次事件被算成多个游戏帧。注明统计不包含游戏整体、GPU 和部分原生挂钩。
- 新增加权 FPS、P99 边界、卡顿过期、无效数值、环形覆盖和重置检查；85 组 Probe、新旧客户端分别编译通过，保留 2 处现有可空警告。游戏内布局和读数待用户验收。

# 1.38.5

- 修复 1.38.4 光学开镜转动视角时人物方框漂移：移除 LateUpdate 中固定的二维正交投影及 SetViewProjectionMatrices，将方框网格还原为人物深度处的世界坐标，继承瞄具实际渲染矩阵。
- 在精确匹配的原生 optic Camera.onPreRender 回调中刷新，进入 BeforeImageEffects 阶段，与人物共享纹理朝向和后处理。未收到回调时仍有世界坐标回退，避免只依赖回调；正常收到时不在 LateUpdate 重复构建。
- 保留批处理、缓冲区复用及血量缓存，热卸载移除回调和命令。新增矩阵使用、渲染阶段及回调生命周期检查；83 组 Probe、新旧客户端分别构建通过。移动视角、移动人物和变倍的视觉验收待用户完成。

# 1.38.4

- 光学开镜使用原生 OpticCameraManager.Camera 投影，将人物方框及血条批量绘入瞄具纹理，由游戏镜片完成映射、遮罩及眼距裁切；实时跟随当前倍率，避免主摄像机方框在镜内错位。光学开镜时隐藏主视野人物方框和文字，红点/机瞄保持普通显示，可关闭“瞄准镜内自适应方框”恢复普通路径。
- 镜内绘制复用动态网格和上传缓冲区，合并为一个绘制批次；血量读取缓存 100 ms。只裁切原有框边，不在纹理边缘生成假方框；退出瞄准、关闭 ESP、隐藏 HUD 和热卸载时移除命令。
- 新增“枪口瞄准线”：从当前原生 FireportPosition 沿 WeaponDirection 延伸至最近碰撞；默认青色、200 米、0.012 米线宽，可独立开关及调整。最多 64 个射线命中，不扫描人物或全场景；渲染器自动进入主视野与光学镜，退出战局隐藏，卸载销毁。
- 82 组 Probe 与 48558/47888 分别编译通过，保留两处现有可空警告。用户已确认此前破门恢复；本次镜内方框、枪口线的视觉效果及帧率变化仍需游戏内验收。

# 1.38.3

- 根据 48558 原生调用链适配破门：StartDoorBreak 内联成功判定并写入 MovementContext.NextBreachResult，动画最终读取该字段。增加本地移动控制器精确校验的开始后置及动画前置修正，保留原踹门动画和原生开门流程。
- 破门菜单字段只对准星附近的有限射线命中门开放，移除地图全局 Door 搜索与遍历。
- 容器缓存经归属校验的碰撞体/渲染器，绘制时继续读取实时 bounds，减少每帧父子组件搜索。
- 普通上色层每 50 ms 维护启用状态，只在变化时写入；每次最多新建 16 组，分摊人物刷新时的创建开销；离屏模型允许原生视锥裁剪，保留隔墙可见与原生场景渲染路径。
- 静默瞄准在真实射击时重新选目标，取消空闲时每 50 ms 重复扫描；暴力及微自瞄保持各自驱动路径。
- 80 组 Probe 检查通过，新旧客户端分别编译通过。1.38.2 的 ESP 对齐和画质由用户确认恢复；本次破门与帧率改善待战局验收。

# 1.38.2

- 人物、容器、尸体、撤离、转移、空投、弹道等世界标记统一使用主场景相机的归一化视口，转换为最终屏幕尺寸，修复 FSR/DLSS/SSAA 内部渲染尺寸造成的偏移；每次绘制重新获取主相机。
- 人物方框使用实际头部骨骼；容器锚点检查碰撞体所属容器，绘制时重新读取位置以跟随开启中的抽屉。
- 破门改为附近门原生字段的可恢复覆盖及角度、成功率前置挂接，移除无法拦截 IL2CPP 字段读取的旧方案。
- 增加显式、单次的本地画质修复请求，通过游戏原生设置接口保存原生分辨率、高质量 TAA、关闭 FSR/DLSS、噪点和色差。处理后保留用户后续自行选择的画质。
- 48558 与 47888 分别构建；当前客户端热加载及原生设置保存成功。79 组 Probe、12 组战局服务端和 2 组物品邮件检查通过。战局中的对齐、上色及破门效果待用户验收。

# 1.38.1

- 增加范围强制拾取、可配置快捷键，以及容器和尸体内容选项。通过原生库存事务移动真实物品，检查当前战局、物品所有权、距离和容量。
- 增加商人修复保留最大耐久、当前穿戴头盔和护甲不掉耐久、物品箱嵌套。商人按原规则收费，修理包保持原规则；箱子保留格数和防循环检查。
- 钥匙使用不消耗次数，保留原剩余次数显示，通过本地玩家的原生钥匙消耗概率接口实现。
- 1.3 客户端直接控制原生活动、常规/断电实验室和灯光，旧活动移植入口仅在 1.2 启用。
- LocalRaidControls 升级至 1.5.0。77 组 Probe、12 组服务端和 2 组邮件检查通过；48558 客户端热加载成功，新增战局玩法待实测。

# 1.38.0

- 适配 EFT 1.3.0.1.48558 的互操作程序集及地图加载状态机。构建路径改为 GameRoot/EftBuild 参数，保留 47888 条件编译支持。
- 仅迁移辅助 mod 与必要依赖；使用新客户端物品名称和价格库，保留原始菜单。
- 新版服务端及游戏启动日志已核对，客户端模块加载成功。

# 1.37.4

- 修复关闭“仅瞄准镜”后仍只有镜内上色：普通模式改用与原人物共享网格及骨骼的原生场景渲染层，不依赖主相机后处理阶段的绘制命令。镜内模式继续使用光学瞄具专用绘制命令。
- 保留阵营配色与按深度区分直视/遮挡的双通道，保持原人物材质；生成层排除自身扫描，跟随源模型启用状态，关闭上色、模式切换和离开战局时清理。
- Release 编译成功（0 错误、2 条原有警告），69 组离线检查通过；当前战局成功创建原生上色层，用户确认未开镜已能看到人物上色。镜内模式切换和遮挡明暗尚未单独实测。

# 1.37.3

- 人物上色从宿主 LateUpdate 直接维护相机绘制命令，普通模式不再依赖相机预渲染回调或瞄准状态，并可回退到有效主相机。
- 普通模式同时支持主视野和正在使用的光学瞄具；仅瞄准镜模式仍限制在光学瞄具内。关闭上色、离开战局或切换模式时移除旧相机命令。
- 补充相机、网格与绘制命令诊断。Release 编译成功，69 组离线检查通过；尚未验证游戏内实际显示。

# 1.37.2

- 沿用原有 IMGUI 菜单、图标、布局和滚动控件。
- 玩具枪增加“命中尸体复活”，通过本局 AI 原始角色资料和尸体当前库存重建，成功激活后移除原尸体；保留现有物品生成功能。
- 传送增加实时尸体目标；读取本局物资时使用尸体库存，传送到尸体物品附近前重新验证所有权。
- 人物模型增加可调速度和方向的 360 度持续旋转；枪械确认击杀增加脉冲环、碎光和准星反馈。
- 投掷物轨迹读取原生投掷参数，恢复预览过程中的随机数状态，预测至首次碰撞。
- 人物上色支持仅在光学瞄具内显示，并以场景深度区分直视亮色和遮挡暗色；标签可显示三点视线采样状态。
- Release 编译和 69 组离线检查通过；新增实战行为尚未验证。

# 1.36.40

- 同步当前本地开发源码，包括实验室模式入口、坐标读取、密码辅助、僵尸分类与显示等已存在的改动。
- 移除 Alpha 实验室菜单、运行/绘制回调及诊断命令；独立 Alpha 插件和配置从本地加载目录移出并备份。
- 保留其他实验室模式；Release 编译成功，67 组离线检查通过，客户端热加载确认成功。
- 本次推送为源码快照；最近打包的安装器仍是 1.33.0。

# 1.33.0

- 战局新增活动控制页，分别设置季节模式、召唤、僵尸、夜间邪教徒和暗实验室军团刷新。
- 保存前检查万圣节原生角色、模板映射、地图与感染比例；保存时备份，并支持恢复原始设置。
- 手动选择单一活动定义，按日期模式恢复原始日期，避免同类季节重复启用。
- 65 组离线检查通过；万圣节实战闪退尚未复现，未确认修复。

# 1.32.8

- AI 管理的 BOSS 生成列表加入迷宫狂暴版 Killa 与 Tagilla。
- 暂时隐藏队友侧栏入口，菜单显示版本与运行模块统一。
- 64 组离线检查通过。
# 1.32.7

- 队友移动、转向、暂停与避让交回原生控制器，移除 1.32.6 的移动接管钩子。
- 以原生指令层执行跟随、到达待命和靠近物资；缺少追击层的编队角色补入游戏原生层。
- 清图使用全图敌人记忆、隔离的追击设置；黑色军团放宽守楼与追击限制。
- 跟随站位预约、物资接近点与自身碰撞体判定修正；保留实际库存核验。
- Release 编译与 64 组离线检查通过；实战追击、避让和拾取未在本版重新验证。

历史版本见 [历史索引](history/README.md) 与仓库 Releases。
