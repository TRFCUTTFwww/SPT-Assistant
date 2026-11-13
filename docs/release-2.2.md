# SPT Assistant 2.2

黑墙脉冲合并至辅助 Mod，无需独立插件。保留 0.2.10 已验收的单右臂 CPU 蒙皮、共享红光、视角显示修复、随机死亡音效与音量增益，以及确认击杀后才发声的规则。

- 入口：模型与镜头 → 趣味功能 → 黑墙脉冲。F11 施放，Alt+F11 无伤静音预览，快捷键可配置。
- 安装包随附黑墙需要的动作、贴图与声音，并验证每个文件的 SHA256。资源安装到辅助目录的 Blackwall/Assets。
- 安装器备份并停用旧独立黑墙目录，保留辅助设置；运行时防止旧独立运行时与内置能力同时执行。辅助热更新统一管理黑墙生命周期。
- 辅助面板、模块和安装包统一为 2.2。保留现有编辑菜单与热更新输入恢复代码。

适配 SPT 5.0 BE / EFT 1.3.0.1.48558。关闭游戏后运行 Install.ps1；只更新客户端可加 -ClientOnly，服务端可保持运行。先启动服务端，再从 SPT 启动器启动游戏，右 Shift 打开辅助。

独立 0.2.10 的各视角显示已由用户确认；2.2 合并后的战局画面、声音和热更新仍需用户验收。具体验证记录见 validation-2.2.json。

## English

Blackwall Pulse is now built into Assistant 2.2. The package includes its required animation, textures and audio, installed under LocalSpt5ESP/Blackwall/Assets. The installer verifies hashes and moves the retired standalone BlackwallPulse directory outside BepInEx's plugin tree with rollback on failure. Existing Assistant Blackwall settings are retained.

Use Fun Features → Blackwall Pulse. F11 casts; Alt+F11 previews the gesture without damage or sound. Retain the verified 0.2.10 CPU-skinned right arm, shared glow mesh, varied death audio and confirmed-kill audio gating. The integrated 2.2 raid behavior still requires in-game confirmation.