# SQLite 存档备份与还原

在辅助菜单打开 **数据 → 存档备份**，退出战局后点击“备份当前角色”。可以填写备份名称；每次保存都会创建独立快照，不覆盖旧备份。

数据库保存在服务端 `SPT_Runtime/user/mods/LocalGearMail/backups/profiles.sqlite`。数据库包含完整存档，因此也包含账号信息；不要公开上传。停止服务端后，复制这个文件即可保留或迁移历史备份。数据库使用 Windows 10/11 自带的 SQLite，不需要安装数据库软件。

## 在当前或新存档还原

1. 新建存档时，先正常登录一次，创建 PMC 和 Scav。
2. 使用同一个服务端备份库；迁移服务端时先停止服务端，再把备份库放回上述路径，启动后登录目标存档。
3. 选择历史备份，选择范围：仓库、技能、等级与战绩、生命状态、任务、商人，或全部游戏数据。
4. 点击“预览还原结果”，核对来源、目标和类别，然后点击“确认替换所选数据”。
5. **还原成功后立即退出客户端并重新登录**，再移动物品、使用仓库或进入战局。客户端仍缓存着还原前的库存。

选择的是“替换”，不是合并。仓库还原包含该仓库的根容器模板与容量、嵌套物品、配件、数量、耐久和搜刮状态；保留目标角色身上装备，重新生成导入物品 ID，并修正父容器及快捷栏引用。任务还原同时处理任务物品容器。全部还原包括藏身处、装备、邮件和存档中的 MOD 扩展数据，跨账号完整还原也会重新生成物品 ID，并修正邮件、保险及扩展数据中的相应引用；但保留目标账号的登录信息、账号 ID、PMC/Scav 身份和当前战局标记。

每次实际还原前，会先写入一份“还原前自动备份”。还原错误时可以选择它再次还原。还原前检查 SHA256、模板是否已安装、重复物品 ID、缺失父容器和容器循环；目标存档自预览后发生变化时会拒绝执行并要求重新预览。数据库与还原采用参数绑定和独立连接，写入使用 SQLite 的完整同步事务。

缺少原有 MOD 物品模板时会拒绝还原；安装对应 MOD 后再试。普通游戏时间、商人刷新和 SPT 的每日重置计时仍按服务端规则运行；备份不是暂停时间。不要在服务端运行时用外部 SQLite 编辑器修改备份库或替换存档文件。

## English

Use **Data → Profile backups** outside a raid. Each snapshot stores the full server profile in `LocalGearMail/backups/profiles.sqlite`; restore a subset or all game data into the currently logged-in character. Preview first, confirm replacement, then immediately restart the client before interacting with inventory. A rollback snapshot is created before every restore. New account login and character identities are preserved. Stop the server before copying the database to another installation. Missing templates and malformed inventory trees prevent restoration. Game timers continue to follow SPT rules.
