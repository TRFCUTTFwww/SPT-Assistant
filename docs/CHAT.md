# 聊天与命令 / Chat and commands

**不稳定。尚未经过广泛测试。** 当前适配 SPT 5.0.0 BE / EFT 1.3.0.1.48558、Windows 11 或支持 TLS 1.3 的 Windows。以最新 SPT 为开发目标，不保证向后兼容。当前改动属于 main，旧 `multiplayer-fika` 分支未更新。

**Unstable; not broadly tested.** Targets SPT 5.0.0 BE / EFT 1.3.0.1.48558 and Windows with TLS 1.3 support. Development follows current SPT; backward compatibility is not guaranteed. This implementation belongs to main; the old `multiplayer-fika` branch is unchanged.

## 安装与操作 / Installation and controls

1. 备份存档和配置。构建 `esp-cover/Esp.csproj` 和 `gear-mail/GearMail.csproj`，或使用包含这次变更的安装包。主客户端仍使用单人版热加载宿主。
2. 客户端模块 `Local.Spt5.ESP.Core.dll` 复制为 `BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module`。热加载宿主已安装时可使用既有 reload.request 流程；否则重启客户端。
3. 关闭服务端，将 `LocalGearMail.dll` 放入 `SPT_Runtime/user/mods/LocalGearMail/`，重启服务端。**本地斜杠命令不依赖聊天中继**；跨客户端文字聊天需要所有人连接到安装此服务端组件的同一 SPT 服务端。
4. 战局画面中按 **T** 打开聊天，**/** 直接输入命令；Enter 提交，Esc 关闭。**↑/↓ 选择候选，Tab 填入，Shift+Tab 反向循环**；使用箭头选中候选后 Enter 也只填入，再次 Enter 才执行。无候选时 ↑/↓ 翻历史，有候选时可用 **Ctrl+↑/↓** 翻历史。候选支持鼠标点击填入、滚轮选择。T 可在 **开发工具 → 聊天与命令** 修改。原生库存/菜单显示鼠标时不会抢 T 键；菜单里的“打开聊天”可在战局外打开。
5. Enter 提交后保持聊天框打开、清空输入；空行 Enter 不关闭，Esc 关闭。命令报错时保留输入以便修改。Tab 补全后光标停在补全内容末尾，不自动全选；移动光标后重新按当前参数补全。
6. 聊天框采用独立标题、滚动消息区、参数提示、双行候选和输入栏。输入 `/` 时实时显示用法、当前参数和候选，当前参数使用强调色，选中候选保持在列表可见范围内。长物品名换行，第二行显示本地名称和 ID。带引号的名称、选择器、子命令和坐标均按当前光标位置提示，无需先按 Tab。
7. 菜单顶部 **布局** 中的 **聊天框** 可拖动位置、拖右下角调整宽高；**视觉 → 界面布局** 或 **设置与热键 → 主题与布局 → 各悬浮窗不透明度 → 聊天框** 可调整背景透明度。文字保持清晰；尺寸、位置和透明度自动保存，关闭后的消息也使用相同布局。
8. 中文通过 Unity 的 IME 文本输入。消息最多 2000 字符，聊天仅在内存保存 200 行；命令历史最多 100 项。关闭后最近消息逐渐淡出。`//hello` 发送以 `/` 开头的普通文字。

Back up profiles/config first. Build the client and GearMail projects with the matching game references. Install the client DLL as `Local.Spt5.ESP.Core.module` in the existing host's runtime directory. Install `LocalGearMail.dll` under the server's `user/mods/LocalGearMail/` directory while the server is stopped, then restart it. Local commands work without the relay. Chat requires the same upgraded SPT server. Press T or / in the raid view; use Enter, Esc, history arrows and Tab/Shift+Tab. Change the binding or open chat outside raids under **Developer tools → Chat and commands**. Unity IME handles Chinese input. `//` escapes a leading slash.

Enter keeps chat open and clears the input; failed commands retain the input for editing. Esc closes it. Tab leaves a collapsed caret after the completion. Syntax, the active argument and suggestions appear above the input as you type or move the caret. In the top **Layout** editor, drag **Chat window** to move it and drag its bottom-right corner to resize. Its background opacity is under **Visuals → Interface layout** or **Settings & hotkeys → Theme & layout → Window opacity**. Text stays readable, settings persist, and faded messages share the same bounds.

The redesigned panel separates the title, scrollable messages, argument preview, two-line suggestions and input. Up/Down selects a suggestion without editing the input; Tab fills it. Enter also fills an explicitly selected suggestion, then a second Enter runs the command. Ctrl+Up/Down always recalls history; plain arrows recall history when there are no suggestions. Click a suggestion to fill it or use the wheel to select. Long item names wrap, and the second line distinguishes localized names and template IDs.

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

`/help [command]` 是当前实际目录。**只有本机输入的命令可以执行**；解密收到的 `/kill` 等文字始终作为聊天展示。现在支持 FIKA 战局内的本地输入命令，按战局角色和目标归属检查，范围见下表。旧多人权限系统没有合并进来。

`/help [command]` lists current syntax. **Only locally entered commands execute.** Decrypted messages remain text, including `/kill`. Locally entered commands now work in FIKA raids, subject to raid role and target ownership as listed below. The old multiplayer permission system is not merged into main.

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
/give @s salewa 1
/give @s IFAK 2
/give @s bitcoin 1
/give @s "Salewa first aid kit" 1
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

物品参数支持**中文名、英文名、简称、名称片段和模板 ID**，无需 `item:` 前缀；例如 `/give @s salewa 1`。英文忽略大小写，多个关键词使用引号，例如 `/give @s "first aid"`，再用 ↑↓ 选择具体候选。输入 `/give @s ` 时即可浏览物品，输入关键词缩小范围；精确名称和简称优先。补全自动给长名称加引号，同名物品自动填入明确的 `item:ID`，列表仍显示可读名称。直接执行的名称若匹配多个物品会提示继续选择，不会随便取第一项。`clear`、`item replace`、`loot spawn`、`reload` 同样支持名称搜索，reload 候选仅含弹药。当前命令反馈以中文为主。

Item arguments accept Chinese/English names, short names, substrings and template IDs without requiring `item:`. Try `/give @s salewa 1`. English matching is case-insensitive; quote multiword searches such as `"first aid"`, then select a result with arrows. Exact names and aliases rank first. Completion quotes full names automatically and uses an explicit ID for duplicate names while displaying readable labels. Ambiguous direct input asks for a selection instead of choosing an arbitrary item. This also applies to clear, item replace, loot spawn and reload (ammo only). Command feedback is currently primarily Chinese.

坐标支持绝对、`~` 相对和 `^` 镜头局部坐标；`^` 不可混合使用，正 `^x` 为镜头左侧。地点支持 `room:`、`valuable:`（房间别名）、`exit:`、`saved:`、`marker:`、`airdrop:`、`interaction:`、`quest:`，以及 `btr`。地图标签依赖本机可用的地图数据和实时扫描，不保证每张地图都有房间/任务标签。命令传送到无落脚点时不再自动固定或暂停 AI，后续移动由游戏原生逻辑处理；可用 `/ai anchor <AI> on` 主动固定。

Coordinates accept absolute, relative `~`, or camera-local `^` values, with positive local X pointing left. Local and world coordinates cannot be mixed. Place labels depend on installed map data and current scans. Command teleport no longer automatically anchors or pauses AI at airborne destinations; native movement continues. Use `/ai anchor <AI> on` to request an anchor explicitly.

日常命令现用 `名称=值`，Tab 提供说明与值补全；复杂物品使用 `/give @s AKM edit` 可视化编辑并保存预设。参数块可点击修改，右上角关闭支持鼠标。地点候选显示原标签对应名称、类型、状态和距离。详细物品、AI、治疗、维修、装填、筛选、朝向和天气配置参见 [详细命令参数 / Detailed options](COMMAND-OPTIONS.md)。聊天外观统一为深色卡片、细边框、青绿色选中状态，消息和错误结果使用独立行背景；操作按键保持原样。新增 JSON 解析与补全检查覆盖嵌套/转义、重复/未知字段拒绝、子命令限制、已有配置保留和长输入。原生物品树、枪械数值和界面效果仍需实机验证。

See the [detailed command options](COMMAND-OPTIONS.md) for item trees, AI and other fine-grained parameters. Chat now uses consistent dark cards, subtle borders, mint selection and distinct message/error rows, retaining existing controls. JSON checks cover nesting/escaping, duplicate/unknown fields, subcommand restrictions, preservation and long input. Native item-tree behavior, weapon tuning and visual results still require in-game acceptance.

## FIKA 命令范围 / FIKA command scope

主分支支持 **FIKA 5.0 客户端接口 / Fika Server 2.4.1**，仍是不稳定功能，尚未完成双客户端战局测试。每位玩家在自己的聊天框输入命令；不需要开放聊天中继 admin。`/permission` 显示本机是房主或成员。运行 SPT 服务端不等于战局房主；使用 headless 时由 headless 持有 AI，本次没有成员向 headless 转发命令。

Main supports the **FIKA 5.0 client API / Fika Server 2.4.1** experimentally. Two-client raid testing is still required. Each player enters commands locally; relay admin is not required. `/permission` reports raid host/member. Running the SPT backend does not make a client the raid host. Headless owns AI when enabled; member-to-headless command forwarding is not implemented.

| 命令 / Commands | FIKA 行为 / Behavior |
| --- | --- |
| `list`, `position`, `inspect`, `data get`, `item inspect`, `clear ... 0`, `time query`, `raid time query`, `btr status` | 查询本机已同步的数据 / Read locally replicated state |
| `marker`, `navigate`, `tag`, `gamerule showRaidClock` | 本地标记、导航与 HUD / Local markers, navigation and HUD |
| `tp`, `teleport`, `back`, `spreadplayers`, `heal`, `effect` | 成员仅可修改自己；房主还可修改本机控制的 AI / Members modify self; hosts may also modify locally controlled AI |
| `summon`, `kill`, `damage`, `ai`（不含 remove/name）, `attribute`, `data modify`（不含 name）, `btr route/stop` | 在战局房主输入，沿用原生 AI/角色流程 / Enter on raid host; use native actor/AI paths |
| 库存写入、物品树生成、改名、编辑器、删除、天气、昼夜写入、剩余时间写入、强制结算 / Inventory writes, item-tree creation, rename, editor, delete, weather, clock writes and forced extraction | 尚未接入完整联机同步，明确拒绝 / Rejected pending complete multiplayer integration |

例如：`/tp player:朋友名字` 把自己传送到朋友处；`/heal @s` 治疗自己；房主可用 `/summon assault count=2`。`/tp @a ~ ~ ~` 会因包含远程队友而整体拒绝，不会先改自己再报错。不能直接修改观察到的队友对象；请让对方在自己客户端执行。房主 AI 控制的行为结果沿用 FIKA 同步，自定义状态、名字和 UI 数值不保证显示在每个客户端；这不是远程管理协议。

Examples: `/tp player:Friend` moves you to that friend, `/heal @s` heals you, and a host can use `/summon assault count=2`. Mixed selections containing remote humans fail before any mutation. Ask peers to execute self-targeted commands on their own clients. FIKA carries native actor state; custom control state and UI values are not guaranteed to match across peers. This is not a remote administration protocol.

## 已知限制 / Known limits

- `/experience`、`/xp` 目前只支持 query，战局内写入拒绝，防止与结算冲突。等级修改使用战局外原生命令控制台。
- `/teammsg`、`/say` 和远程 permission grant/revoke 未启用：当前版本没有经过验证的队伍频道、服务器公告签名或远程执行权限。`/permission` 只读展示。中继配置中的 admin 不会赋予修改别人游戏的能力。
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

本次重做通过 EFT 48558 Release 编译（0 错误、两条既有空引用警告）、208 项聊天检查（含本机真实物品数据库）和 127 组 Probe 检查。界面外观、IME 操作和 AI 传送后的原生运动尚待游戏内验收。

This redesign passes the EFT 48558 Release build (zero errors, two existing nullable warnings), 208 chat checks including the installed item database, and 127 Probe groups. In-game appearance, IME interaction and native AI movement after teleport remain manual acceptance checks.

输入测试覆盖实时预览、Tab 正反向循环、箭头选择后填入、输入修改后清除选择、不同可用高度的候选边界、光标移动、引号/选择器、子命令和坐标提示。物品测试覆盖中英文片段、简称、重名拒绝、弹药筛选及补全与执行一致性；设置 `SPT_GAME_ROOT` 后还会读取本机实际物品和中英文本地化数据库验证。游戏内焦点、IME、绘制、缩放和透明度由用户验收。

Input tests cover live previews, completion cycles, arrow-selection acceptance, edit invalidation, bounded suggestion layouts, caret movement, quoted/selector replacement, subcommands and coordinates. Item tests cover bilingual substrings/aliases, ambiguity rejection, ammo filtering and completion/execution round trips; set `SPT_GAME_ROOT` to also check the installed item and Chinese/English locale databases. Focus, IME, rendering, resizing and opacity still require in-game acceptance.

`dotnet run --project chat-tests/Tests.csproj -c Release` 检查签名、防重放、档案绑定、双向核验、游标确认、撤销、队列上限、TLS 指纹失败关闭、加密上下文绑定、重新握手、Unicode 往返和命令语法。可选的 `-- --http <本机档案ID>` 使用 curl 和 EFT 48558 原生帧格式验证本机 `https://127.0.0.1:6969` 的真实中继，不修改档案。端到端消息仍通过固定指纹的双向 TLS；测试的外层 localhost 自签名 HTTPS 使用 `--insecure`。本机实际接口和双向加密消息测试已通过。

The test project covers signed requests, replay/profile isolation, mutual verification, acknowledgements, revocation, bounded queues, certificate/context rejection, fresh handshakes, Unicode round trips and syntax validation. Optional `-- --http <local-profile-id>` uses curl with EFT 48558 framing against the live local relay, without save changes. Only that test's outer localhost HTTPS ignores its self-signed certificate; the inner mutual TLS still pins both identities. The live endpoint and bidirectional encrypted messages have passed locally. In-game visual/input and command effects remain manual checks.
