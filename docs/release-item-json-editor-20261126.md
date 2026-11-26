# 物品 JSON 编辑 / Item JSON editor

2026-11-26，2.4 开发更新。

后续修正：右键按钮创建后重置原生交互状态，避免沿用对象池中的灰色样式。JSON 格式化明确使用 LF，粘贴内容同步规范换行并修正光标和选区，避免 Windows CRLF 与原生编辑框的字符位置错位。增加 `HpPercent: 600 → 6000` 的光标输入、选区替换和原生保存回归；语法报错增加行列位置。

服务端 JSON 和“数据 → JSON 编辑”同步采用同一套粘贴、光标和选区处理；服务端节点预览及格式化输出也统一 LF。

物品 JSON 现为原生扁平数组，包含根物品和通过 `parentId` 关联的所有配件、膛内子弹、弹匣内子弹及嵌套容器内容物。之前只筛选根物品，遗漏了这些独立记录。已有子物品可一并编辑；编号、模板、位置和树成员保持锁定。修订号覆盖全部子物品，任一成员变化或增减都要求主动刷新，草稿继续保留。服务端先完成每条记录的原生转换，再整体替换，落盘后核对整棵树。客户端先校验所有待改子物品，再通过原生反序列化应用。

物品树补充验证：30 项检查覆盖枪械配件、弹匣和膛内子弹、嵌套容器、子物品修改、无关物品保留、旧修订、错误输入及原子保存；13 项隔离目录内的实际 SaveServer 检查包含整棵树的磁盘保存和回读。客户端与服务端 Release 构建通过。

物品树版本已热加载：客户端第 7 代、服务端管理模块第 9 代，均为 `ready=true`，加载哈希与编译产物一致。本机只读接口检查成功返回 PMC 25 条、Scav 20 条的完整子树和有效修订号；未修改玩家库存。

在库存右键选择“编辑”，现有窗口可切换“参数”和“JSON”。JSON 页签支持直接编辑、多行滚动、格式化、丢弃草稿并刷新和保存。两个页签各自保存当前页的编辑内容。

- 战局外读取并保存当前 PMC 或 Scav 的完整物品树，包含服务端自定义扩展字段；保存前备份角色数据，保存后回读磁盘核验。
- `_id`、`_tpl`、`parentId`、`slotId`、`location` 可查看，保持锁定。保存只替换目标树，其他库存物品不变。
- 物品在编辑期间已有变化时拒绝旧修订保存，草稿保留，玩家可主动刷新；无关的角色或其他物品变化不会阻止保存。
- 普通 UPD 修改通过游戏原生 `ItemDeserializer` 应用，先在临时物品上校验。字段删除等需要重新加载的更改先写入存档，再提示重新进入角色。
- 单人战局内可修改原生支持的 `upd`，随战局正常结算；不直接修改服务端战局外库存。FIKA 沿用原有右键编辑限制。
- JSON 类型、重复字段、大小和目标身份均有校验。原生服务端不能原值保存的字段拒绝写入；持久化校验失败时回退。

验证：客户端和服务端 Release 构建、原有物品参数及狗牌测试、49 项物品 JSON 数据检查、537 项服务端 JSON 检查，以及独立临时目录内的实际 SaveServer 磁盘写入测试。

本机修正版客户端热加载第 5 代、服务端管理模块第 8 代均为 `ready=true`，加载哈希与编译产物一致。新物品读取接口与修订号检查通过；在线验证仅执行读取，未修改玩家库存。

The inventory right-click **Edit** window now offers **Properties** and **JSON** tabs. The JSON tab supports multiline editing, formatting, explicit draft discard/refresh and saving. Each tab saves its own edits.

Item JSON now uses the native flat array containing the selected root and every descendant linked by `parentId`: attachments, chambered rounds, magazine ammunition and nested container contents. The previous implementation filtered out every record except the root. Existing descendants can be edited together; IDs, templates, placement and tree membership remain locked. Revisions cover the whole tree, including added or removed descendants. Server conversion completes for every record before replacing the tree, and disk verification covers all descendants. Native client changes are prepared for all affected items before application.

Additional validation: 30 item-tree checks and 13 isolated real SaveServer/disk checks, including complete tree persistence and readback. Client/server Release builds pass.

The tree update is loaded locally: client generation 7 and server admin generation 9 are ready with matching build hashes. Read-only API checks returned complete PMC and Scav subtrees of 25 and 20 records with valid revisions.
- Outside raids, the editor reads and saves the full PMC or Scav item tree, including server extension fields. It backs up character data and verifies the persisted tree after saving.
- Item ID, template, parent, slot and location remain visible and locked. Other inventory records are preserved.
- A changed target item rejects a stale revision while retaining the draft. Unrelated profile changes do not block saving.
- Native UPD changes use the game's `ItemDeserializer`, with validation on temporary items before applying live changes. Field deletions are persisted and require reloading the character.
- Solo raids support native UPD edits through normal raid settlement. Existing FIKA restrictions remain in effect.
- Invalid types, duplicate fields, oversized documents and identity changes are rejected. Server conversions must preserve the submitted JSON exactly; failed persistence checks trigger rollback.

Validation covers client/server Release builds, existing item and dogtag checks, 49 item JSON checks, 537 server JSON checks and real SaveServer persistence in an isolated temporary directory.
