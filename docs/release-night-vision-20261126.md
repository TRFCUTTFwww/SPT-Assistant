# 夜视效果与镜内独立光源屏蔽 / Night vision and optic lighting

入口为 **视觉 → 瞄准镜**，页面顶部提供全屏夜视和光学瞄准镜夜视两组设置。原有全屏夜视开关与快捷键继续使用，新增功能默认关闭。

- 两组设置分别选择 PNV-57E、PNV-10T、Armasight N-15、AN/PVS-14、GPNVG-18、PVS-31A，或保留当前设备效果。六款预设取自 EFT 48558 / SPT 5.0 的原生设备参数，包含颜色、增亮、噪点、暗部增光与视野遮罩。
- 点击“以所选效果开始自定义”复制所选参数，可编辑 HEX 颜色、强度、噪点、暗部增光、遮罩和大小。数值仅验证类型和有限性，沿用开发工具不设置人为上下限的约定。
- “开镜夜视”有独立开关，只在本地存活玩家使用光学瞄准镜开镜时启用。机瞄、红点、自由镜头及非活动镜片不启用镜内效果；收镜、换镜、死亡、离局和热更新恢复此前状态。
- “全屏夜视屏蔽场景光源”与“镜内夜视屏蔽场景光源”分别控制对应画面，支持一开一关。屏蔽范围包含场景实时灯光、AI 和玩家枪灯、体积光束、原生定制灯光、面光源／管状灯与镜头光晕。
- 灯光只在对应相机渲染期间临时隐藏。不会修改枪灯物品的开关状态或 AI 决策状态；关闭功能及卸载时恢复此前启停状态。渲染命令缓冲区保留内容并按原顺序恢复，避免影响其他后处理效果。
- 烘焙光照、反射中的既有照明和发光材质仍可能可见。此功能覆盖动态照明与相关渲染管线，并不将所有发亮表面改成黑色。

Two independent channels on **Visuals → Optics** provide global and optic ADS night vision, with six native device presets, current-device mode and editable custom parameters. Each channel has its own light-suppression switch. Suppression covers real-time scene lights, AI/player weapon flashlights, volumetric beams, native custom/area/tube lights and lens flares for the selected camera. Native tactical-device and AI states remain intact. Existing baked lighting, reflections and emissive surfaces can remain visible.

## 验证 / Validation

- Release 客户端构建通过，保留三项既有警告。
- 95 项专项检查通过：六款设备与本机 SPT 数据逐项一致，自定义数值与颜色校验，开镜生命周期条件，四种全屏／镜内开关组合，嵌套相机、AI／玩家枪灯状态恢复、中断清理、动态发现灯光，以及命令缓冲区顺序与外部增删保留。
- 完整 probe 离线回归通过，涵盖原有瞄准镜、测距、贴纸、菜单、生命周期和本机原生 API 检查。


- 客户端已热加载 generation 9，ready=true、error=null，运行构建与部署文件 SHA256 一致：`8382CF2DEC2A42EEDF8EC3253B3D981C09E44CA952F73E89179A7289A83D8089`。修正版加载日志未出现夜视共用函数挂钩警告。
