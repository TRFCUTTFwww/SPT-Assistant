# 不死图腾触发后免伤：ProjectE 1.2.8

用户反馈图腾触发后很快再次死亡，要求默认 10 秒无视伤害，并能在趣味功能设置中自行调整。

原先 `TotemRescueGate` 仅覆盖恢复过程与触发当帧的伤害连锁，下一帧的新攻击会按正常伤害处理。1.2.8 在此基础上增加按实际时间计算的保护窗口。

## 设置

“趣味功能 → ProjectE → 触发后免伤秒数”，默认 `10`。支持小数和任意有限的正数，不设额外的小上限；`0` 关闭额外保护期，仍保留触发当帧的防重复致死保护。负数或非有限值不生成保护窗口。

配置持久化在 `BepInEx/config/local.projecte.cfg` 的 `[Totem]` 节。修改用于下一次成功消耗图腾；正在进行的保护窗口使用触发时的设置，不因开关菜单而重新计时。

## 实现

- 只有成功消耗一枚图腾并完成原生生命恢复后才提交保护窗口。伤害、扩散、直接负生命值变化、部位摧毁和致死钩子共用同一控制器身份及截止时间。正向治疗正常运行。
- 保护期内后续攻击不重复消耗图腾；截止时刻立即恢复正常伤害判定。已有 45 秒再生和 40 秒火焰抵抗保留。
- 保护绑定本地玩家的实际健康控制器。其他演员不继承；死亡、离开战局或模块清理会清除保护。原有 Existence/Undefined 场景生命周期致死排除保持不变。
- 使用 unscaled time，减慢游戏时间不会延长保护。模块热更新在恢复过程和有效保护窗口内等待，防止切换模块时丢失免伤。
- `totem-status.json` 新增配置秒数、剩余秒数、直接扣血与部位摧毁的拦截计数，并记录保护结束。

## 验证

模块构建零警告、零错误；51 项图腾、快捷栏和结算检查通过，包含 10 秒内 600 次后续攻击、恰好到期、小数时长、0 秒、身份隔离、失败消耗、直接扣血与治疗方向、退出清理及设置 API 暴露。原先 HUD/终端的 524 项回归检查继续通过。

客户端 PID 23072 实际热加载 1.2.8；诊断通过真实设置 API 读取到菜单条目与当前值 10，并确认 ApplyDamage、SpreadOverDamage、Kill、ChangeHealth、DestroyBodyPart 五个钩子均注册。仅对独立创建的门控实例验证倒计时，不向实际角色施加测试伤害，不消耗图腾。原生 Medicine 结算数据仍通过服务端 DTO 往返检查。

```powershell
dotnet build projecte-client/Client.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558
dotnet run --project totem-tests/Tests.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558
dotnet run --project surgery-ui-tests/Tests.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558 -- projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll
```
