# 详细命令参数 / Detailed command options

当前目标：**SPT 5.0 BE / EFT 1.3.0.1.48558**。不稳定，未经过广泛战局测试；仅适配 main 的本机单人命令。旧 Fika 分支不更新。

Current target: **SPT 5.0 BE / EFT 1.3.0.1.48558**. Unstable and not broadly raid-tested. These additions apply to main's local single-player commands; the old Fika branch is unchanged.

日常参数使用 `名称=值`，不用输入 JSON。位置参数写在前面，之后的命名参数可任意排序；字符串有空格时用双引号。Tab 先选择参数名，再选择值；候选显示中文含义。聊天框下方的参数块可以点击修改或移除。

Use `name=value` for daily commands without JSON. Keep positional arguments first, then named options in any order. Quote values containing spaces. Tab selects an argument name and then a value; hints explain their meaning. Click parameter chips below the chat to edit or remove a value.

```text
/give @s salewa 1 fir=true medical=400
/item modify entity @s FirstPrimaryWeapon durability=80 maxDurability=100 ergonomics=80 firerate=750
/heal @s Chest amount=20 clearEffects=false
/ai configure @e[type=scav,limit=1] name="Test AI" paused=true healthPercent=50 pose=crouched playerRelation=friendly
/summon assault count=2 difficulty=hard pos=~,~,~5 health=1000
/weather rain 120 rain=0.4 fog=0.05 clouds=0.8
```

简写：`fir` 带钩、`medical` 医疗余量、`food` 食物余量、`uses` 钥匙次数、`health` 总血量；其余下表的简单字段可直接作为参数名。关系支持 default/friendly/hostile。`pose/follow/look/speed/noHealing/noDoors/noMovement/followDistance/setFacing` 写入 AI 导演设置；`ergonomics/firerate/recoilUp/recoilBack/ignoreAttachments` 写入枪械设置。高级单值路径也支持，例如 `bodyHealth.Chest.maximum=300 bodyHealth.Chest.current=200`、`weapon.values.RecoilForceUp=20`。effect 的类型、时间、强度和部位继续使用原有命令。

Short aliases map to the corresponding fields below. Actor relations accept default/friendly/hostile; scene and weapon shorthands map to existing nested settings. Advanced numeric paths also work, as shown above. Effects retain their existing type/duration/strength/part command syntax.

复杂物品用 `/give @s AKM edit`。打开独立编辑窗口，可以逐层选择附件、编辑组件参数、添加容器内容物、按顺序添加弹药组、设置枪械参数；最后点击生成到命令指定目标。内容物自动安排格子，原生容量和兼容限制继续生效。枪械页修改后需点“保存枪械数值到当前配置”。取消或 Esc 返回聊天，不发放物品。

Use `/give @s AKM edit` for the visual editor. Browse attachment slots, component values, container contents, ordered ammo groups and weapon values, then generate for the original targets. Contents use automatic grid placement and native compatibility/capacity checks. On the weapon tab, save values to the current specification before generation. Cancel/Esc returns to chat without delivery.

窗口中输入预设名并保存，之后用 `/give @s preset:训练AKM 1`；名字带空格可写 `"preset:Training AKM"`。也可用该预设加 `edit` 继续编辑，或追加命名参数覆盖本次生成值。预设存于 `BepInEx/plugins/LocalSpt5ESP/command-item-presets.json`，保存会备份旧文件，与原有物品快捷标签分开。

Save an editor preset and reuse `/give @s preset:TrainingAKM 1`, optionally with `edit` or named overrides. Quote the token for names with spaces. Presets live in `command-item-presets.json` under the Assistant plugin directory, use backup-on-save, and are separate from existing quick item tags.

地点补全保留原标签，显示地点名称、类型、状态/说明和距离，可按名称搜索；确认后仍插入精确 ID。右上角“关闭 · ESC”支持鼠标点击。这里的地点和状态依赖当前战局数据，不伪造未知名称。

