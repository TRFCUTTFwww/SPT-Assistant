# SPT Assistant 2.4 — 2026-11-28

Source snapshot: `04f9e0fdf11191b47d560c3890517bd86c308b67`. Target: SPT 5.0 BE / EFT 48558, Windows x64.

## 2026-11-28 覆盖更新

- AI 生成使用已预热角色资料克隆、并发 PMC 准备与就绪后即时结算，缩短生成等待。
- 客户端热更新使用持久 Harmony 挂钩，保留初始化依赖顺序并加强备用挂钩清理。
- 切换手持物品前补齐物品、配件与弹药资源池，避免对共享泛型 IL2CPP Proceed 入口安装补丁。
- 瞄准镜贴纸新增固定／自定义颜色、预设色、RGB／HEX 输入和各镜具方案颜色同步。
- 修复原生库存根节点导致的在线出售卡死，支持多件上架，物品透视估值使用同步跳蚤价格。
- 更新原生库存右键操作与无限制 JSON 编辑，改进空格添加物品、BTR 友好、注射器、投掷轨迹和选图时钟刷新。

本次包含当前源码快照、改动清单和关联文档。涉及服务端组件的更新请使用完整安装器或 ZIP。游戏内界面与实际战局效果由用户自行检查。

## Release refresh — 2026-11-28

- Speed up AI spawning with warm donor profile cloning, concurrent PMC preparation and immediate settlement when the spawn is ready.
- Add fast client hot reload with persistent Harmony hooks; preserve initialization order and harden fallback hook cleanup.
- Load missing item/attachment pools before switching hands and avoid the shared generic IL2CPP Proceed stub when installing the guard.
- Add solid/custom scope sticker colors with presets, RGB/HEX input and synchronization across saved optics.
- Fix online sale freezing at native inventory roots, support multiple selected items, and use synced flea prices for ESP valuation.
- Add native inventory context actions and unrestricted JSON editing; improve empty-cell item creation, BTR friendliness, injector use, grenade trajectories and map-clock refresh.

The refresh includes the full current source snapshot, changelog and linked documentation. Update with the full installer or ZIP when server components change. Game visuals remain subject to in-game verification by the user.

