# Portal 2 传送枪素材检查 — 2026-11-14

本机素材目录：`D:/Portal2_PortalGun_Assets_20261114`。素材说明记录来源为本机已安装的 Portal 2。此次仅检查和准备接入信息，没有实现传送枪功能，也没有将游戏素材加入源码仓库。

## 检查结果

`RESOURCE_MANIFEST.csv` 中 313 项的文件大小与 SHA-256 全部匹配。63 张 PNG 均实际解码成功；6 个模型均为 IDST v49，所列模型和材质映射存在。声音事件表的 38 条独立音频路径均存在。

| 内容 | 数量 / 情况 | 接入方式 |
| --- | --- | --- |
| Source 模型 | 6 个 MDL，附 VVD/VTX，部分有 PHY | 需要转换模型、骨骼与动画，Unity 不能直接使用 MDL |
| 第一人称传送枪 | `models/weapons/v_portalgun.mdl`，头部记录 31 根骨骼、14 段本地动画、14 个序列 | 保留枪体、手部和配件结构；之后验证转换结果和 EFT 持枪动画适配 |
| 第三人称枪体 | `w_portalgun`、`w_models` 版本及 `prop_portalgun` | 可分别用于持枪外观、世界物体与拾取展示 |
| 传送门 | `models/portals/portal1.mdl`、`portal2.mdl` | 原材质包含深度写入和遮罩，需重建 Unity 渲染逻辑 |
| PNG 贴图 | 63 张 | 已有枪体、蓝橙发光、法线、准星、门边缘和粒子纹理；按 VMT 解释透明度/材质通道 |
| 音频 | 56 个 `.wav` 文件，其中 53 个可按 PCM WAV 读取 | 开火、开门、关门、无效表面、进出门、启动和环境循环的事件引用齐全 |
| 压缩语音 | 3 个 GLaDOS `taunt_small_wave_portalgun*.wav` 并非 RIFF，文件头为 MPEG 音频帧 | 不能直接交给现有 PCM 读取器；如果之后需要这三段语音，先确认解码并转 PCM |
| 粒子 | 8 个 PCF 与对应可读 DMX 文本 | 用粒子参数、材质、子效果关系重建 Unity 效果，不能直接播放 Source PCF |

PCM 音频中：33 个为 44.1 kHz / 16 bit / 双声道，17 个为 44.1 kHz / 16 bit / 单声道，1 个为 22.05 kHz / 16 bit / 单声道，2 个为 11.025 kHz / 16 bit / 单声道。声音脚本中的 red 是原游戏橙门事件命名，接入时保留映射即可。

唯一已知缺失依赖是旧 DX80 备用粒子库引用的 `materials/particle/particle_glow_08.vmt`；当前版 `portal_projectile.pcf` 不引用它。没有发现当前版核心模型、粒子或音效的依赖缺失。

## 素材预览与下一步

本机生成了贴图联系表与详细结构检查：

- `D:/SPT_Mod_Staging_20261027/portal-assets-review-20261114/portal-textures-preview.png`
- `D:/SPT_Mod_Staging_20261027/portal-assets-review-20261114/asset-inspection.json`

联系表展示的是展开的贴图与粒子图，不是模型渲染。第一人称枪体贴图的 alpha 不能直接当作整枪透明度；应按原 VMT 重建不透明材质、法线和高光。实时门内画面、穿门碰撞、出口变换和动量保留属于程序实现，不在素材包中。

建议后续建立独立 `portalgun` 模块，使用通用加载器接口。先完成枪体模型和动画转换验证，再接蓝橙门放置、粒子/音效与本地玩家穿越；实时门内相机和动量传送分开验证。本次已提供加载器、发布脚本和示例工程，未提前修改游戏武器或安装未完成的传送枪。
