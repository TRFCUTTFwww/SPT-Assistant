# 聊天与命令 / Chat and commands

**不稳定。尚未经过广泛测试。** 当前适配 SPT 5.0.0 BE / EFT 1.3.0.1.48558、Windows 11 或支持 TLS 1.3 的 Windows。以最新 SPT 为开发目标，不保证向后兼容。当前改动属于 main，旧 `multiplayer-fika` 分支未更新。

**Unstable; not broadly tested.** Targets SPT 5.0.0 BE / EFT 1.3.0.1.48558 and Windows with TLS 1.3 support. Development follows current SPT; backward compatibility is not guaranteed. This implementation belongs to main; the old `multiplayer-fika` branch is unchanged.

## 安装与操作 / Installation and controls

1. 备份存档和配置。构建 `esp-cover/Esp.csproj` 和 `gear-mail/GearMail.csproj`，或使用包含这次变更的安装包。主客户端仍使用单人版热加载宿主。
2. 客户端模块 `Local.Spt5.ESP.Core.dll` 复制为 `BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module`。热加载宿主已安装时可使用既有 reload.request 流程；否则重启客户端。
3. 关闭服务端，将 `LocalGearMail.dll` 放入 `SPT_Runtime/user/mods/LocalGearMail/`，重启服务端。**本地斜杠命令不依赖聊天中继**；跨客户端文字聊天需要所有人连接到安装此服务端组件的同一 SPT 服务端。
4. 战局画面中按 **T** 打开聊天，**/** 直接输入命令；Enter 提交，Esc 关闭，↑/↓ 历史，Tab / Shift+Tab 循环候选。T 可在 **开发工具 → 聊天与命令** 修改。原生库存/菜单显示鼠标时不会抢 T 键；菜单里的“打开聊天”可在战局外打开。
5. Enter 提交后保持聊天框打开、清空输入；空行 Enter 不关闭，Esc 关闭。命令报错时保留输入以便修改。Tab 补全后光标停在补全内容末尾，不自动全选；移动光标后重新按当前参数补全。
6. 输入 `/` 命令时，输入框上方实时显示用法、当前参数和候选，当前参数使用强调色。带引号的名称、选择器、子命令和坐标均按当前光标位置提示。无需先按 Tab。
7. 菜单顶部 **布局** 中的 **聊天框** 可拖动位置、拖右下角调整宽高；**视觉 → 界面布局** 或 **设置与热键 → 主题与布局 → 各悬浮窗不透明度 → 聊天框** 可调整背景透明度。文字保持清晰；尺寸、位置和透明度自动保存，关闭后的消息也使用相同布局。
8. 中文通过 Unity 的 IME 文本输入。消息最多 2000 字符，聊天仅在内存保存 200 行；命令历史最多 100 项。关闭后最近消息逐渐淡出。`//hello` 发送以 `/` 开头的普通文字。

Back up profiles/config first. Build the client and GearMail projects with the matching game references. Install the client DLL as `Local.Spt5.ESP.Core.module` in the existing host's runtime directory. Install `LocalGearMail.dll` under the server's `user/mods/LocalGearMail/` directory while the server is stopped, then restart it. Local commands work without the relay. Chat requires the same upgraded SPT server. Press T or / in the raid view; use Enter, Esc, history arrows and Tab/Shift+Tab. Change the binding or open chat outside raids under **Developer tools → Chat and commands**. Unity IME handles Chinese input. `//` escapes a leading slash.

Enter keeps chat open and clears the input; failed commands retain the input for editing. Esc closes it. Tab leaves a collapsed caret after the completion. Syntax, the active argument and suggestions appear above the input as you type or move the caret. In the top **Layout** editor, drag **Chat window** to move it and drag its bottom-right corner to resize. Its background opacity is under **Visuals → Interface layout** or **Settings & hotkeys → Theme & layout → Window opacity**. Text stays readable, settings persist, and faded messages share the same bounds.

## 身份核验与加密 / Identity verification and encryption

每人执行 `/identity` 查看自己的档案 ID 和完整 SHA-256 证书指纹，**通过另一个可信渠道**核对双方指纹。核验示例：

```text
/identity verify <对方档案ID> <对方的完整64位十六进制指纹>
/msg "朋友的名字" 测试消息
/reply 收到了
```

双方都必须核验。在线列表展示的指纹来自中继，复制它本身不构成身份核验。指纹改变时拒绝连接；不会自动接受新密钥。普通聊天会为每个在线收件人分别建立点对点加密通道；存在未完成双方核验的在线收件人时不发送全体消息，可向已核验对象发送私信。

Each person runs `/identity` and compares the full fingerprint through a separate trusted channel. Both must verify. Merely copying the fingerprint shown by the relay does not verify it. Changed keys fail closed. Broadcast chat encrypts a separate copy for every online recipient; broadcasts stop if any recipient is not mutually verified. Private messages can target a verified recipient by unique name or profile ID.

- 使用系统 `SslStream` **双向 TLS 1.3**，固定证书指纹，禁用会话恢复，重新连接时使用新 ECDHE 握手；不回退到明文或 TLS 1.2。加密 hello 绑定双方档案 ID 和本次连接 ID。
- 长期 ECDSA P-256 身份证书用于身份认证；Windows DPAPI 以当前 Windows 用户加密本地 PFX。位置：`BepInEx/plugins/LocalSpt5ESP/chat/<档案哈希>/`。更换 Windows 用户/系统可能需要重新建立身份并让朋友重新核验；不要公开此目录。
- 提供**会话级前向保密**。未来泄露长期身份密钥不应解密已结束会话的录制流量。未实现逐消息 Double Ratchet，也不提供入侵后自动恢复；当前进程、内存或 Windows 账户遭入侵时不能保护消息。
- 服务端只能转发 TLS 字节，但能看到档案、名字、证书、核验关系、收件人、时间和大小，能够阻断或延迟流量。收件人可以复制、截图和转发明文。
- 不支持离线消息、历史同步或端到端送达回执。不持久化明文聊天。发送结果不确定时不会自动重试；“已发送”只表示本地加密写入完成，不证明对方看到。
- 有签名请求、时间窗、nonce 防重放、连接与档案绑定、单条大小/速率/队列限制、确认游标和断线关闭。SPT 的档案会话本身不是互联网账户认证；不要把它视为强登录机制。TLS 指纹核验保护聊天内容，不能修复 SPT 会话认证。

The implementation uses platform mutual TLS 1.3, pinned certificates, no resumption, fresh ECDHE on reconnect, and encrypted binding of both profiles/connections. It provides **session-level forward secrecy**, not a per-message Double Ratchet or post-compromise recovery. Private keys are protected by current-user Windows DPAPI; protect the chat directory and verify again after identity changes. The relay sees metadata and can deny service. Participants can retain plaintext. There is no offline delivery, history sync or end-to-end delivery receipt, and uncertain sends are never retried automatically. SPT session IDs are not a substitute for strong internet account authentication.

## 命令 / Commands

`/help [command]` 是当前实际目录。**只有本机输入的命令可以执行**；解密收到的 `/kill` 等文字始终作为聊天展示。世界命令只在单人战局执行；检测到 Fika 程序集或远程玩家会拒绝修改。旧多人权限系统没有合并进来。

`/help [command]` lists current syntax. **Only locally entered commands execute.** Decrypted messages remain text, including `/kill`. World commands require a single-player raid and reject Fika/remote-player worlds. The old multiplayer permission system is not merged into main.

| 类别 / Category | 命令 / Commands |
| --- | --- |
| 聊天 / Chat | help, list, msg/tell/w, reply/r, me, clearhistory, identity, chat reconnect |
| 定位 / Movement | tp/teleport, position, back, spreadplayers, marker, navigate |
| AI / Actors | inspect, kill, damage, heal, effect give/clear, summon, tag, rename, ai pause/resume/follow/look/pose/relation/anchor/remove |
| 编辑 / Editing | edit, copy, paste, remove, data get/modify, attribute |
| 物品 / Inventory | give, clear, item replace/modify, loot spawn/give, repair, reload |
| 环境 / Environment | time, weather, gamerule, raid time/extract, airdrop summon, btr status/route/stop |

示例 / Examples:

```text
/list entities
/tp @s ~ ~2 ~
/tp @e[type=boss,limit=1] marker:training
/inspect "boss:Big Pipe" effects
/damage @e[type=scav,distance=..30,sort=nearest,limit=1] 20 Bullet Chest
/effect give @s PainKiller 120 1 Chest
/summon assault ~ ~ ~5 2 1
/tag @e[type=scav,distance=..20] add training
/rename @e[tag=training,limit=1] "Test AI"
/ai pause @e[tag=training]
/ai relation @e[tag=training] neutral
/ai resume @e[tag=training]
/edit on
/copy @selected
/paste ~ ~ ~4
/data modify entity @e[type=scav,limit=1] invincible set value true
/attribute @e[type=scav,limit=1] damageMultiplier base set 1
/give @s item:5449016a4bdc2d6f028b456f 100000
/loot spawn ~ ~ ~2 item:5449016a4bdc2d6f028b456f 1000
/item modify entity @s Backpack foundInRaid true
/reload @s FirstPrimaryWeapon item:<ammo-template-id>
/time set noon
/raid time add 15m
/weather rain 120
/gamerule preventTimeout true
/marker set training
/navigate marker:training
```

唯一名称可以省略前缀；重名拒绝静默选择，使用 `player:`、`ai:`、`boss:`、`pmc:`、`scav:`、`usec:`、`bear:`、`follower:`、`raider:`、`rogue:`、`cultist:`、`zombie:` 或 `uuid:`。UUID 支持无歧义前缀。玩家 UUID 由档案 ID 派生；AI UUID 在本机战局内稳定，跨热加载/战局不保证保留。`@p` 为最近的其他人类玩家；单人战局通常没有匹配。`@selected` 复用编辑器的 AI 选择；编辑器的物品复制/删除也可在不传 AI 参数时使用 `/copy`、`/remove @selected`。不支持物品、尸体的统一实体选择器。

Names must be unique or explicitly qualified. UUID prefixes must be unambiguous. AI UUIDs are local to this raid/module lifetime. `@p` means the nearest other human, so it normally has no match in single-player. `@selected` uses editor AI selection; `/copy` without an argument and `/remove @selected` can reuse the editor's item selection. Unified item/corpse entity selectors are not implemented.

Tab 按当前参数位置提供候选：对象、效果、装备槽、规则、地点和物品模板。物品先输入部分名称或 `item:` 后的部分 ID；使用模板 ID 可避开本地化名称重名。当前命令反馈以中文为主，聊天设置和本说明提供英文。

Tab candidates follow the current argument: entities, effects, slots, rules, places and item templates. Type part of an item name or ID first; template IDs avoid localized-name ambiguity. Command feedback is currently primarily Chinese; chat settings and this guide include English.

坐标支持绝对、`~` 相对和 `^` 镜头局部坐标；`^` 不可混合使用，正 `^x` 为镜头左侧。地点支持 `room:`、`valuable:`（房间别名）、`exit:`、`saved:`、`marker:`、`airdrop:`、`interaction:`、`quest:`，以及 `btr`。地图标签依赖本机可用的地图数据和实时扫描，不保证每张地图都有房间/任务标签。传送到 AI 空中位置会固定 AI，`/ai resume` 解除。

Coordinates accept absolute, relative `~`, or camera-local `^` values, with positive local X pointing left. Local and world coordinates cannot be mixed. Place labels depend on installed map data and current scans. Airborne AI are anchored; `/ai resume` releases them.

## 已知限制 / Known limits

- `/experience`、`/xp` 目前只支持 query，战局内写入拒绝，防止与结算冲突。等级修改使用战局外原生命令控制台。
- `/teammsg`、`/say` 和远程 permission grant/revoke 未启用：单人版没有经过验证的队伍频道、服务器公告签名或远程执行权限。`/permission` 只读展示。中继配置中的 admin 不会赋予修改别人游戏的能力。
- `item replace` 当前只对空槽工作；已占用槽会明确报错。`reload` 补充已收起枪械的弹匣，空弹匣必须指定兼容弹药；不改变弹膛、原有混装和原生动画。`loot give` 使用已保存的 AI 场景装备，非武器目标槽需先清空，枪械通过现有异步换枪队列处理。
- 批量操作使用现有原生流程，可能部分完成，不提供跨角色事务回滚。物品最多生成100个堆叠，地面物品最多32个堆叠。清空容器同时移除内容物。
- `weather thunder` 设置浓云和最大降雨，不承诺原生闪电。`btr stop` 沿用现有停留接口，停留60秒。`ai follow/look` 目标限本地玩家。属性/数据写入仅支持已列出的字段，禁止任意反射执行。
- `/time` 使用 MC 昼夜 ticks（0=06:00），与 `/raid time` 剩余秒数分开；系统时钟同步等既有设置可能覆盖手动时刻。
- UI、IME、原生输入拦截及每条命令的战局效果仍需实机验证；构建、自动化和热加载成功不等同于这些效果已验证。

Experience is query-only during raids. Team chat, server announcements and remote permission mutations are not enabled. Item replacement requires empty slots. Reload fills a stowed weapon's magazine and requires explicit compatible ammo for empty magazines. Batch native operations can partially complete and do not provide transactional rollback. Weather thunder is heavy rain/cloud cover; BTR stop pauses for 60 seconds. AI follow/look targets the local player. Data/attribute writes use an explicit field allowlist. Game time and raid countdown are separate. UI/IME/input interception and live command behavior still require in-game verification.

## 服务端策略 / Server policy

首次合法连接创建 `SPT_Runtime/user/mods/LocalGearMail/chat-permissions.json`：

```json
{
  "admins": [],
  "blocked": []
}
```

条目使用 `{"profile":"档案ID","fingerprint":"完整大写SHA256指纹"}`。两项必须同时匹配。blocked 可即时撤销该身份聊天中继权限；admins 仅返回 admin 标记，**不提供远程修改游戏能力**。没有首位用户自动管理员，没有与旧版共享密钥的兼容。错误配置拒绝服务，不退回宽松权限。

Each entry binds both `profile` and the uppercase full certificate `fingerprint`. Blocked identities immediately lose relay permission. Admin entries only expose a role marker; they grant no remote game-control ability. There is no first-user admin and no reuse of the retired multiplayer secret.

## 验证 / Validation

本次输入与布局更新通过客户端 Release 编译（0 错误、两条既有空引用警告）、119 项聊天检查和 127 组 Probe 检查。新增检查覆盖输入时预览、Tab 正反向循环、移动光标后重新补全、引号/选择器与后续参数保留、子命令分支和坐标参数提示。游戏内焦点、IME、绘制、缩放和透明度由用户验收。

The input/layout update passes the Release client build (zero errors, two existing nullable warnings), 119 chat checks and 127 Probe groups. Added checks cover live suggestions, completion cycling, caret movement, quoted/selector token replacement, preserved following arguments, subcommands and coordinate hints. Focus, IME, rendering, resizing and opacity still require in-game acceptance.

`dotnet run --project chat-tests/Tests.csproj -c Release` 检查签名、防重放、档案绑定、双向核验、游标确认、撤销、队列上限、TLS 指纹失败关闭、加密上下文绑定、重新握手、Unicode 往返和命令语法。可选的 `-- --http <本机档案ID>` 使用 curl 和 EFT 48558 原生帧格式验证本机 `https://127.0.0.1:6969` 的真实中继，不修改档案。端到端消息仍通过固定指纹的双向 TLS；测试的外层 localhost 自签名 HTTPS 使用 `--insecure`。本机实际接口和双向加密消息测试已通过。

The test project covers signed requests, replay/profile isolation, mutual verification, acknowledgements, revocation, bounded queues, certificate/context rejection, fresh handshakes, Unicode round trips and syntax validation. Optional `-- --http <local-profile-id>` uses curl with EFT 48558 framing against the live local relay, without save changes. Only that test's outer localhost HTTPS ignores its self-signed certificate; the inner mutual TLS still pins both identities. The live endpoint and bidirectional encrypted messages have passed locally. In-game visual/input and command effects remain manual checks.
