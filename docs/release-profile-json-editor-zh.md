# 数据 → JSON 编辑（存档结构树与字段说明编辑器）

在 **数据** 分类下新增 **`JSON 编辑`** 页签，面向服务端角色存档（`SPT_Runtime/user/profiles/<session>.json`）提供结构化浏览、字段用途标识与直接编辑。

## 主要功能

1. **双栏 JSON 结构树 + 大集合分页**
   - 左栏按层级展示存档顶层（`info`、`characters`、`userbuilds`、`dialogues`、`spt` 及各 MOD 扩展节点）与 `pmc` / `scav` 全部分区；超过 60 项的数组或字典（如 `Inventory.items`、`Quests`、`Encyclopedia`、`dialogueProgress`）自动启用内联分页。
   - 顶部提供 `根级 / PMC 资料 / PMC 仓库 / 技能 / 任务 / 商人 / 藏身处 / Scav / MOD 扩展` 快捷跳转与全文检索（支持按字段名、中英文用途说明、标量值或游戏内物品/任务/商人名称搜索定位）。

2. **全字段用途说明与游戏实体识别**
   - 内置 `ProfileFieldCatalog` 覆盖 SPT 5.0 角色存档顶层、`Info`、`Customization`、`Health`、`Inventory`（含 `items[]` 与 `upd.*` 动态属性）、`Skills`、`Stats`、`Hideout`、`Quests`、`RepeatableQuests`、`TradersInfo`、`RagfairInfo`、`Bonuses`、`Ending`、`spt`、`userbuilds`、`dialogues` 及辅助模组扩展字段的中英双语说明。
   - 自动将库存物品 `_tpl`、任务 `qid`（含状态码）、商人 ID、常规技能与藏身处区域编号解析为游戏内本地化名称并旁注显示。

3. **保存校验与整档导入导出**
   - 节点编辑按上次读取快照检查目标节点并合并到服务端最新存档，账号与角色核心身份字段设为只读。
   - 写入前校验物品模板、唯一 `_id`、父容器链及原生 `SptProfile` 反序列化，自动写入 `LocalGearMail/backups/profiles.sqlite` 备份并同步内存与磁盘。
   - 支持导出格式化整档 JSON 与从外部 JSON 文件导入。
