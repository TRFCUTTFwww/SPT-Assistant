# 服务端 JSON 编辑器 / Server JSON editor

2026-11-26，2.4 开发更新。

同日修正编辑框的换行和光标处理：节点预览、格式化、导入和粘贴统一 LF，粘贴时保留光标及选区。物品、服务端和存档 JSON 编辑器共用这一处理，避免 Windows CRLF 导致输入位置错位。

入口为**服务端 → JSON 编辑**。左侧选择文件、浏览当前节点的子项或搜索，右侧显示字段说明和 JSON 草稿。可通过“根级”“上级”和路径跳转进入任意层级。

- 文件范围为 `SPT_Runtime/SPT_Data/**/*.json` 与 `SPT_Runtime/user/mods/**/*.json`，包括 28 份主配置、72 类 AI、地图、物品、任务、商人、藏身处、本地化和 MOD 配置。备份、缓存、日志、构建及依赖目录不列入编辑器；角色存档沿用“数据 → JSON 编辑”。
- 支持对象、数组、字符串、数字、布尔值和 `null`，可替换根节点、添加字段、追加数组元素和删除子节点。字段名可以为空或包含句点、引号、方括号和 Unicode 字符。
- 文件和字段都有说明区域。优先使用核实的专用说明及当前 SPT 模型 XML 文档；物品、弹药、瞄具、护甲、容器等常见字段有中英说明。没有可靠定义的字段明确显示“当前服务端未提供该字段的用途说明”，不推断其数值范围或含义。
- 文件每页最多显示 60 个子节点，搜索最多返回 160 个结果；大型节点通过子项浏览或完整节点导出、编辑和导入处理。导入先保留为草稿，点击“保存当前节点”才写入。
- 每 5 秒检查当前文件。检测到外部变化只显示“此文件自开始编辑以来有更新。”，不会刷新浏览快照、移动选中节点或替换草稿。提示保留至玩家主动刷新或重新打开文件。
- 明确保存时，将选中路径的修改应用于磁盘最新文档，保留其他路径的外部修改。若选中路径本身已有更新，该次保存以玩家明确提交的值为准。替换根节点会替换整份文档；数组按当前下标操作。
- 保存不会自动读取外部变更到编辑器。浏览快照只更新本次提交的内容；“丢弃草稿并刷新”由玩家主动执行。
- 保存前保留被替换文件的完整字节，位置为 `user/mods/LocalGearMail/backups/server-json/<时间与编号>/<原相对路径>`。写入使用临时文件和原子替换，校验保存结果。提交瞬间发生额外写入时保留草稿和原文件，可直接重试保存，无需强制刷新。

此编辑器修改磁盘配置；多数原生配置需要重启服务端生效。

## English

Open **Server → JSON editor**. Select a file on the left, browse or search its nodes, and edit the selected JSON value on the right. Root, parent and direct path navigation reach every nesting level.

- Covers JSON files under `SPT_Data` and installed `user/mods`, including the 28 main configuration files, 72 bot types, maps, items, quests, traders, hideout data and localization. Backup, cache, log, build and dependency directories are excluded. Character profiles retain their existing Data editor.
- Supports objects, arrays, strings, numbers, booleans and null; root replacement, named fields, array appends, child deletion and complete node import/export. Imported values remain drafts until explicitly saved.
- Shows file purposes and field descriptions using curated annotations and the installed SPT model XML documentation. Fields without a reliable definition are explicitly marked as undocumented. No guessed ranges are imposed.
- Reads at most 60 children per page and returns up to 160 search matches. Large nodes can be browsed through their children or exported, edited and imported in full.
- Checks the current file every five seconds while the editor is visible. External changes produce a persistent notice only. They never refresh the browse snapshot, move the selection or replace a draft. Refresh remains a player action.
- An explicit save applies the selected edit to the newest disk document and preserves unrelated external changes. The submitted value wins at the selected path. Replacing the root replaces the complete document; array operations address current indices.
- Saving updates only the submitted edit in the browsing snapshot. It does not import unrelated external changes. The player controls **Discard drafts and refresh**.
- Atomically replaces the file and retains its exact previous bytes under `user/mods/LocalGearMail/backups/server-json`. A further write during the commit retains the draft and original file; saving can be retried without a forced refresh.

Most native configuration changes require a server restart. The admin module supports hot reload.
## 验证 / Validation

- 客户端和管理模块 Release 构建通过；管理模块无警告，客户端仅有现存的 3 条警告。
- 535 项服务端 JSON 检查通过：特殊字段名、全部值类型、根节点替换、分页、全文搜索、精确大整数、外部修改保留、无自动刷新、备份、路径限制和损坏 JSON 拒绝。
- 遍历 28 份主配置、普通 Scav 数据和物品数据库共 958,780 个节点，验证每个节点都有说明或明确的无文档标记；其中 33,931 个节点没有可靠用途定义。
- 当前最大的地图 JSON（约 57 MB）分页打开约 0.36 秒，返回约 2.2 KB；完整无匹配搜索约 2.8 秒。耗时为本机测试样本，不作为性能承诺。
- 原有主模块 Probe、英文目录和服务端回归检查通过。
- 运行中的 HTTPS 服务端接口实测 11 项通过，使用临时 JSON 验证外部更新提示、保存不要求刷新、合并保留外部字段、空值读取和手雷字段说明。临时文件与其备份已清理，未修改实际游戏配置或角色存档。
- 当前客户端关闭，更新模块已放入游戏目录；服务端管理模块已热重载。GitHub Release 保持草稿。

Client and server Release builds passed, along with 535 editor checks, the existing main-module Probe, English localization checks and server regressions. A further 11 checks passed through the running HTTPS server using a temporary fixture. Gameplay files and profiles were not modified by that integration test.
