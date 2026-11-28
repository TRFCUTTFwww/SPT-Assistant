# Startup notifications and kill effect defaults — 2026-11-28

- Start feature-toggle notifications after all startup config bindings, language setup and overlay construction. BepInEx emits `SettingChanged` while restoring saved settings; those startup events no longer appear as manual toggles.
- Prevent duplicate subscriptions when initializing notification handling again, detach the previous config when switching, and stop notifications before module teardown.
- Default firearm kill effects and the center kill marker to off. Existing saved choices remain valid.
- Regression checks use the actual BepInEx config parser, exercise manual toggles and repeated initialization, and inspect the compiled startup order and both defaults.

## 中文

- 修复启动时重复弹出“枪械击杀特效”“准星击杀反馈”等开关通知：配置恢复触发的 `SettingChanged` 不再当作用户操作。
- 通知监听移至全部初始化完成后；重复初始化不重复订阅，卸载前解除监听。
- 枪械击杀特效与准星击杀反馈默认关闭，保留已有保存值。

游戏内界面与实际战局效果由用户检查。