Place completion retains exact identifiers while showing names, type, status/details and distance, and can search by readable name. The top-right Close/ESC button is clickable. Place metadata depends on current raid data.

旧末尾 JSON 仍兼容，但不再作为默认交互，也不可与命名参数混用。以下是内部字段和高级结构参考。本地输入上限 16,384 字符，网络文字仍限 2,000；命令仅在本机单人战局执行，Fika 下明确拒绝世界修改。

Legacy final JSON remains compatible as an advanced format, cannot be mixed with named arguments, and is no longer the default UI. The following sections describe underlying fields. Local input allows 16,384 characters; network messages remain limited to 2,000. World commands remain local-single-player only and reject Fika worlds.

## 物品 / Items

`give`、`loot spawn`、`item replace` 支持完整配置。`item modify entity <目标> <槽/附件路径> {配置}` 只允许现有物品的组件、狗牌和枪械数值；不允许重建其附件/内容物/弹药结构。`item replace` 仍仅限空装备槽。旧式单字段 modify 继续可用，不能与 JSON 混用。

`give`, `loot spawn`, and `item replace` accept full specifications. JSON `item modify` edits existing components, dogtags and weapon values, without rebuilding attachments, contents or ammunition. Replacement still requires an empty equipment slot. Legacy single-field modify remains available and cannot be mixed with JSON.

| 字段 / Field | 含义 / Meaning |
| --- | --- |
| `foundInRaid` | 带钩标记；新物品树向下继承，子项可覆盖 / FiR inherited by new descendants, with child overrides |
| `durability`, `maxDurability` | 当前/最大耐久；当前不可超过最大 / Current/max condition |
| `medicalResource`, `foodResource` | 医疗余量、食物余量 / Medical and food resources |
| `keyRemaining` | 钥匙剩余次数，整数 0…2,147,483,647 / Remaining key uses |
| `poisonUses`, `resource`, `repairKitResource` | 毒素、资源、维修工具余量 / Poison, resource and repair-kit reserves |
| `toggledOn` | 兼容物品开关 / Compatible item's toggle |
| `attachments` | 原生槽名 → 子物品对象；`null` 移除可选附件 / Native slot → nested item; null removes an optional attachment |
| `contents` | 容器内容物数组 / Grid contents array |
| `cartridges` | 弹匣内弹药数组，保留指定顺序 / Magazine ammunition array in specified order |
| `weapon` | `values` 数值对象、可选 `ignoreAttachments` / Numeric values and optional attachment-stat suppression |
| `dogtag` | `nickname`, `level`, `side` (Usec/Bear/Savage), `accountId`, `profileId`, `killerName`, `weaponName` |

每个子物品写为 `{"item":"名称或ID","count":1,"options":{…}}`。count 默认 1，必须是单堆叠范围内的整数。contents 还可写 `grid`（原生储物格 ID），`x/y`（从 0 起，必须同时提供），`rotation`（0/1）；不写位置时自动放置。游戏原生容量、过滤器、必需/固定附件限制仍生效。

Nested items use `{"item":"name-or-id","count":1,"options":{…}}`. Count defaults to 1 and must fit one native stack. Contents additionally accept grid ID, zero-based x/y (both required together), and rotation 0/1. Omit position for automatic placement. Native capacity, filters and required/locked attachment rules still apply.

```text
/give @s salewa 1 {"foundInRaid":true,"medicalResource":400}
/give @s item:544a5caa4bdc2d1a388b4568 1 {"foundInRaid":false,"contents":[{"item":"item:5449016a4bdc2d6f028b456f","count":10000}]}
/item modify entity @s FirstPrimaryWeapon {"durability":80,"maxDurability":100,"weapon":{"values":{"Ergonomics":80,"bFirerate":750},"ignoreAttachments":false}}
/item inspect entity @s FirstPrimaryWeapon
/item inspect entity @s FirstPrimaryWeapon/mod_magazine
```

附件结构示例（实际弹匣必须兼容该枪）：

