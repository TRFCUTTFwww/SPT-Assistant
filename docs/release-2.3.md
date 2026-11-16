# SPT Assistant 2.3

Target: SPT 5.0 BE / EFT 1.3.0.1.48558.

## 功能与入口

- **载具 → BTR 控制**：免费乘车、免费物品转移、秒转移、扩展转移仓（10×100，共 1000 格）。默认均关闭，使用原生司机交互与库存交易。
- **叫车**：按玩家水平距离寻找最近的可达站点，使用现有道路调度；行驶中完成当前路段再改道。车辆已在所选站点时停留等候；车辆未入场或在车库时提示等待。
- **秒转移**：原生 SPT 在战局结算时提交转移物品；收到本次 BTR 配送数据后立即走原生邮件交付，不再等待配送计时器。只加速本次 BTR 包裹，不提前发送过图包裹或其他历史配送。领取期限仍按原版。需要房主服务端 2.3。
- **转移仓扩容**：创建新仓时使用 10×100。已创建的仓不强改尺寸，以保留物品位置；下一局生效。保留 10 格宽度，增加纵向容量。实际面板滚动及服务交易仍需战局验证。
- **设置 → 版本更新**：启动后后台检查正式 GitHub Releases，成功后 24 小时不重复自动检查；可手动重试。代理只用于更新检测，支持 HTTP、HTTPS、SOCKS5，地址为空使用系统代理。无 API 密钥，不自动安装。
- **趣味功能 → 像素剑 / ProjectE**：MC 客户端与服务端已整合。HUD 总开关、爱心、饱食度、口渴、经验与物品快捷栏可独立控制。原 MC 配置继续沿用。
- 3-X 武器、注册和独立客户端代码已移除。

## 迁移与安装

完整安装需关闭客户端及服务端。安装器验证文件哈希，备份存档和旧代码，将 `atm-swords`、`projecte`、`scp3x` 从 UniversalModHost 的模块目录移至游戏 `mod-backups`，并停用旧 MC 服务端 DLL / 逻辑模块。其余 UniversalModHost 模块与辅助本体重载保留。MC Assets 移至辅助 MOD 内；服务端钱包、知识和终端数据仍保存在原角色存档；注册状态和终端备份改存到 LocalGearMail/Minecraft 内。未完成客户端操作日志复制到内置功能的数据目录，避免迁移丢失恢复信息。再次安装不会覆盖已有的新目录日志。

3-X 在存档中的旧物品不再有有效模组模板，SPT 的原生未知模组物品清理可能移除它们；升级前存档由安装器备份，便于恢复。不要在新旧 MC 服务端加载器同时启用时启动。

Fika 的服务端设置及叫车应由房主控制。客户端本地代理不影响游戏服务器连接。当前没有声称完成多人战局验证。

## Upstream repositories

See the repository links in both READMEs. Original third-party licenses and provenance remain bundled with MC assets.

GitHub update API: https://docs.github.com/en/rest/releases/releases#get-the-latest-release

## Validation

Builds, boundary checks, isolated server startup and package verification are recorded in `validation-2.3.json`. In-raid UI, vehicle arrival, free-service payment, grid scrolling and item delivery require an actual game session and are not established by a successful build.
