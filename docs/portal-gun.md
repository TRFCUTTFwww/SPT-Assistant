# 传送枪 0.1.0 试玩版

适用于本机 EFT 48558 / SPT 5.0。客户端为通用热更新宿主的 `portalgun` 模块，物品注册为独立服务端插件 `LocalPortalGun`。不依赖辅助或快速破解模块。

## 使用

1. 重启服务端和客户端，让新物品模板进入客户端数据库。
2. 机械师一级商店购买 **光圈科技手持传送装置 / Portal Gun**，测试价 1000 卢布。
3. 放入手枪栏，按正常切枪键拿出。没有 F8 临时变枪开关，也不改普通武器。
4. 左键放蓝门，右键放橙门；Backspace 清除当前双门。不消耗弹药，也不接受装填。
5. 两扇门配好后，走入其中一扇即可传送。切回普通武器后门继续可用，退出战局、死亡或热重载时清除。

支持足够宽、平整、固定的墙和地面。拒绝斜面、天花板、门板、活动刚体及没有安全站立位置的表面。瞄准墙面约腰胸高度放置，门边缘需要完整覆盖墙面。两个门需相距 2.5 米以上；默认射程 100 米。

## 当前效果与边界

- 手持枪体采用本机 Portal 2 素材中的静态 prop 网格和贴图，含轻微开火回弹。没有移植原版骨骼手部动画。
- **独立物品 ID：`67360000000000000000a001`**。原生信号枪模板不修改。库存图标、物品查看与地面掉落外观暂用 SP-81 的基础资源；第一人称显示传送枪。
- 蓝橙门有发光轮廓及另一侧实时视角。仅渲染可见、靠近的门，默认宽度 512 像素、20 FPS、远景 150 米，无递归门中门。可降低分辨率/刷新率或关闭门内视角。
- 穿门不读条、不黑屏，不需要额外按键。转换朝向并保留有限的门内横向偏移；出口必须有地面和完整站立空间，会重新检测临时障碍物。
- **不是完全无缝的 Portal 物理复刻**：EFT 的实心墙碰撞保留，在人物碰撞壳接近门时提交原生传送；出门会校准站立高度并留出安全距离。可能看到轻微位置跳变。地面门也保持人物直立，不实现动量抛射、身体切片、子弹/AI/物品穿门。
- 原生枪械输入仅按当前手持物品 ID 拦截。切枪、丢弃、移动、打开背包仍走原生流程；Ctrl/Alt 按住时不发射传送门，避免与扫描组合操作冲突。

## 配置及热更新

首次加载生成：

`BepInEx/plugins/UniversalModHost/state/portalgun/data/settings.json`

字段：`Volume`（0–1）、`Range`（5–200 米）、`LiveView`、`ViewSize`（256–1024）、`ViewFps`（5–60）。编辑后 Ctrl+F9 重载。客户端逻辑以后可以热更新；新增物品或服务端注册逻辑仍需重启服务端，客户端物品表改变后也需重启。

源码构建：

```powershell
python scripts/Prepare-PortalAssets.py D:/Portal2_PortalGun_Assets_20261114 D:/SPT_Mod_Staging_20261027/portalgun-0.1.0/Assets
dotnet build portalgun -c Release
dotnet build portalgun-server -c Release
dotnet run --project portalgun-tests -c Release -- D:/SPT_Mod_Staging_20261027/portalgun-0.1.0/Assets D:/SPT-5.0.0-48558/SPT_Runtime/SPT_Data/database/templates/items.json portalgun/bin/Release/net10.0/Local.PortalGun.dll
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-HotModule.ps1 -Id portalgun -Name 'Portal Gun' -Assembly portalgun/bin/Release/net10.0/Local.PortalGun.dll -EntryType LocalPortalGun.PortalModule -Version 0.1.0 -AssetsDirectory D:/SPT_Mod_Staging_20261027/portalgun-0.1.0/Assets
```

退出服务端后，将 `portalgun-server/bin/Release/net10.0/Local.PortalGun.Server.dll` 放入 `SPT_Runtime/user/mods/LocalPortalGun/`。不要复制 SDK、Unity 或服务端依赖 DLL。Valve 素材仅从本机已有提取包转换，不提交到源码仓库；本项目 MIT 许可不覆盖这些素材。

## 验证

自动检查覆盖入口方向、门框范围、地板侧向进入、背面拒绝、外部瞬移拒绝、到达后不连续回传、偏移变换、十个音效的 PCM 解码、独立物品/手枪分类、空弹药过滤、原始模板不变，以及客户端模块依赖与输入物品隔离。

服务端 `user/mods/LocalPortalGun/server-status.json` 记录本次启动注册状态及 PID。客户端宿主状态在 `state/portalgun/status.json`。客户端首次启动及游戏内画面、性能、购入/装备/丢弃、双门实地穿越仍由用户验证；离线通过不代表原生场景效果已验证。