Attachment structure example (choose a magazine compatible with the weapon):

```json
{
  "attachments": {
    "mod_magazine": {
      "item": "<magazine name or ID>",
      "options": {
        "cartridges": [{"item":"<ammo name or ID>","count":30}]
      }
    }
  }
}
```

`/item inspect` 返回模板/实例 ID、参数、附件和直接子物品 ID、枪械可写值。路径支持原生附件槽，以及 `/id:<直接子物品ID>`。可继续追加路径访问更深层。它不是完整的存档导入导出格式。

Inspect returns IDs, component values, attachment data, direct child IDs and allowed weapon values. Paths accept native attachment slots and `/id:<direct-child-id>`, chained for deeper items. Inspect output is not a complete save import/export format.

枪械字段沿用现有编辑器：`RecoilForceUp`, `RecoilForceBack`, `RecoilCamera`, `RecoilReturnSpeedHandRotation`, `RecoilDampingHandRotation`, `Ergonomics`, `bFirerate`, `SingleFireRate`, `CenterOfImpact`, `ShotgunDispersion`, `Velocity`, `Weight`, `Durability`, `MaxDurability`, `HeatFactorGun`, `CoolFactorGun`, `HeatFactorByShot`，以及该型号可读写的 `WeaponRecoilSettings.<字段>` / `WeaponAimSettings.<字段>` 数字属性。Velocity 最低 -100，射速最低 1；其余基础字段非负。嵌套数值取决于当前原生模型。模板 Durability 与物品组件 durability 是不同字段。

Weapon values reuse the editor's allowlist above, including model-specific numeric recoil/aim properties. Velocity has a minimum of -100, fire rates 1, other basic values 0. Nested properties follow the installed native model. Template `Durability` differs from component `durability`.

**枪械参数仅本战局的实例副本生效，不持久化进存档；本局上限 512 把。**不影响未修改的同型号枪械，但既有无后座等功能可能覆盖最终表现。

**Weapon tuning changes a per-instance template copy for this raid only, capped at 512 guns. It is not persisted in the save.** Untuned guns retain their template. Other active weapon features may override final behavior.

## AI / Actors

`/summon <角色|template:场景名> [x y z] [数量] [难度0-3] {配置}` 与 `/ai configure <AI> {配置}`：

| 字段 / Field | 规则 / Rules |
| --- | --- |
| `name` | 名字 1…64 字符 / Name |
| `totalHealth`, `healthPercent` | 总血量按原部位比例缩放；百分比 (0,100]；生成时 totalHealth=0 保留原生值 / Proportional total HP and current percentage; summon total=0 retains native HP |
| `bodyHealth` | 部位 → `{"maximum":300,"current":150}`；current 默认 maximum，覆盖比例设置 / Per-part overrides, current defaults to max |
| `paused`, `invincible`, `cleanEffects` | 暂停、无敌、清理效果 / Pause, invincibility, clear effects |
| `playerRelation`, `aiRelation` | 0 默认（清除该个人覆盖）、1 友好、2 敌对；现有 AI 未指定的一类保持 / Default, friendly, hostile; omitted categories preserved |
| `effects` | 最多 32 项：`type`、`part`、`seconds` (1…86400)、`strength` (0.01…1000000) / Up to 32 effects |
| `scene` | `pose` (Original/Standing/Crouched/Prone), `yaw`, `pitch`, `setFacing`, `noDoors`, `noHealing`, `noMovement`, `look`, `follow`, `followDistance` (1…30), `speed` (0.1…5) |

部位：Head / Chest / Stomach / LeftArm / RightArm / LeftLeg / RightLeg。效果类型复用 `/effect give` 补全；骨折仅限四肢。跟随/注视对象仍为本地玩家。装备使用 `template:场景名`、`loot give` 或物品命令，不接受自造 equipment 字段。生成仍经过原生异步队列，并非瞬间生成。

