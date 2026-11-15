# 物品图标串图修复：Icon Render Guard 1.1.1

适用 EFT 48558 / SPT 5.0。2026-11-15 在当前客户端复现并修复原生物品图标叠加旧预览模型的问题。

## 原因与处理

漂白剂、金鸡、钥匙、针剂及其他物品的缓存 PNG 中叠着相同的文件袋和迷彩物件。仅串行执行 `ItemIconCreator.RenderModel` 不能隔离此前遗留在图标相机下面的模型。

原生 `CaptureSpriteOfModel` 先调用 `SetupScene`，将当前模型挂到图标相机下面，再调用 `IconCamera.Render`。图标相机平时未激活；拍摄时启用它，也会让旧的同级预览重新可见。1.1.0 在场景中搜索已激活 Renderer，因而漏掉这些尚未激活的相机子物体；全场景遍历还造成了明显的单次扫描开销。

1.1.1 在 `IconCamera.Render` 入口、原生场景布置完成之后，只遍历该相机的 Renderer 子节点，并包含未激活节点。属于本次模型及其附件的 Renderer 保持可见；其他符合相机遮罩且原本可渲染的节点，仅在本次同步拍摄期间设置 `forceRenderingOff`。外层捕获的 finalizer 还原这些节点；原先已隐藏、已禁用或不在遮罩内的节点保留原状态。正常拍摄不更改模型激活状态、图层、网格和材质。

保留串行原生任务队列，以及尺寸校正：有效宽高原样保留，非正尺寸按实际物品的原生格子像素尺寸补齐。ProjectE 的图标调用改为 `ItemViewFactory.LoadItemIcon`，从调用端传入原生尺寸，避免零宽高进入 RenderTexture 创建。

## 验证

- 图标模块构建：0 警告、0 错误；16 项离线尺寸、隔离及程序集调用检查通过。
- 运行中的客户端进程 23072 加载 1.1.1 / `r20261115135245-f18f2b3a`，第 5 代，状态 `running`，无宿主错误或重启要求；发布文件 SHA-256 与清单一致。
- 38 个模板完成实际原生图标捕获，包含常见杂物、药品、武器、容器、多格物品和自定义道具。每次额外放入一个带 `HideAndDontSave` 的相机同级测试物体；38 次均在拍摄时隐藏、拍摄后还原。观察点位于纹理读回前，避免把隔离补丁运行前的状态误判为最终状态。
- 对比捕获前后的本地 PNG 联系表，已检查原来重复叠加的文件袋、迷彩物件消失；重建后的 38 个缓存 PNG 也逐项检查。实际缓存与测试捕获的尺寸一致。两次渲染不要求 PNG 字节完全一致。
- 隔离扫描的最慢记录由旧版五项采样的约 343.14 毫秒，降至新版本次 82 次捕获的约 3.60 毫秒。这是隔离代码的耗时，不能当作整帧、完整图标生成或战局 FPS 测试。
- 已备份图标索引，清除受污染的缓存索引并重建 38 项图标，同时通过原生接口刷新当前可见物品视图；合计 82 次原生任务完成，失败及捕获异常均为 0。修复后队列为空，图标相机与人物图标相机均恢复未激活、未启用状态。
- 一次性 `icon-diagnostics` 测试模块已停用，不作为正常运行依赖。

游戏内库存、仓库和终端页面的最终显示由用户自行检查。原生预览 PNG 的检查不等于所有界面、所有枪械组合或所有地图均已验收。像素物品原有的独立库存精灵图覆盖保持现有行为。

## 复查与回退

从源码根目录执行（使用 .NET 10 SDK，并确保 `Directory.Build.props` 中游戏路径正确）：

```powershell
dotnet build icon-render-guard/Guard.csproj -c Release
dotnet build projecte-client/Client.csproj -c Release
dotnet run --project icon-render-tests/Tests.csproj -c Release
```

运行状态在游戏目录的 `BepInEx/plugins/UniversalModHost/state/icon-render-guard/data/render-status.json`，一次性缓存修复结果在同目录 `repair-result.json`。不要把旧进程留下的状态当成当前运行证据。

本次诊断报告、修复前后 PNG 联系表和原清单在仓库外 `D:/SPT_Mod_Staging_20261027/icon-finish-20261115-134548`。缓存修复前的索引与请求模板 PNG 在游戏目录 `BepInEx/plugins/UniversalModHost/state/icon-render-guard/data/before-20261115055511697`；最初接手前的索引另存于上述外部备份目录。旧模块发布目录均保留。回退模块清单不会自动回退缓存；不要在客户端写缓存时手动覆盖索引。

不提交本地诊断文件、图片、缓存、游戏资源或发布程序集。
