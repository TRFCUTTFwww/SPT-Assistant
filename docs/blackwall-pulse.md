# 黑墙脉冲试验版 0.1.0

面向 SPT 5.0 BE / EFT 48558 的独立客户端插件。`Local.BlackwallPulse.dll` 负责手臂动作、场景特效、音效、目标选择和伤害；辅助 Mod 通过可选的反射接口调用，不引用或嵌入插件程序集。辅助 Mod 仍为 2.1。

辅助 Mod → 模型与镜头 → 黑墙脉冲。默认 F11 施放，Alt+F11 无伤害预览；两个绑定均可在页面及全部热键修改，支持组合键和鼠标键。默认 60 米、前方 70 度、最多 8 个 AI、不穿墙。没有额外冷却，约 1.67 秒收手后即可再次使用。范围内优先选择较近目标。

施放使用本地导出的四种车站手势轮换，读取右手和手指轨道，以两段手臂 IK 适配塔科夫骨架。施放时临时隐藏持枪网格、阻止扣扳机，结束或中断后恢复原始显示状态，不切换库存装备。换弹或交互进行中拒绝启动；菜单、失焦、死亡、战局改变、手中控制器改变会取消尚未执行的伤害。AI 在释放阶段再次检查身份、存活、当前战局和辅助 Mod 的无敌保护，通过原生伤害和死亡流程生成正常尸体。

视觉是基于原版黑墙贴图的 Unity 重建原型：身体纹理覆盖、黑红电弧和短时画面故障滤镜。尚未逐项复刻 REDengine 粒子、体素材质和所有 IK 行为。参考包中的完整 REDengine 特效不是 Unity 可直接加载的资源。

## 本机安装

关闭游戏客户端，从源码根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-BlackwallPulse.ps1 -GameRoot 'D:\SPT-5.0.0-48558' -ResourceRoot 'D:\CP2077_Blackwall_Supplement_20261112' -Dotnet 'D:\SPT_Mod_Staging_20261027\dotnet-sdk\dotnet.exe'
```

安装器先核对所用资源的 SHA-256，构建两个客户端模块并备份旧文件。只安装一个动画 GLB、两张 PNG 和两个 WAV。资源取自用户本地导出，不进入源码仓库或公共发布包。新插件需要启动客户端加载；后续仅辅助模块变更可热更新。

独立插件配置在 `BepInEx/config/local.blackwall.pulse.cfg`。辅助 Mod 连接期间以辅助页面设置为准；辅助 Mod 缺席时读取独立配置，可设置 `Standalone key` 使用。插件不依赖辅助 Mod 启动。诊断写入 `BepInEx/plugins/BlackwallPulse/blackwall-state.json`，辅助诊断动作名为 `blackwall-state`。

## 验证边界

离线检查覆盖目标排除、穿墙开关、一次施放只释放一次、中断取消、无额外冷却、动作混合、实际 GLB 解码、手指运动与完整时长。编译和离线检查不能证明手臂适配、身体纹理、声音时机已在游戏内正确显示。

进战局后先按 Alt+F11 检查抬手、握拳、收手及武器恢复；再对少量 AI 按 F11 检查侵蚀、死亡与搜刮。游戏内界面和画面由用户检查。当前不包含 FIKA 同步。

卸载独立能力时移走 `BepInEx/plugins/BlackwallPulse` 文件夹；辅助 Mod 会显示未连接，不影响其余功能。
