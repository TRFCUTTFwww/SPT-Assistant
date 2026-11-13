# 0.5.2 原版字体与上传控件接入

用户协作提供的本地补包为 `CP2077_Upload_UI_Fonts` 和 `CP2077_Blackwall_Evidence`，均位于 `D:/aaa/CP2077_Collaboration_20261113`。素材、原始字体、补包和离线样张保留本机，不提交源码仓库。

## 字体

`scripts/Rasterize-QuickhackFonts.py` 从补包 manifest 校验过的 JingXiHeig 简中、Rajdhani 半粗、Orbitron regular TTF 生成 64 像素字形 PNG 与度量 JSON。构建取当前模块源码字符和可打印 ASCII，中文覆盖必须通过；每个图集记录源路径和 SHA-256。三个图集合计 394 / 95 / 97 字形，PNG 加采样边距，度量记录字形框、基线偏移和 advance。

运行时 `OriginalFonts` 加载图集，并保持现有 Text 的对齐、分行、裁剪和颜色接口。含图集外字形的整条文本使用原动态字体回退，适用于未预知的 NPC/玩家名字。运行时不调用系统字体安装，也不把 `.fnt` 改后缀当作 Unity 字体。热更新时随 Assets 清理纹理及度量缓存。

构建依赖 Python 3、Pillow、fontTools，可用 Build-Quickhacks.ps1 的 Python、PythonModulePath、UiSupplementRoot 参数指定。此方式采用原字形，但没有移植 REDengine 的全部语言排版与 hinting。

## 上传与完成

依据补包 `BOUND_UPLOAD_ANIMATION_TRACKS`、`WIDGET_TREE` 和 `QuickHackMappinController`：

- 图标 64×64、底框 80×88，以 2160p 几何换算到本模组 1080p 设计画布，并保留距离缩放。
- 入场 scale 1.5→1，0.4 秒；translation Y -100→0，0.25 秒；主节点透明度 0→1，0.03 秒。Back/EasyIn 使用常规 Back 入缓动重建，原引擎内部曲线实现未宣称逐位一致。
- 上传采用顶部锚定的纵向填充，覆盖背景、技能图标及框；技能实际上传时长仍由 HackRules 决定。
- 上传完成采用 Quintic 下移、Circular 回位及延迟线性淡出，总计约 0.416 秒，随后显示青色持续状态及剩余填充。
- 原型一次上传一个技能，不实现虚构队列。未绑定的 unfold 轨道不擅自绑定到根节点；queue_move_up 参数留作未来真实队列功能的参考。
- F10 预览和真实目标共用头顶绘制函数；取消、失去目标、失焦等仍走原有运行时清理。

## 黑墙音效与身体特效

Batch02 复核新包 42 项、旧资源引用 553 项，全部大小/哈希通过；七个 glTF 2.0 GLB 的顶点、UV0、索引边界和有限值检查通过，197 份引用 PCM WAV 解码长度通过。

`vfx_blackwall_hack_npc_upload` 在已查音库中 action_count=0、无媒体映射；本版停用突触熔融借用音效，保留真实 `w_expl_blackwall_npc_death_gameplay`。结论限于已查音库，不扩展成全引擎声音绝不存在的断言。旧安装残留的借用 WAV 不再被运行时调用，新包也不再分发它。

完整电弧、粒子和身体材质尚未移植，本版主要完成字体和头顶上传流程。共享 `blackbody_lit.mt` 仍标为缺项，不伪造为原件；它不阻塞本轮 UI 更新。

## 验证

新增离线检查覆盖图集 PNG 尺寸、字形边距和技能名字形、上传开场终点、Back 初始反向位移、完成偏移/淡出边界及全程有限值。构建继续运行既有谜题、原生 API、禁止共享 CanPressTrigger 补丁及热加载回归检查。

0.5.2 在本机已发布到常驻加载器，hotreload-status 为 generation=3、ready=true、waiting=false、error 为空。客户端日志确认三套原字形图集已创建。离线样张及资源加载成功不等于游戏画面验收；扫描布局、字距和真实目标投影仍由用户检查。
