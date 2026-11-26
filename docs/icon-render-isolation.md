# 原版物品图标串图修复

目标环境：Win11-GPU，EFT 48558 / SPT 5.0。模块 `icon-render-guard` 1.0.1，通过现有 UniversalModHost 热加载。

用户截图中的 Concordia 安保钥匙显示金鸡，Obdolbos 2 的图标也错误。读取运行时 ItemView、模板、图标 Hash 与磁盘 PNG 后确认：模板和缓存键正确，钥匙自己的缓存 PNG 内同时出现了金鸡，OBD2 的旧 PNG 内混有枪械模型。这是预览模型互相进入图标画面后被缓存，不是库存物品被替换，也不是单纯的 UI 格子错配。

补丁在引用类型 `ItemIconCreator.RenderModel` 的公共异步任务入口排队，一次只启动一条完整原生渲染流程，等模型创建、异步纹理等待、截图、模型归还全部结束，再启动下一件。保留原生的物品克隆、模型、相机设置、SpriteFactory、缓存 Hash 和写盘流程。只隔离相机最终截图还不够：等待截图的其他预览模型可能已经处于可渲染状态。

不修改原生 async 值类型状态机或 MoveNext，不操作人物动画/镜头、存档、装备、物品模板、模型贴图及服务端。生成异常/取消继续交还给原生调用者，后续任务仍可执行；有任务或修复请求在途时 `CanReload=false`，正常完成后可热更新。退出清理撤销本代补丁并取消尚未完成的代理请求，不销毁原生正在处理的模型。

首次热加载前缓存队列为空，先在两个已知错误物品上验证：两次任务成功，导出的钥匙和针剂 PNG 均不再包含其他模型。随后备份原图标索引，清除内存/文件索引，并刷新当前可见物品及五个指定回归目标。旧 PNG 没有删除；新索引令其不再用于显示，后续缺失图标按需排队生成。32 条原生任务完成、0 失败、队列清空。五个直接观察的预制体分别是钥匙、Obdolbos 2、漂白剂、金鸡和诊断仪，导出图片逐一检查正确。

用户在游戏内明确确认“钥匙和 OBD2 都正常了”。该确认覆盖这次已报告物品；没有宣称检查过所有物品组合。

## 构建和安装

```powershell
dotnet build icon-render-guard/Guard.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-HotModule.ps1 -Id icon-render-guard -Assembly icon-render-guard/bin/Release/net10.0/Local.IconRenderGuard.HotModule.dll -EntryType IconRenderGuard.IconRenderModule -Version 1.0.1
```

本次构建 0 警告、0 错误；发布版本 `r20261115102603-c73a3ce4`。运行中的模块为第 2 代，宿主无错误、无重启要求。实际游戏进程仍为 23072，先前已验收的辅助核心镜头修复未改动。

## 可选的定向缓存重建

默认仅隔离新图标的生成，不自动重建旧缓存。仅在已确认存在旧错误图标时，向 `UniversalModHost/state/icon-render-guard/data/repair.request.json` 写入一次性请求，例如：

```json
{"templates":["63a39c7964283b5e9c56b280","637b60c3b7afa97bfc3d7001"],"invalidateAll":false}
```

`templates` 为 1–128 个明确模板 ID。会使用脱离库存的临时物品重建对应默认图标，并刷新已打开的匹配视图；实际装备的改装枪械通过界面刷新走自己的原生完整配置 Hash。请求前的目标 PNG 和索引保存在模块数据目录 `before-*` 中。

`invalidateAll=true` 会备份并作废整个物品图标索引，刷新可见视图，其余图标后续按需生成。不要周期性请求：正常启动或热更新只记录既有请求时间戳，不重复执行旧文件。`repair-result.json` 和 `render-status.json` 记录本次请求及渲染结果；不逐帧写盘。

回退可将模块清单 `enabled` 设为 `false` 并更新 `reload.request`。有在途渲染时会等它完成再卸载。卸载不自动恢复已知错误的旧图标索引。
