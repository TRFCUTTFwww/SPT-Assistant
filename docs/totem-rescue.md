# 不死图腾：快捷栏与致命伤恢复（ProjectE 1.2.4）

不死图腾放在本地 PMC 的口袋或弹挂直属格子时，可通过原版拖放或数字键绑定到 4–0 快捷栏。背包、仓库和嵌套容器不额外开放绑定；其他物品保留原版规则。图腾仍为随身自动触发的被动物品，按绑定键不会调用原版药品/武器使用流程，也不会收起当前枪械。

## 致命伤流程

原生 `ActiveHealthController.ApplyDamage` 在关键部位损毁后调用死亡判断，返回后还会继续执行 `SpreadOverDamage`，随后可能追加流血等效果。只在 `Kill` 前恢复生命并播放动画，无法阻止同一次伤害的剩余分摊；恢复过程中的事件回调也可能重入死亡逻辑。

`TotemRescueGate` 按健康控制器身份隔离保护，在移除图腾、恢复原生部位状态时拦截重入，恢复成功后保护到当前帧结束。`ApplyDamage`、`SpreadOverDamage` 和 `Kill` 共用此门控；下一帧不保留额外无敌。一次成功恢复仅消耗一枚图腾，恢复头胸等部位并验证正生命值，伤害返回后再次清理此次伤害追加的负面效果。原有再生和火焰抗性继续生效。

触发后在通用宿主 `state/projecte/data/totem-status.json` 记录阶段、存活状态、各部位生命和拦截次数，用于区分动画显示与实际恢复。不会自动对玩家施加致命伤进行测试。

## 同期报错处理

- 可选装备界面可能没有 `SecuredContainer` 视图，改用字典 `TryGetValue` 后再隐藏，避免缺键异常。
- 生存辅助在角色销毁、死亡或缺少移动上下文时停止更新负重，不再写入已失效的物理状态。
- 保留已验证的 `PlayerVisualGuard` 切手镜头修复及 `icon-render-guard` 图标隔离。

## 验证与部署

- ProjectE 与辅助核心 Release 编译均为零警告、零错误。
- `totem-tests`：20 项门控、重入、剩余伤害、单次消耗、下一帧正常死亡、绑定范围及编译产物挂钩检查通过。模拟链不等同于游戏内致命伤验证。
- 68 项 ProjectE 规则检查、28 项物品/终端回归检查通过。
- `probe --totem-boundaries-only`：镜头生命周期、可选装备视图和存活移动边界检查通过；未声称全量 probe 通过。
- 原生只读检查：当前图腾 `IsAtBindablePlace=true`，`BindResult.Run(..., simulate:true)` 成功，前后实际绑定一致。未修改玩家快捷栏。
- ProjectE 1.2.4 已热加载，generation 2，revision `r20261115105536-af4044c5`；62 个包文件，61 个资源文件与上一版一致。
- 辅助核心 generation 3，331 步完成，ready=true，无加载错误。SHA256：`F1551BD7152E167C2ED1D4550212995B93841F74C3D89776D35AC8B94DC8EB66`。
- 辅助核心备份：游戏目录 `mod-backups/totem-lifecycle-20261115-105537416`；ProjectE 发布器保留 previous-manifest 和旧 release。

实际致命伤后的存活、动画和报错表现仍需用户在游戏内确认。
