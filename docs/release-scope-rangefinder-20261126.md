# 准星测距 / Crosshair rangefinder

2026-11-26，2.4 开发更新。

在“镜头与画面 → 瞄准镜”增加“准星测距悬浮窗”开关，默认关闭。通过光学倍镜瞄准时，准星右下方显示指向地点与玩家之间的距离，单位米，保留一位小数。无须导入或启用贴纸图片。

测距沿当前光学相机的中心射线读取最近有效碰撞，使用玩家位置计算距离。每秒更新 10 次，查询距离 5,000 米；没有命中或碰撞查询溢出时显示“— m”。离开光学瞄准、打开菜单、进入贴纸编辑或隐藏 HUD 时自动隐藏，卸载时清理缓存。悬浮窗不接管鼠标和按键。

验证：客户端 Release 构建、倍镜贴纸回归和英文目录检查通过。本机客户端热加载第 8 代为 `ready=true`，哈希与编译产物一致，配置已生成且默认关闭。

Added **Crosshair rangefinder** to **Visuals → Optics**, off by default. While aiming through an optical scope, a small panel beside the reticle shows the distance from the player to the indicated point in metres with one decimal place. Imported sticker artwork is optional.

The query follows the current optic camera's centre ray and selects the nearest valid collision. Distance is measured from the player's position. It updates at 10 Hz with a 5,000 m query range; missing hits or an overflowing query show **— m**. The panel hides outside optical aiming, in menus, during sticker editing and when the HUD is hidden. Unloading clears its cached state, and the panel does not capture input.

Validation: client Release build, existing scope-sticker regression checks and English localization checks passed. Client generation 8 is ready with the expected build hash; the generated configuration defaults to off.
