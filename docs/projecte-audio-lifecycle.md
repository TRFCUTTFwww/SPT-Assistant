# ProjectE 技能音效间歇失效：1.2.9

用户报告武器充能、范围技能音效时有时无。

## 证据

2026-11-15 客户端 PID 25788、ProjectE 1.2.8 的实际资源缓存中，`pecharge`、`tock`、`petransmute`、`totem` 的 AudioClip 包装对象仍存在，但原生 `samples` 均为 0，hideFlags 为 None。旧 Play 只检查字典是否命中，继续提交没有有效 PCM 数据的缓存。日志另有 `Can not play a disabled audio source`，原实现只在创建时启用音源，之后没有恢复其状态。

## 修复

- 每次播放检查原生音频的存活状态、采样数、声道、采样率及 loadState；缺失或失效时，从原始 WAV 重建并替换缓存。
- 模块拥有的运行时 AudioClip 标记 HideAndDontSave，包含 DontUnloadUnusedAsset，避免仅由托管字典保存的 one-shot 音频被场景资源回收；模块清理仍显式销毁这些对象。
- 每次播放恢复自身 GameObject 和 AudioSource 的启用、静音、增益和音高，保持二维反馈、提高短反馈音的优先级，并允许其跨原生监听器暂停播放。
- 不修改 AudioListener、全局音量、全局音频暂停或引擎声音数量设置。ProjectE 配置音量继续作为 PlayOneShot 音量参数使用。

## 验证

构建零警告、零错误；12 项音频生命周期检查、51 项图腾检查、524 项终端/HUD 检查通过。

当前客户端实际热加载 1.2.9。直接调用真实 ProjectResources.Play 后，`pecharge` 为 36,352 个采样、Loaded、HideAndDontSave，音源 isPlaying=true、isVirtual=false。将模块自己的音源停用、静音、音量设为 0、音高设为 0.5，并停用其根对象，再调用同一播放路径，以上状态均自动恢复并成功播放。测试不发动技能、不伤害角色、不消耗物品。

实际听感及多次换局后的持续表现仍以游戏复测为准；音频引擎报告正在播放不等同于终端设备的听感验收。

```powershell
dotnet build projecte-client/Client.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558
dotnet run --project audio-tests/Tests.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558 -- projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll
```
