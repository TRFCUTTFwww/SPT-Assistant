# AllTheModium 四把独立近战剑 1.0.0

目标环境：EFT 48558 / SPT 5.0。每把剑有固定且不同的模板 ID，使用原生近战物品、近战槽、命中判定和存档流程。客户端外观使用 Universal Mod Host 模块，服务端另行注册物品与商人商品。

| 物品 | 模板 ID | 劈砍 / 刺击基础伤害 | 耶格一级价格 |
| --- | --- | --- | --- |
| ATM 剑 | a7a000000000000000000001 | 18 / 18 | ₽18,000 |
| 振金剑 | a7a000000000000000000002 | 36 / 36 | ₽36,000 |
| 难得素剑 | a7a000000000000000000003 | 51 / 51 | ₽51,000 |
| 合金剑 | a7a000000000000000000004 | 69 / 69 | ₽69,000 |

伤害依据 AllTheMods/AllTheModium 的 `1.21.x` 分支，固定提交 `b68d348df1efa129e8288eb170f04cc2543b7514`。`ATMTier.java` 的材质攻击加成为 12、25、35、65；`ModRegistry.java` 中四把 `SwordItem.createAttributes` 的附加伤害为 5、10、15、3。加上 MC 玩家基础攻击 1，得到 18、36、51、69。映射未附魔、满蓄力的普通基础伤害，不额外换算成 MC 心数。塔科夫的护甲、命中部位、技能和体力规则继续生效，最终扣血不保证跨游戏相同。攻速保留塔科夫原生近战动画节奏。

原始 PNG 是 MC 的 16×16 物品纹理，原始模型继承 `minecraft:item/handheld`。像素轮廓挤出为一像素厚度的三维网格，关闭平滑过滤及 mipmap。保留原图比例，按实测 gladius 网格坐标放置：X 厚度、Y 宽度、Z 剑身，长度约 0.832 米。复用塔科夫 gladius 的拔出、挥砍、刺击、收起、音效和碰撞；素材没有独立骨骼动画。原版 gladius 模板与原始共享网格不被修改。

客户端仅在这四个 ID 对应的模型实例上替换 gladius LOD 网格与材质。同步工厂、异步工厂、近战拔出路径以及现有战局人物都有处理。对象池转给原版 gladius 时恢复原网格和材质；禁用、热更新也恢复所有仍存活的实例。不创建独立 BepInEx 宿主，不注册每代 IL2CPP 类型。

## 安装

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Install-AllTheModiumSwords.ps1
```

初次安装后重启服务端和客户端，客户端登录时才会取得新增模板。随后外观逻辑的更新可用 `scripts/Publish-HotModule.ps1` 发布；服务端物品定义更新需重启服务端并重新登录客户端。

服务端目录：`SPT_Runtime/user/mods/AllTheModiumSwords`。客户端模块：`BepInEx/plugins/UniversalModHost/modules/atm-swords`。模块诊断：`state/atm-swords/status.json`。服务端注册成功会生成 `registration-status.json`，包含当前 PID、时间、四把剑的伤害及原版模板未变化的校验结果。

新剑可在耶格一级购买，也可在辅助 MOD 的物品搜索中按中英文名称寻找。第一次进入商店时，游戏若沿用了原版 gladius 的旧图标缓存，可清除客户端图标缓存后重新加载。

游戏内检查由用户完成：四把剑分别购买、装备、拔出、左右键攻击、收起、丢出拾取、仓库与检视显示；切回原版 gladius 后模型应恢复。分别测试无甲与有甲目标。编译和服务端数据检查不代表这些画面与命中效果已实际验证。

离线素材检查可执行 `python scripts/Test-AllTheModiumAssets.py`，覆盖原图 SHA-256、尺寸、UV/索引、封闭面、非退化三角形和映射后的坐标范围。客户端、服务端均针对本机依赖编译。2026-11-14 本机实际服务端启动验证：四个物品模板和伤害、中文本地化、耶格一级商品均通过 HTTP 回读；原版 gladius 保留 38/57 伤害。新增物品使用 `OnLoadOrder.Preload`，在存档载入前注册。

## 素材来源

原图、MC 模型声明和翻译来自 https://github.com/AllTheMods/AllTheModium 。源文件 SHA-256 和固定下载地址见 `atm-swords-client/Assets/provenance.json`。OBJ 为原 PNG 的像素轮廓挤出结果。第三方素材版权归其原作者；本地适配代码的许可证不替代第三方素材条款。
