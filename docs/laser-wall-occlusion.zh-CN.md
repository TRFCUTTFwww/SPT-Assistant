# 激光穿墙：人物遮挡修复

修复开启“激光穿墙”后，光束视觉覆盖在人物模型上方的问题。

原实现将光束深度测试改成 Always，会同时忽略墙体与人物。现在穿墙光束在独立的透明颜色/深度缓冲中绘制：先写入本地玩家和其他已注册人物的模型深度，再绘制激光，最后合成到对应相机。墙体不进入该缓冲，因此仍可透墙显示；人物身体、手臂和随人物挂载的可见装备保持遮挡。

仅处理显示层次，激光射线、命中选择和伤害逻辑没有修改。普通模式继续使用场景深度；穿墙模式不再使用 Always 盖住人物。缓冲只写入独立的临时目标，不清空或改写游戏原有深度，不替换人物材质。

主相机和原生倍率镜分别使用各自的投影、渲染尺寸和光束网格；只在实际发射且开启穿墙时附加回调。停止发射、关闭开关、切换战局、卸载及热更新时移除命令缓冲和回调。缺少合成着色器时回退到普通场景深度，并输出日志。

验证范围：EFT 48558 客户端 Release 编译及完整 Probe 回归。视觉效果仍需游戏内确认，不能把编译或热加载成功当作实机遮挡验证。

建议验收：第三人称转动镜头，观察激光在头部、身体、手臂和装备后的部分是否被遮住，而前方光束仍显示；再观察墙后光束、其他人物以及倍率镜。关闭穿墙后确认恢复场景遮挡，并确认停止发射、退出战局后不残留光线。

## English

Fixes laser beams appearing on top of character models when wall penetration is enabled. Wall mode now uses a transparent temporary render target with character-only depth before composing the beams into each supported camera. Character geometry occludes the beams while environmental geometry is omitted from that private depth buffer. Ordinary scene depth, actor materials, physics raycasts and damage behavior are preserved. Main and optic cameras use separate beam meshes and command buffers, which are removed when firing stops or the module unloads. Actual visual occlusion, third-person equipment, wall visibility and optics still require in-game verification.