Body parts are listed above. Effects reuse `/effect give` types; fractures require a limb. Look/follow targets the local player. Use saved templates/loadouts or item commands for equipment. Spawning still uses the native asynchronous queue.

```text
/ai configure @e[type=scav,limit=1] {"name":"Test AI","healthPercent":50,"paused":true,"scene":{"pose":"Crouched","noHealing":true}}
/summon assault ~ ~ ~5 1 1 {"totalHealth":1000,"playerRelation":1,"bodyHealth":{"Chest":{"maximum":400,"current":300}},"effects":[{"type":"PainKiller","part":"Chest","seconds":120,"strength":1}]}
```

## 其他命令 / Other commands

| 命令 / Command | JSON 配置 / Options |
| --- | --- |
| `heal [目标] [部位]` | `amount`（增加血量）或 `percent`（设为最大值百分比，0…100），不能同时使用；`clearEffects` 默认 true；`hydration` / `energy` 设目标值并限制到角色最大值。空对象恢复指定血量；仅写水分/能量时不改血量 / Add HP or set percent; clear effects defaults true; hydration/energy are capped target values; empty object heals, hydration-only does not change HP |
| `repair <目标> [槽/路径]` | `durability` 或 `percent`；`maxDurability`；`recursive` 默认 true，含所选物品附件/内容物 / Condition or percent, max condition, recursive descendants |
| `reload <目标> [槽] [弹药]` | `count`：本次最多新增的发数，1…100000；仍须收起枪械 / Maximum rounds to add; weapon must be stowed |
| `clear <目标> [物品] [最大数量]` | `foundInRaid`、`durabilityMin` / `durabilityMax`（含边界）、`recursive` 默认 true；数量 0 只查询。删容器仍会带走内容物 / FiR/condition filters and recursive search; count 0 queries only; removing containers removes contents |
| `tp/teleport [来源] <目标或坐标>` | `yaw` (-360000…360000)、`pitch` (-89…89)；不可同时使用 facing；AI 固定朝向沿用导演控制 / Rotation, mutually exclusive with facing; AI rotation uses director control |
| `weather clear/rain/thunder [秒]` | `rain` (0…1)、`fog` (0…1)、`clouds` (-1…1)；定时命令覆盖另一条定时命令后仍恢复最初状态 / Weather overrides; repeated timed overrides retain the original restore state |

```text
/heal @s Chest {"amount":20,"clearEffects":false}
/repair @s FirstPrimaryWeapon {"percent":90,"maxDurability":100,"recursive":true}
/reload @s FirstPrimaryWeapon M855A1 {"count":10}
/clear @s salewa 0 {"foundInRaid":true}
/tp @s ~ ~2 ~ {"yaw":90,"pitch":0}
/weather rain 120 {"rain":0.4,"fog":0.05,"clouds":0.8}
```

`effect` 继续用已有的类型/部位/秒数/强度参数，`data/attribute` 仍只允许明示字段；复杂 AI 状态用 `ai configure`。其余命令不会接受并忽略任意 JSON。

Effect retains its existing type/part/duration/strength arguments. Data/attribute remain allowlisted; use AI configure for detailed actor state. Unsupported commands reject extra JSON.

## 边界 / Boundaries

- 新物品树会在首次发放前完成构造检查；每次最多 256 个显式节点、8 层，每个数组最多 100 项，发放 100 堆叠、地面 32 堆叠。原生写入失败和空间不足仍可能部分完成，无跨目标事务回滚。
- Existing items and actor changes use native calls; batches can partially complete. Detached item trees are built before first delivery, with 256 explicit nodes, 8 levels, 100 array entries, 100 inventory stacks or 32 ground stacks per command. This is not a cross-target transaction.
- 数值存储成功不代表原生游戏机制都支持该值。没有对应组件会拒绝；实际耐久、资源使用、枪械、容器放置、AI 和界面效果仍需战局验证。
- Successful storage does not guarantee native gameplay supports every value. Missing components are rejected. Native condition/resource usage, weapons, grids, actors and visual behavior still need in-raid verification.
