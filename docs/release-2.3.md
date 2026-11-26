# SPT Assistant 2.3

Target: SPT 5.0 BE / EFT 1.3.0.1.48558.

## 功能与入口

- **载具 → BTR 控制**：免费乘车、免费物品转移、秒转移、扩展转移仓（10×100，共 1000 格）。默认均关闭，使用原生司机交互与库存交易。
- **叫车**：按玩家水平距离寻找最近的可达站点，使用现有道路调度；行驶中完成当前路段再改道。车辆已在所选站点时停留等候；车辆未入场或在车库时提示等待。
- **秒转移**：原生 SPT 在战局结算时提交转移物品；收到本次 BTR 配送数据后立即走原生邮件交付，不再等待配送计时器。只加速本次 BTR 包裹，不提前发送过图包裹或其他历史配送。领取期限仍按原版。需要房主服务端 2.3。
- **转移仓扩容**：创建新仓时使用 10×100。已创建的仓不强改尺寸，以保留物品位置；下一局生效。保留 10 格宽度，增加纵向容量。
- **设置 → 版本更新**：启动后后台检查正式 GitHub Releases，成功后 24 小时不重复自动检查；可手动重试。代理只用于更新检测，支持 HTTP、HTTPS、SOCKS5，地址为空使用系统代理。无 API 密钥，不自动安装。
- 3-X 武器、注册和独立客户端代码已移除。

## 迁移与安装

Fika 的服务端设置及叫车应由房主控制。客户端本地代理不影响游戏服务器连接。当前没有声称完成多人战局验证。

## Upstream repositories

GitHub update API: https://docs.github.com/en/rest/releases/releases#get-the-latest-release

## Validation

Builds, boundary checks, isolated server startup and package verification are recorded in `validation-2.3.json`.
