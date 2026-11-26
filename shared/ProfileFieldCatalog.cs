using System;
using System.Collections.Generic;
using System.Text;

namespace LocalSpt5Esp;

public static class ProfileFieldCatalog
{
    public sealed record Entry(string TitleZh, string TitleEn, string DetailZh, string DetailEn, bool Locked = false);

    public sealed record Description(string Title, string Detail, bool Locked);

    static readonly HashSet<string> ExactLockedPaths = new(StringComparer.Ordinal)
    {
        "$._id",
        "$.info.id",
        "$.info.scavId",
        "$.info.aid",
        "$.inraid",
        "$.characters.pmc._id",
        "$.characters.pmc.aid",
        "$.characters.pmc.sessionId",
        "$.characters.pmc.savage",
        "$.characters.scav._id",
        "$.characters.scav.aid",
        "$.characters.scav.sessionId",
        "$.characters.scav.savage"
    };

    static readonly HashSet<string> UndeletablePaths = new(StringComparer.Ordinal)
    {
        "$",
        "$.info",
        "$.characters",
        "$.characters.pmc",
        "$.characters.scav",
        "$.characters.pmc.Info",
        "$.characters.pmc.Health",
        "$.characters.pmc.Inventory",
        "$.characters.pmc.Inventory.items",
        "$.characters.pmc.Inventory.equipment",
        "$.characters.pmc.Inventory.stash",
        "$.characters.pmc.Skills",
        "$.characters.scav.Info",
        "$.characters.scav.Health",
        "$.characters.scav.Inventory",
        "$.characters.scav.Inventory.items",
        "$.characters.scav.Inventory.equipment"
    };

    static readonly HashSet<string> DynamicMapParents = new(StringComparer.Ordinal)
    {
        "$.dialogues",
        "$.traderPurchases",
        "$.localGearMailReceipts",
        "$.localProjectETransmutation.Receipts",
        "$.spt.migrations",
        "$.spt.cultistRewards",
        "$.spt.extraRepeatableQuests",
        "$.characters.*.Encyclopedia",
        "$.characters.*.TaskConditionCounters",
        "$.characters.*.TradersInfo",
        "$.characters.*.Achievements",
        "$.characters.*.CarExtractCounts",
        "$.characters.*.CoopExtractCounts",
        "$.characters.*.WishList",
        "$.characters.*.Variables",
        "$.characters.*.QuestNotes",
        "$.characters.*.CompletableItems",
        "$.characters.*.battlePassDocumentLimitData",
        "$.characters.*.Ending.available",
        "$.characters.*.Inventory.hideoutAreaStashes",
        "$.characters.*.Inventory.fastPanel",
        "$.characters.*.Hideout.Production",
        "$.characters.*.Hideout.Improvements",
        "$.characters.*.Hideout.MannequinPoses",
        "$.characters.*.Quests[].statusTimers"
    };

    static readonly HashSet<string> BodyPartNames = new(StringComparer.Ordinal)
    {
        "Head", "Chest", "Stomach", "LeftArm", "RightArm", "LeftLeg", "RightLeg"
    };

    static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal)
    {
        // Root
        ["$"] = new("角色完整存档根对象", "Profile root object", "当前账号的完整角色存档数据。", "Complete character profile JSON for the active account."),
        ["$._id"] = new("存档根 ID", "Profile root ID", "存档顶层标识。", "Top-level profile identifier.", true),
        ["$.info"] = new("账号基础信息", "Account info", "启动器账号登录名、游戏购买版本、累计在线时长与角色绑定 ID。", "Launcher login metadata, game edition, total play time and character IDs."),
        ["$.info.id"] = new("账号／主角色 ID", "Account / PMC ID", "存档文件名与会话 ID。", "Matches the profile filename and session ID.", true),
        ["$.info.scavId"] = new("关联 Scav 角色 ID", "Linked Scav ID", "当前账号绑定的玩家 Scav ID。", "Unique ID of the linked player Scav character.", true),
        ["$.info.aid"] = new("账号数字编号 (AID)", "Numeric account ID", "纯数字账号编号，与角色 aid 一致。", "Numeric account identifier matching character aid.", true),
        ["$.info.username"] = new("登录用户名", "Login username", "SPT 启动器使用的账号名称。", "Account username used in the SPT launcher."),
        ["$.info.password"] = new("登录密码哈希", "Password hash", "启动器登录凭据（空字符串或密码哈希）。", "Launcher login credential (empty string or password hash)."),
        ["$.info.wipe"] = new("删档重开标记", "Wipe flag", "设为 true 时下次登录触发删档重选阵营流程。", "When true, the next login triggers a profile wipe and faction selection."),
        ["$.info.edition"] = new("游戏购买版本", "Account edition", "账号版本名称（如 Unheard、Edge Of Darkness、Standard、SPT Developer）。", "Edition key such as Unheard, Edge Of Darkness, Standard or SPT Developer."),
        ["$.info.totalInGame"] = new("账号累计在线秒数", "Total time in game (s)", "账号累计运行客户端的总秒数统计。", "Cumulative seconds spent in game on this account."),

        ["$.characters"] = new("双角色存档容器", "Characters container", "包含 pmc（主角色）与 scav（玩家拾荒者）两套完整角色数据。", "Holds both the pmc main character and the scav player-scav character."),
        ["$.characters.pmc"] = new("PMC 主角色数据", "PMC character data", "主角色的等级、生命、主仓库、穿戴装备、技能、任务、藏身处、商人关系与成就。", "Main PMC progression, health, stash, equipment, skills, quests, hideout, traders and achievements."),
        ["$.characters.scav"] = new("玩家 Scav 数据", "Player Scav data", "玩家 Scav 当前待出击装备、独立技能、生命体征与生涯统计。", "Player Scav next-raid loadout, Scav skills, health and statistics."),

        ["$.userbuilds"] = new("玩家改枪与配装预设", "Saved player builds", "在游戏改枪台、配装台与压弹预设中保存的全部自定义方案。", "Custom presets saved in weapon modding, equipment loadouts and magazine presets."),
        ["$.userbuilds.weaponBuilds"] = new("改枪台武器预设列表", "Weapon builds", "玩家手动保存的枪械配件组合预设列表。", "Saved custom weapon modding presets."),
        ["$.userbuilds.weaponBuilds[]"] = new("单套武器改枪预设", "Weapon build entry", "包含预设 ID、自定义名称 Name、枪身根 ID Root 与配件列表 Items。", "Single saved weapon build with Name, Root item ID and component Items."),
        ["$.userbuilds.weaponBuilds[].Id"] = new("武器预设 ID", "Weapon build ID", "该套武器预设的唯一 24 位标识。", "Unique 24-hex identifier for this weapon preset."),
        ["$.userbuilds.weaponBuilds[].Name"] = new("武器预设名称", "Weapon build name", "玩家在改枪台起的名字。", "User-authored name shown in weapon presets."),
        ["$.userbuilds.weaponBuilds[].Root"] = new("枪械本体根物品 ID", "Weapon root item ID", "Items 数组中代表枪机本体的 _id。", "Item _id in Items that represents the receiver/root weapon."),
        ["$.userbuilds.weaponBuilds[].Items"] = new("预设配件列表", "Build component items", "组成整把武器的枪身与各级配件条目。", "Receiver and attachment items composing the build."),
        ["$.userbuilds.equipmentBuilds"] = new("整套穿戴配装预设", "Equipment builds", "原版装备预设台保存的整套出击配装列表。", "Saved full gear/equipment presets."),
        ["$.userbuilds.magazineBuilds"] = new("弹匣压弹预设", "Magazine builds", "原版弹匣装填预设（含顶弹、混装比例与底弹设置）。", "Saved magazine loading presets (top/loop/bottom rounds)."),

        ["$.dialogues"] = new("邮件与联系人会话", "Mail & dialogues", "各商人、系统通知、Ragman 跳蚤回款、保险退回及好友聊天记录（键为会话 ID）。", "Conversations with traders, system mail, flea payouts and insurance returns keyed by dialogue ID."),
        ["$.dialogues.<id>"] = new("单个联系人会话", "Dialogue thread", "单个商人或系统联系人的会话状态与历史邮件列表。", "Message history and unread/attachment state for one trader or system contact."),
        ["$.dialogues.<id>.messages"] = new("会话邮件列表", "Dialogue messages", "该联系人发来的全部消息与待领取附件物品。", "Messages and claimable item rewards in this conversation."),
        ["$.dialogues.<id>.messages[]"] = new("单封系统／商人邮件", "Mail message", "包含文本 text、时间 dt、是否有未领附件 hasRewards 及附件 items。", "Single mail entry with text, timestamp dt, reward status and attached items."),
        ["$.dialogues.<id>.new"] = new("未读消息数", "Unread message count", "该会话角标显示的未读条数。", "Unread message badge counter."),
        ["$.dialogues.<id>.attachmentsNew"] = new("未领附件消息数", "Unclaimed attachment count", "含有未领取物品奖励的新邮件数量。", "Count of new messages with unclaimed attachments."),
        ["$.dialogues.<id>.pinned"] = new("会话置顶标记", "Pinned conversation", "是否在聊天列表顶部固定该联系人。", "Whether this thread is pinned in the messenger."),

        ["$.spt"] = new("SPT 服务端元数据", "SPT metadata", "SPT 服务端专用的版本记录、历史加载 MOD、礼品领取记录与数据迁移标记。", "SPT-specific version info, mod history, redeemed gifts, daily refresh counters and migrations."),
        ["$.spt.version"] = new("存档所属 SPT 版本", "SPT version", "创建或最近更新该存档的 SPT 服务端版本号。", "SPT server version that created or last updated this profile."),
        ["$.spt.mods"] = new("历史加载 MOD 记录", "Recorded server mods", "曾在该存档运行过的服务端 MOD 名称、作者、版本与首次记录时间。", "History of server mods loaded with this profile."),
        ["$.spt.mods[]"] = new("单条 MOD 历史记录", "Mod history entry", "记录单个服务端 MOD 的名称、版本号、作者与时间戳。", "Recorded mod name, version, author and timestamp."),
        ["$.spt.receivedGifts"] = new("已领取礼品码记录", "Redeemed gifts", "已兑换过的 SPT 礼品码 ID 与领取次数记录。", "Gift codes already redeemed on this profile."),
        ["$.spt.blacklistedItemTpls"] = new("存档屏蔽物品模板", "Blacklisted item templates", "仅对该存档屏蔽生成的物品 _tpl 列表。", "Item template IDs blacklisted for this profile."),
        ["$.spt.freeRepeatableRefreshUsedCount"] = new("日常任务已用免费刷新数", "Used free quest refreshes", "当日/当周已消耗的每日、每周与 Scav 任务免费更换次数。", "Used free reroll counts for daily, weekly and Scav repeatable quests."),
        ["$.spt.tutorialCompleted"] = new("新手教程完成标记", "Tutorial completed", "是否已完成开局教学引导关卡。", "Whether the introductory tutorial raid is marked complete."),
        ["$.spt.migrations"] = new("已执行存档迁移记录", "Applied migrations", "SPT 升级时已自动执行的存档修复脚本标记。", "Completed SPT profile migration timestamps/flags."),
        ["$.spt.cultistRewards"] = new("仪式圈待发奖励缓存", "Cultist circle reward cache", "邪教徒献祭圈在后台预生成的奖励缓存数据。", "Pending Cultist Circle sacrifice reward state."),
        ["$.spt.extraRepeatableQuests"] = new("额外日常任务槽数据", "Extra repeatable quests", "通过特殊物品或加成解锁的额外循环任务记录。", "Extra repeatable quest data unlocked on the profile."),

        ["$.inraid"] = new("战局运行态标记", "In-raid state", "当前战局所处地图状态。", "Active raid location state.", true),
        ["$.insurance"] = new("待投递保险队列", "Pending insurance queue", "战局阵亡丢弃后等待到点发信退回的投保物品队列。", "Insured gear waiting for scheduled trader return mail."),
        ["$.btrDelivery"] = new("BTR 待送达物资队列", "Pending BTR deliveries", "战局中交由 BTR 司机托运、等待发信送达的物品列表。", "Items handed to the BTR driver awaiting mail delivery."),
        ["$.traderPurchases"] = new("商人周期限购计数", "Trader purchase limits", "当前补货周期内各商人限量商品的个人已购数量统计。", "Per-trader personal buy-restriction counts for the current restock cycle."),
        ["$.traderPurchases.<id>"] = new("单名商人限购记录", "Trader purchase record", "该商人在本周期内已被当前角色买走的商品计数。", "Personal purchase counts for one trader's assortment."),
        ["$.friends"] = new("好友列表", "Friends list", "本地或联机模式下的好友资料数组。", "Friend list entries."),
        ["$.customisationUnlocks"] = new("已解锁外观与藏身处装饰", "Customization unlocks", "通过任务、成就或商城解锁的服装、迷彩、语音、狗牌与藏身处装潢项。", "Unlocked clothing suites, voices, dogtags and hideout customization items."),
        ["$.customisationUnlocks[]"] = new("单项外观解锁记录", "Customization unlock entry", "包含解锁项 id、获取来源 source 与外观类别 type。", "Single unlocked customization entry (id, source, type)."),
        ["$.dialogueProgress"] = new("3D 商人／主线对话进度", "Dialogue tree progress", "记录与 3D 商人或剧情 NPC 已走过的对话节点（traderId / dialogueId / nodeId）。", "Completed 3D trader / story dialogue tree nodes (traderId, dialogueId, nodeId)."),
        ["$.dialogueProgress[]"] = new("单条对话节点进度", "Dialogue progress node", "记录某位商人某段对话树中已触发的节点 ID。", "Visited dialogue node record."),
        ["$.purchasedShopOffers"] = new("商城已购报价记录", "Purchased shop offers", "游戏内扩展商城/联动商店已购买过的报价 ID 列表。", "Offer IDs purchased from the in-game expansion shop."),
        ["$.purchasedShopStashRows"] = new("额外购买仓库行数", "Purchased extra stash rows", "在基础版本仓库之上额外解锁的扩充行数（每行 10 格）。", "Extra stash rows unlocked beyond base edition size (10 cells per row)."),

        // Mod extensions at root
        ["$.localGearMailReceipts"] = new("辅助邮件防重回执 (MOD)", "Assistant mail receipts (Mod)", "SPT Assistant 邮件发放回执表。", "SPT Assistant gear-mail delivery receipts."),
        ["$.localGearMailReceipts.<id>"] = new("单次发信回执 (MOD)", "Mail receipt entry (Mod)", "记录单次物品发放请求的摘要 Digest、邮件 MessageId 与物品数量。", "Single delivery receipt with Digest, MessageId and item counts."),
        ["$.localMiscScavSecure"] = new("Scav 安全箱暂存数据 (MOD)", "Scav secure container cache (Mod)", "辅助模组「Scav 安全箱」功能跨战局保留的安全箱与内部物品。", "Persisted Scav secure container items managed by SPT Assistant."),
        ["$.localProjectETransmutation"] = new("ProjectE 等价交换存档 (MOD)", "ProjectE transmutation data (Mod)", "辅助模组内置 ProjectE 转换桌数据（EMC 余额、已学习物品与流水）。", "Integrated ProjectE transmutation table state (EMC balance, learned templates, receipts)."),
        ["$.localProjectETransmutation.Balance"] = new("ProjectE EMC 能量余额", "ProjectE EMC balance", "当前角色在等价交换转换桌中储存的 EMC 数值。", "Stored EMC energy balance in the ProjectE transmutation table."),
        ["$.localProjectETransmutation.Revision"] = new("ProjectE 状态修订号", "ProjectE revision", "转换桌存档每次变更递增的乐观锁版本号。", "Monotonic revision number for ProjectE storage."),
        ["$.localProjectETransmutation.Learned"] = new("已学习物品模板列表", "Learned item templates", "已在转换桌中拆解并解锁复制配方的物品 _tpl 数组。", "Item _tpl IDs learned for EMC replication."),
        ["$.localProjectETransmutation.Receipts"] = new("ProjectE 操作回执", "ProjectE operation receipts", "转换桌操作回执表。", "ProjectE transmutation transaction receipts."),
        ["$.localEspScavPreset"] = new("Scav 出击装备预设 (MOD)", "Scav loadout preset (Mod)", "辅助模组「角色管理 → Scav 装备」保存的下局或常驻出击装备快照。", "Saved player-Scav equipment preset from SPT Assistant."),

        // Character shared (pmc & scav)
        ["$.characters.*.Prestige"] = new("转生声望记录", "Prestige data", "角色声望转生（Prestige）阶段与历史奖励记录。", "Character prestige reset history and rewards."),
        ["$.characters.*._id"] = new("角色唯一 ID", "Character ID", "角色实体 24 位十六进制 ID。", "24-hex character entity ID.", true),
        ["$.characters.*.aid"] = new("所属账号 AID", "Character account ID", "角色归属的数字账号 ID。", "Owning numeric account ID.", true),
        ["$.characters.*.sessionId"] = new("角色会话绑定 ID", "Session ID", "绑定到主存档的会话 ID。", "Bound session ID.", true),
        ["$.characters.*.savage"] = new("关联 Scav ID", "Linked Scav ID", "PMC 关联的玩家 Scav 角色 ID。", "Linked player Scav character ID.", true),
        ["$.characters.*.karmaValue"] = new("PMC 业力值 (0～1)", "PMC karma (0–1)", "PMC 归一化业力值，0.5 为中性。", "Normalized PMC karma value (0.5 is neutral)."),

        // Info
        ["$.characters.*.Info"] = new("角色名片与等级信息", "Character profile info", "昵称、阵营、等级、总经验、购买版本图标、Scav 冷却与权限开关。", "Nickname, faction side, level, experience, edition badge, Scav cooldown and account flags."),
        ["$.characters.*.Info.Nickname"] = new("角色游戏内昵称", "Character nickname", "战局狗牌、聊天和大厅显示的角色名。", "Display name shown in UI, chat and dogtags."),
        ["$.characters.*.Info.LowerNickname"] = new("小写索引昵称", "Lowercase nickname", "用于名字搜索索引的小写昵称。", "Lowercase nickname used for search indexing."),
        ["$.characters.*.Info.MainProfileNickname"] = new("所属 PMC 主昵称", "Main PMC nickname", "玩家 Scav 信息中记录的主角色昵称。", "Main PMC nickname referenced on the Scav profile."),
        ["$.characters.*.Info.Side"] = new("所属阵营", "Character faction", "PMC 为 Usec 或 Bear；玩家 Scav 为 Savage。", "Usec or Bear for PMC; Savage for player Scav."),
        ["$.characters.*.Info.Level"] = new("角色当前等级", "Character level", "面板显示等级（1～79），实际等级由 Experience 总经验决定。", "Displayed character level (1–79); actual level is derived from Experience."),
        ["$.characters.*.Info.Experience"] = new("累计总经验值", "Total experience (XP)", "决定角色实际等级的累计经验整数。", "Cumulative XP integer determining actual character level."),
        ["$.characters.*.Info.RegistrationDate"] = new("建号时间戳", "Registration timestamp", "角色创建时的 Unix 秒级时间戳。", "Unix timestamp (seconds) when the character was created."),
        ["$.characters.*.Info.GameVersion"] = new("角色版本内部代号", "Character edition code", "如 unheard_edition、edge_of_darkness、prepare_to_escape、left_behind、standard。", "Internal edition identifier such as unheard_edition, edge_of_darkness or standard."),
        ["$.characters.*.Info.AccountType"] = new("账号类型枚举", "Account type", "常规账号为 0。", "Account type code (0 for standard player accounts)."),
        ["$.characters.*.Info.MemberCategory"] = new("可用名字徽章掩码", "Member category bitmask", "拥有的身份图标位掩码（如 1024 Unheard 蓝标、2 皇冠/开发、0 普通）。", "Bitmask of owned name badges/icons (e.g. 1024 Unheard, 2 Developer/EoD, 0 Default)."),
        ["$.characters.*.Info.SelectedMemberCategory"] = new("当前佩戴名字徽章", "Selected member badge", "当前在昵称前展示的图标枚举值。", "Currently displayed name badge value."),
        ["$.characters.*.Info.Bans"] = new("封禁限制列表", "Active bans", "跳蚤市场或社交封禁条目数组。", "Active restriction or ban entries."),
        ["$.characters.*.Info.lockedMoveCommands"] = new("移动操作锁标记", "Locked move commands", "原生客户端库存移动锁布尔标记。", "Native inventory move-command lock flag."),
        ["$.characters.*.Info.SavageLockTime"] = new("Scav 冷却结束时间戳", "Scav cooldown unlock time", "下次可出击 Scav 的 Unix 秒级时间戳（设为 0 可立即清零冷却）。", "Unix timestamp when Scav cooldown ends (set 0 to clear cooldown)."),
        ["$.characters.*.Info.LastTimePlayedAsSavage"] = new("上次游玩 Scav 时间", "Last Scav raid timestamp", "最近一次使用玩家 Scav 的 Unix 秒级时间戳。", "Unix timestamp of the last player-Scav session."),
        ["$.characters.*.Info.Settings"] = new("角色 AI 身份参数", "Bot role settings", "AI 角色类型 Role、难度 BotDifficulty 等原生结构字段。", "Role and BotDifficulty metadata."),
        ["$.characters.*.Info.NicknameChangeDate"] = new("允许改名时间戳", "Nickname change cooldown", "下次允许在原生设置里改名的 Unix 秒级时间戳（0 为无冷却）。", "Unix timestamp when native nickname change becomes available again."),
        ["$.characters.*.Info.NeedWipeOptions"] = new("待确认删档选项", "Pending wipe options", "赛季重置待选模式列表。", "Pending wipe mode options."),
        ["$.characters.*.Info.BannedState"] = new("账号封禁状态", "Banned state", "角色封禁状态布尔值。", "Whether the character is marked banned."),
        ["$.characters.*.Info.BannedUntil"] = new("封禁截止时间戳", "Banned until timestamp", "封禁到期 Unix 秒级时间戳。", "Ban expiration Unix timestamp."),
        ["$.characters.*.Info.IsStreamerModeAvailable"] = new("主播隐私模式权限", "Streamer mode available", "是否在设置中开放主播隐藏昵称选项。", "Enables the streamer privacy option in game settings."),
        ["$.characters.*.Info.SquadInviteRestriction"] = new("组队邀请限制", "Squad invite restriction", "是否屏蔽陌生人组队邀请。", "Blocks random lobby squad invitations."),
        ["$.characters.*.Info.HasCoopExtension"] = new("离线合作扩展标记", "Co-op extension owned", "是否拥有原版离线练习合作扩展权限。", "Whether PvE/offline co-op expansion flag is enabled."),
        ["$.characters.*.Info.HasPveGame"] = new("PvE 模式权限标记", "PvE mode owned", "是否拥有 PvE 模式访问权限。", "Whether PvE mode entitlement is active."),
        ["$.characters.*.Info.isMigratedSkills"] = new("技能迁移完成标记", "Skills migrated flag", "旧版技能数据迁移标记。", "Skill system migration completion flag."),
        ["$.characters.*.Info.PrestigeLevel"] = new("当前转生等级", "Current prestige level", "角色名片显示的转生图标等级（0 为未转生）。", "Current prestige tier level (0 = none)."),
        ["$.characters.*.Info.PrestigeGameMode"] = new("转生所属游戏模式", "Prestige game mode", "如 regular 或 pve。", "Game mode key for prestige (regular / pve)."),
        ["$.characters.*.Info.PrestigeLevels"] = new("各模式转生等级表", "Prestige levels by mode", "分别记录 regular 与 pve 的转生次数。", "Per-mode prestige level dictionary."),
        ["$.characters.*.Info.SelectedPrestigeGameMode"] = new("当前展示转生模式", "Displayed prestige mode", "名片当前选中展示的转生模式。", "Selected prestige mode shown on profile."),

        // Customization
        ["$.characters.*.Customization"] = new("人物外观与语音", "Character appearance & voice", "头部脸型、上衣、裤子、手套、狗牌外观与人物语音模板 ID。", "Template IDs for head, upper body, lower body, hands, dogtag style and voice."),
        ["$.characters.*.Customization.Head"] = new("头部脸型模型 ID", "Head model ID", "角色脸部外观模板 ID。", "Character head/face customization template ID."),
        ["$.characters.*.Customization.Body"] = new("上衣外观 ID", "Upper body clothing ID", "Ragman 战术上衣外观模板 ID。", "Tactical upper clothing suite ID."),
        ["$.characters.*.Customization.Feet"] = new("裤子与靴子外观 ID", "Lower body clothing ID", "Ragman 战术裤外观模板 ID。", "Tactical lower clothing/pants ID."),
        ["$.characters.*.Customization.Hands"] = new("第一人称手部手套 ID", "First-person hands ID", "第一人称视角手臂与手套模型模板 ID。", "First-person sleeves/gloves template ID."),
        ["$.characters.*.Customization.DogTag"] = new("狗牌样式 ID", "Dogtag style ID", "角色佩戴的定制狗牌外观 ID。", "Equipped custom dogtag appearance ID."),
        ["$.characters.*.Customization.Voice"] = new("角色语音包 ID", "Voice ID", "战局内战术喊话使用的语音模板 ID。", "Character voice line template ID."),

        // Health
        ["$.characters.*.Health"] = new("生命体征与部位血量", "Vitals & body part health", "水分、能量、体温以及头/胸/腹/四肢七大部位的当前与最大生命值、负面效果。", "Hydration, energy, temperature and current/max HP plus effects for all seven body parts."),
        ["$.characters.*.Health.Hydration"] = new("水分数值", "Hydration", "Current 当前水分与 Maximum 水分上限（常规 100）。", "Current and Maximum hydration."),
        ["$.characters.*.Health.Energy"] = new("能量数值", "Energy", "Current 当前能量与 Maximum 能量上限（常规 100～110）。", "Current and Maximum energy."),
        ["$.characters.*.Health.Temperature"] = new("体温数值", "Body temperature", "Current 当前体温与 Maximum 上限（常规 37℃）。", "Current and Maximum body temperature."),
        ["$.characters.*.Health.BodyParts"] = new("七大身体部位血量", "Seven body parts", "包含 Head(头)、Chest(胸)、Stomach(腹)、LeftArm(左臂)、RightArm(右臂)、LeftLeg(左腿)、RightLeg(右腿)。", "Health and status effects for Head, Chest, Stomach, LeftArm, RightArm, LeftLeg and RightLeg."),
        ["$.characters.*.Health.BodyParts.<part>"] = new("单个身体部位状态", "Body part health & effects", "该身体部位的 Health（当前/上限血量）与 Effects（骨折/流血等伤势）。", "Health (Current/Maximum) and active wound Effects for this body part."),
        ["$.characters.*.Health.BodyParts.<part>.Health"] = new("部位血量 (当前／最大)", "Limb HP (Current / Maximum)", "须满足 0 ≤ Current ≤ Maximum 且 Maximum > 0。", "Must satisfy 0 <= Current <= Maximum and Maximum > 0."),
        ["$.characters.*.Health.BodyParts.<part>.Effects"] = new("部位伤势与负面效果", "Limb status effects", "战局外残留的流血、骨折等效果字典（清空为 {} 即可移除伤势）。", "Out-of-raid lingering wounds such as fractures or bleeds ({} clears wounds)."),
        ["$.characters.*.Health.UpdateTime"] = new("藏身处回血结算时间戳", "Health update timestamp", "上次在战局外结算自然恢复水/能量/血量的 Unix 秒级时间。", "Unix timestamp of last out-of-raid regeneration tick."),
        ["$.characters.*.Health.Immortal"] = new("原生不死标记", "Native immortal flag", "角色原生无敌标记字段。", "Native character immortality boolean."),

        // Inventory
        ["$.characters.*.Inventory"] = new("角色库存与装备树", "Character inventory", "包含扁平化物品数组 items 及装备、主仓库、整理台、任务物品仓等根容器引用 ID。", "Flat item tree array plus root container IDs for equipment, stash, sorting table and quest items."),
        ["$.characters.*.Inventory.items"] = new("全部物品实例列表", "Inventory items array", "仓库、身上装备、枪械配件、弹匣子弹与藏身处插槽内所有物品的扁平列表。", "Flat array of all items across stash, equipped gear, weapon mods and containers."),
        ["$.characters.*.Inventory.items[]"] = new("单件物品实例", "Inventory item instance", "由 _id(实例ID)、_tpl(物品模板)、parentId(父容器ID)、slotId(槽位)、location(坐标) 和 upd(动态属性) 组成。", "Single item node defined by _id, _tpl, parentId, slotId, grid location and dynamic upd attributes."),
        ["$.characters.*.Inventory.items[]._id"] = new("物品实例唯一 ID", "Item instance _id", "24 位十六进制唯一 ID；子配件/内容物通过 parentId 指向此值。", "Unique 24-hex item instance ID referenced by child items' parentId."),
        ["$.characters.*.Inventory.items[]._tpl"] = new("物品模板 ID (_tpl)", "Item template _tpl", "决定物品种类的 24 位模板 ID。", "24-hex item template ID."),
        ["$.characters.*.Inventory.items[].parentId"] = new("所属父容器／母体 ID", "Parent item/container ID", "所属容器、背包、胸挂或母体武器的 _id。", "The _id of the parent container, rig, weapon or stash."),
        ["$.characters.*.Inventory.items[].slotId"] = new("安装槽位／网格分区名", "Slot or grid ID", "如 hideout(仓库主网格)、FirstPrimaryWeapon(主武器槽)、mod_scope(瞄具槽)、main(包内网格)、cartridges(弹药)。", "Slot or grid name such as hideout, FirstPrimaryWeapon, mod_scope, main or cartridges."),
        ["$.characters.*.Inventory.items[].location"] = new("容器网格坐标／弹序", "Grid coordinates / index", "网格内为 {x, y, r: \"Horizontal\"/\"Vertical\", isSearched}；弹匣压弹中为整数弹序。", "Grid placement {x, y, r} inside containers, or integer cartridge stack index inside magazines."),
        ["$.characters.*.Inventory.items[].upd"] = new("物品动态状态属性 (upd)", "Item dynamic state (upd)", "记录数量、战局打钩(FIR)、耐久度、医疗/食物/燃料余量、钥匙已用次数、狗牌等动态参数。", "Dynamic per-item properties: stack count, FIR flag, durability, resource remaining, key usages, dogtag, etc."),
        ["$.characters.*.Inventory.items[].upd.StackObjectsCount"] = new("物品堆叠数量", "Stack count", "货币、子弹或可堆叠物资的当前组内数量。", "Current stack size for ammo, money or stackable items."),
        ["$.characters.*.Inventory.items[].upd.SpawnedInSession"] = new("战局中发现打钩 (FIR)", "Found in raid (FIR)", "true 表示带有战局内发现白色对勾标记，可用于提交 FIR 任务。", "true marks the item as Found in Raid (white checkmark)."),
        ["$.characters.*.Inventory.items[].upd.Repairable"] = new("护甲／武器耐久度", "Repairable durability", "包含 Durability（当前耐久）与 MaxDurability（当前最大耐久上限）。", "Contains current Durability and current MaxDurability."),
        ["$.characters.*.Inventory.items[].upd.MedKit"] = new("医疗品剩余治疗量", "Medkit HP resource", "HpResource 表示医疗包或手术包剩余可用点数。", "Remaining HpResource points on a medkit or surgical kit."),
        ["$.characters.*.Inventory.items[].upd.FoodDrink"] = new("食品／饮品剩余量", "Food / drink remaining", "HpPercent 表示食物或水壶剩余可食用量。", "Remaining HpPercent resource on food or drink."),
        ["$.characters.*.Inventory.items[].upd.Resource"] = new("燃料／滤芯资源余量", "Consumable resource", "Value 表示燃料桶、滤水器、空气滤毒罐剩余资源量。", "Remaining Value on fuel tanks, water filters or air filters."),
        ["$.characters.*.Inventory.items[].upd.Key"] = new("钥匙已使用次数", "Key usages consumed", "NumberOfUsages 为「已消耗次数」（0 为全新；设为负数可获得超出模板上限的可用次数）。", "NumberOfUsages is times already used (0 = fresh; negative values grant extra uses above template max)."),
        ["$.characters.*.Inventory.items[].upd.RepairKit"] = new("维修包剩余点数", "Repair kit resource", "护甲或武器维修套件的剩余维修点数 Resource。", "Remaining repair points in an armor/weapon repair kit."),
        ["$.characters.*.Inventory.items[].upd.SideEffect"] = new("邪教徒刀毒素余量", "Cultist toxin charges", "Value 表示近战武器剩余毒素层数。", "Remaining poison charges on toxin weapons."),
        ["$.characters.*.Inventory.items[].upd.Foldable"] = new("枪托折叠状态", "Stock folded state", "Folded 为 true 表示枪托已折叠。", "Folded boolean for folding stocks."),
        ["$.characters.*.Inventory.items[].upd.Togglable"] = new("战术附件／夜视仪开关", "Device toggle state", "On 表示手电、激光或夜视仪当前开启状态。", "On boolean for flashlights, lasers, NVGs or face shields."),
        ["$.characters.*.Inventory.items[].upd.FireMode"] = new("武器快慢机射击模式", "Weapon fire mode", "FireMode 记录当前选中的 single / fullauto / burst 模式。", "Selected fire mode (single, fullauto, burst)."),
        ["$.characters.*.Inventory.items[].upd.Sight"] = new("瞄具归零与倍率档位", "Sight calibration & mode", "瞄准镜当前分划样式、切换倍率与归零点索引。", "Selected reticle mode, scope index and zeroing calibration."),
        ["$.characters.*.Inventory.items[].upd.Dogtag"] = new("狗牌阵亡者记录", "Dogtag kill metadata", "记录死者昵称、等级、阵营、击杀时间、凶手与所用武器。", "Victim nickname, level, side, timestamp, killer and weapon template on a dogtag."),
        ["$.characters.*.Inventory.items[].upd.PinLockState"] = new("物品锁定／固定状态", "Item pin/lock state", "仓库物品固定（Pinned）或锁定（Locked）状态。", "Inventory item Pinned or Locked state."),
        ["$.characters.*.Inventory.equipment"] = new("角色穿戴根容器 ID", "Equipment root ID", "指向 items 中代表角色装备栏根节点的 _id。", "References the _id of the root equipment container item."),
        ["$.characters.*.Inventory.stash"] = new("主仓库根容器 ID", "Main stash root ID", "指向 items 中主仓库根容器的 _id。", "References the _id of the main stash root item."),
        ["$.characters.*.Inventory.sortingTable"] = new("临时整理台根容器 ID", "Sorting table root ID", "指向 items 中临时分类整理台的根 _id。", "References the sorting table root container _id."),
        ["$.characters.*.Inventory.questRaidItems"] = new("战局内任务物品栏 ID", "In-raid quest items root ID", "指向当前带入战局的特殊任务信物栏根 _id。", "References the in-raid quest item container _id."),
        ["$.characters.*.Inventory.questStashItems"] = new("战局外任务物品仓 ID", "Off-raid quest stash ID", "指向任务界面暂存的特殊任务物品仓库根 _id。", "References the off-raid quest item stash _id."),
        ["$.characters.*.Inventory.hideoutAreaStashes"] = new("藏身处区域容器映射", "Hideout area stashes", "武器架、装备假人、战利品展示柜与仪式圈等区域容器 ID 字典。", "Maps hideout area stash types (weapon stand, mannequins, cultist circle) to container item IDs."),
        ["$.characters.*.Inventory.fastPanel"] = new("数字快捷栏绑定", "Quick-slot bindings", "快捷栏槽位（Item4～Item0）与物品 _id 的绑定关系。", "Maps quickbind slots to inventory item IDs."),
        ["$.characters.*.Inventory.favoriteItems"] = new("收藏物品 ID 列表", "Favorite items", "玩家标记为星标收藏的物品 _id 数组。", "Item _id list marked as favorites."),
        ["$.characters.*.Inventory.hideoutCustomizationStashId"] = new("藏身处装修库存根 ID", "Hideout customization stash ID", "存放藏身处壁纸、地板、吊顶与海报的根容器 _id。", "Root container _id for hideout walls, floors, ceilings and posters."),

        // Skills
        ["$.characters.*.Skills"] = new("角色技能与武器精通", "Skills & weapon mastering", "包含 Common（常规技能等级）与 Mastering（枪械家族精通）。", "Contains Common passive/active skills and weapon family Mastering."),
        ["$.characters.*.Skills.Common"] = new("常规技能列表", "Common skills array", "耐力、力量、搜索、代谢等全部角色技能数组。", "Endurance, Strength, Attention, Metabolism and all other character skills."),
        ["$.characters.*.Skills.Common[]"] = new("单项角色技能", "Skill entry", "Id 为技能英文名，Progress 为点数（每 100 点 = 1 级，5100 点 = 51 满级精英）。", "Id is the skill key; Progress is skill points (100 = Lv.1, 5100 = Lv.51 Elite)."),
        ["$.characters.*.Skills.Common[].Id"] = new("技能标识名", "Skill ID", "如 Endurance(耐力)、Strength(力量)、Attention(感知)、Health(健康)。", "Skill identifier such as Endurance, Strength or Attention."),
        ["$.characters.*.Skills.Common[].Progress"] = new("技能熟练度点数", "Skill progress points", "100 点对应 1 级，5100 为满级精英。", "100 points per level; 5100 reaches Elite level 51."),
        ["$.characters.*.Skills.Mastering"] = new("武器精通列表", "Weapon mastering array", "各枪械家族（如 M4、AK74）的熟练命中进度。", "Mastery progress across weapon families."),
        ["$.characters.*.Skills.Mastering[]"] = new("单项武器精通", "Weapon mastery entry", "Id 为武器精通组名，Progress 为累计熟练命中点数。", "Weapon family Id and cumulative mastery hit Progress."),
        ["$.characters.*.Skills.Points"] = new("遗留技能点", "Legacy skill points", "原版保留字段，通常为 0。", "Legacy unused skill points field (0)."),

        // Stats & Counters
        ["$.characters.*.Stats"] = new("角色生涯战绩统计", "Career statistics", "记录出击场次、撤离次数、K/D、连胜、击杀名单与上局结算明细。", "Lifetime raid counts, survivals, kills, streaks, victim log and last-session stats."),
        ["$.characters.*.Stats.Eft"] = new("塔科夫详细统计容器", "EFT stats container", "包含 OverallCounters（生涯总计）、SessionCounters（上局统计）、Victims（击杀记录）等。", "Holds OverallCounters, SessionCounters, Victims, DeathCause and TotalInGameTime."),
        ["$.characters.*.Encyclopedia"] = new("物品检视全图鉴", "Item encyclopedia", "已检视物品模板表（_tpl -> bool）。", "Examined item templates map (_tpl -> bool)."),
        ["$.characters.*.Encyclopedia.<id>"] = new("单件物品检视状态", "Examined item flag", "true = 已检视，false = 未检视。", "true if this item template has been examined."),
        ["$.characters.*.TaskConditionCounters"] = new("任务目标进度计数器", "Quest condition counters", "各任务子条件（如击杀特定目标数、搜索次数、距离击杀数）的当前累计值。", "Progress counters for multi-step quest objectives (kills, items handed over, etc.)."),
        ["$.characters.*.TaskConditionCounters.<id>"] = new("单项任务条件计数", "Condition counter entry", "包含条件 id、所属任务 sourceId、类型 type 与当前进度数值 value。", "Tracks current value for one quest condition ID."),
        ["$.characters.*.InsuredItems"] = new("当前已投保物品列表", "Insured items list", "身上或仓库中已向商人购买过保险的物品条目（tid 商人 ID + itemId 物品 _id）。", "Currently insured inventory items (trader tid + itemId)."),
        ["$.characters.*.InsuredItems[]"] = new("单条物品投保记录", "Insured item record", "tid 为承保商人 ID，itemId 为被投保物品的 _id。", "Single insurance binding (tid trader ID, itemId inventory item _id)."),

        // Hideout
        ["$.characters.*.Hideout"] = new("藏身处设施与制造", "Hideout state", "全部藏身处模块等级、正在进行的建造/制造进度、插槽耗材与装潢风格。", "Hideout area levels, ongoing upgrades/crafts, fuel/filter slots and visual customization."),
        ["$.characters.*.Hideout.Areas"] = new("藏身处设施区域列表", "Hideout areas array", "发电机、工作台、医疗站、比特币矿场、仓库等全部设施状态。", "All hideout station entries (Generator, Workbench, Medstation, Bitcoin Farm, etc.)."),
        ["$.characters.*.Hideout.Areas[]"] = new("单个藏身处区域", "Hideout area entry", "包含区域枚举 type、当前等级 level、建造状态 constructing、完成时间 completeTime 与插槽 slots。", "Station type enum, current level, constructing flag, completeTime and installed slot items."),
        ["$.characters.*.Hideout.Areas[].type"] = new("藏身处区域类型编号", "Hideout area type ID", "原生 HideoutAreas 枚举整数（如 0 通风、4 发电机、10 工作台、20 比特币矿场、27 仪式圈）。", "Numeric HideoutAreas enum ID."),
        ["$.characters.*.Hideout.Areas[].level"] = new("设施当前等级", "Station current level", "0 表示尚未建造，1～3（仓库最高 4）为已建成等级。", "Current built level of the hideout station."),
        ["$.characters.*.Hideout.Areas[].active"] = new("设施运行开关", "Station active flag", "如发电机当前是否通电开启。", "Whether the station (e.g. Generator) is switched on."),
        ["$.characters.*.Hideout.Areas[].passiveBonusesEnabled"] = new("被动增益生效标记", "Passive bonuses enabled", "该设施提供的角色被动加成是否处于激活状态。", "Whether passive bonuses from this station are active."),
        ["$.characters.*.Hideout.Areas[].completeTime"] = new("升级完成时间戳", "Upgrade completion time", "建造/升级结束的 Unix 秒级时间戳（配合 constructing 使用）。", "Unix timestamp when ongoing construction finishes."),
        ["$.characters.*.Hideout.Areas[].constructing"] = new("正在建造／升级标记", "Under construction flag", "true 表示该设施正在读秒升级中。", "true while a level upgrade is in progress."),
        ["$.characters.*.Hideout.Areas[].slots"] = new("设施耗材／显卡插槽", "Station item slots", "安装在发电机燃料槽、滤水器、显卡架或展示架上的物品槽位。", "Installed fuel tanks, water/air filters, GPUs or display items."),
        ["$.characters.*.Hideout.Production"] = new("藏身处制造／挖矿进度", "Active hideout productions", "正在进行或已完成待领取的配方制作、净水、私酒与比特币挖矿状态。", "In-progress and ready-to-claim crafts, water/booze runs and Bitcoin mining."),
        ["$.characters.*.Hideout.Improvements"] = new("藏身处区域改良状态", "Hideout improvements", "健身房等附加区域改良项完成记录。", "Completed station sub-improvements."),
        ["$.characters.*.Hideout.Seed"] = new("藏身处随机种子", "Hideout random seed", "藏身处局部随机生成种子字符串。", "Random seed string for hideout generation."),
        ["$.characters.*.Hideout.MannequinPoses"] = new("装备假人动作姿势", "Mannequin poses", "装备展示假人各自选定的展示动作 ID。", "Selected pose IDs for gear rack mannequins."),
        ["$.characters.*.Hideout.sptUpdateLastRunTimestamp"] = new("后台产量上次结算时间", "Last SPT hideout tick", "SPT 服务端上次推进发电耗油与制造进度的 Unix 秒级时间戳。", "Unix timestamp when SPT last simulated fuel consumption and production progress."),
        ["$.characters.*.Hideout.Customization"] = new("藏身处墙地顶装潢设置", "Hideout interior customization", "当前应用的墙面、地面、天花板、靶场靶纸与灯光风格 ID。", "Equipped wall, floor, ceiling, shooting-range target and lighting customization IDs."),

        // Quests & Traders & Flea
        ["$.characters.*.Quests"] = new("常规与主线任务状态列表", "Quests status array", "所有已解锁、进行中、待提交、已完成或失败的任务记录。", "All tracked trader and story quest states."),
        ["$.characters.*.Quests[]"] = new("单条任务状态记录", "Quest status entry", "包含任务模板 qid、状态码 status（0未解锁/1可接/2进行中/3可交/4完成/5失败/9冷却中）及完成条件。", "Quest qid, status enum (0 Locked, 1 AvailableForStart, 2 Started, 3 AvailableForFinish, 4 Success, 5 Fail, 9 AvailableAfter) and completedConditions."),
        ["$.characters.*.Quests[].qid"] = new("任务模板 ID (qid)", "Quest template ID", "对应 quests.json 中的 24 位任务 ID。", "24-hex quest template ID."),
        ["$.characters.*.Quests[].status"] = new("任务状态码", "Quest status code", "0=未解锁, 1=可接取, 2=进行中, 3=可交付, 4=已完成, 5=已失败, 9=时间等待中。", "0=Locked, 1=AvailableForStart, 2=Started, 3=AvailableForFinish, 4=Success, 5=Fail, 9=AvailableAfter."),
        ["$.characters.*.Quests[].startTime"] = new("任务接取时间戳", "Quest start timestamp", "接取该任务时的 Unix 秒级时间戳。", "Unix timestamp when the quest was accepted."),
        ["$.characters.*.Quests[].availableAfter"] = new("任务冷却解锁时间戳", "Quest wait unlock time", "status=9 时，任务变为可接取状态的 Unix 秒级时间戳。", "Unix timestamp when a time-gated quest (status 9) unlocks."),
        ["$.characters.*.Quests[].statusTimers"] = new("任务各阶段变更时间", "Status transition timers", "记录进入各任务状态时的历史时间戳字典。", "Timestamps recorded for each status transition."),
        ["$.characters.*.Quests[].completedConditions"] = new("已完成子目标 ID 列表", "Completed condition IDs", "该任务下已勾选完成的条件 ID 数组。", "Condition IDs already satisfied for this quest."),

        ["$.characters.*.RepeatableQuests"] = new("每日／每周／Scav 循环任务", "Repeatable quests", "包含 Daily(日常)、Weekly(周常) 与 Daily_Savage(Fence Scav 任务) 三组动态任务池。", "Daily, Weekly and Fence Scav repeatable quest pools."),
        ["$.characters.*.RepeatableQuests[]"] = new("单组循环任务池", "Repeatable quest pool", "包含任务池名称 name、当前激活任务 activeQuests、刷新截止 endTime 与免费刷新次数。", "Pool name, activeQuests, reset endTime and remaining free rerolls."),

        ["$.characters.*.TradersInfo"] = new("商人好感、交易额与皇冠等级", "Trader standings & loyalty", "全部商人的解锁状态、好感度（Fence 好感即 Scav 业力）、累计交易额与忠诚度等级。", "Unlock status, standing (Fence = Scav karma), sales sum and loyalty level for every trader."),
        ["$.characters.*.TradersInfo.<id>"] = new("单名商人关系数据", "Single trader standing", "包含 standing(好感)、salesSum(交易流水)、loyaltyLevel(皇冠等级 1～4) 与 unlocked(是否解锁)。", "standing, salesSum, loyaltyLevel (1–4) and unlocked state for this trader."),
        ["$.characters.*.TradersInfo.<id>.standing"] = new("商人好感度", "Trader standing", "与该商人的声望浮点数（其中 Fence 579dc571d53a0658a154fbec 的好感即 Scav 业力）。", "Reputation with this trader (Fence's standing is Scav karma)."),
        ["$.characters.*.TradersInfo.<id>.salesSum"] = new("商人累计交易额", "Cumulative sales sum", "在该商人处买卖累计达成的货币流水（用于解锁皇冠等级）。", "Total currency volume traded with this trader toward loyalty requirements."),
        ["$.characters.*.TradersInfo.<id>.loyaltyLevel"] = new("商人皇冠忠诚等级", "Loyalty level (LL)", "当前生效的商人等级（1～4，4 级即皇冠王冠）。", "Current trader loyalty tier (1–4)."),
        ["$.characters.*.TradersInfo.<id>.unlocked"] = new("商人交易解锁开关", "Trader unlocked", "true 表示已在商人界面开放访问（如 Jaeger、Ref、Lightkeeper 解锁状态）。", "Whether this trader is unlocked and accessible."),
        ["$.characters.*.TradersInfo.<id>.disabled"] = new("商人临时禁用标记", "Trader disabled flag", "剧情事件临时关闭商人服务时为 true，正常为 false。", "true if temporarily disabled by a story event; normally false."),

        ["$.characters.*.UnlockedInfo"] = new("已解锁制作配方记录", "Unlocked crafting recipes", "通过任务奖励或蓝图解锁的藏身处特殊工作台/医疗站制作配方 ID。", "Hideout production recipe IDs unlocked via quests or blueprints."),
        ["$.characters.*.UnlockedInfo.unlockedProductionRecipe"] = new("已解锁配方 ID 列表", "Unlocked recipe IDs", "可在藏身处制造列表中看到的额外解锁配方 ID 数组。", "Array of unlocked hideout production scheme IDs."),

        ["$.characters.*.RagfairInfo"] = new("跳蚤市场卖家信誉与统计", "Flea market seller info", "跳蚤市场信誉分、涨跌状态、当前挂单与历史成交额。", "Flea market seller rating, rating trend, active offers and sales totals."),
        ["$.characters.*.RagfairInfo.rating"] = new("跳蚤市场卖家信誉", "Flea market rating", "卖家信誉浮点数，决定原版可同时上架的最大报价槽位数。", "Seller reputation score controlling native max active flea offers."),
        ["$.characters.*.RagfairInfo.isRatingGrowing"] = new("信誉上升趋势标记", "Rating growing indicator", "true 显示绿色向上箭头，false 显示红色向下箭头。", "true shows green upward trend arrow in flea UI."),
        ["$.characters.*.RagfairInfo.offers"] = new("角色挂单缓存", "Profile flea offers", "角色侧挂单列表。", "Profile-side flea market offers array."),
        ["$.characters.*.RagfairInfo.sellSum"] = new("跳蚤累计成交总额", "Total flea sales sum", "在跳蚤市场成功售出的累计金额统计。", "Cumulative rouble value of sold flea offers."),
        ["$.characters.*.RagfairInfo.notSellSum"] = new("跳蚤未售／撤单累计额", "Unsold / expired sum", "过期未售出或手动撤单的累计金额统计。", "Cumulative value of expired or cancelled flea offers."),

        ["$.characters.*.Achievements"] = new("已解锁成就与达成时间", "Unlocked achievements", "键为 24 位成就 ID，值为达成该成就时的 Unix 秒级时间戳。", "Map of unlocked achievement IDs to Unix unlock timestamps."),
        ["$.characters.*.Achievements.<id>"] = new("单项成就解锁时间戳", "Achievement unlock timestamp", "达成该成就的 Unix 秒级时间戳（删除此键即取消解锁该成就）。", "Unix timestamp when this achievement was unlocked."),

        ["$.characters.*.Bonuses"] = new("角色被动加成与扩容列表", "Character passive bonuses", "来自版本特权、藏身处设施升级或任务的持久加成（如 StashSize 仓库扩容、解锁槽位、经验倍率）。", "Permanent bonuses from edition perks, hideout stations and quests (e.g. StashSize, energy reserve, XP rate)."),
        ["$.characters.*.Bonuses[]"] = new("单项角色被动加成", "Bonus entry", "包含加成 id、加成种类 type（如 StashSize、AdditionalSlots）与数值/模板。", "Single passive bonus definition (id, type, value / templateId)."),
        ["$.characters.*.Notes"] = new("玩家手写游戏笔记", "Player notes", "在任务/角色笔记页手动添加的文字备忘录。", "User-written text notes in the Notes tab."),
        ["$.characters.*.CarExtractCounts"] = new("付费汽车撤离点使用次数", "Paid car extract counts", "记录各张地图付费汽车撤离的累计搭乘次数（次数越多单次加的 Fence 好感越少）。", "Per-extract usage counts for paid vehicle extractions (affects diminishing Fence standing gains)."),
        ["$.characters.*.CarExtractCounts.<id>"] = new("单处汽车撤离使用次数", "Car extract usage count", "该汽车撤离点已累计搭乘撤离的次数（改小可恢复高额 Fence 好感奖励）。", "Times extracted via this car exit."),
        ["$.characters.*.CoopExtractCounts"] = new("阵营合作撤离点使用次数", "Co-op extract counts", "记录 PMC+Scav 合作撤离点的累计使用次数。", "Per-extract usage counts for co-op extractions."),
        ["$.characters.*.CoopExtractCounts.<id>"] = new("单处合作撤离使用次数", "Co-op extract usage count", "该合作撤离点累计使用次数。", "Times extracted via this co-op exit."),
        ["$.characters.*.WishList"] = new("愿望单物品字典", "Item wishlist", "玩家加入愿望单高亮提醒的物品模板与分类分组映射。", "Bookmarked item templates and wishlist categories."),
        ["$.characters.*.Variables"] = new("1.0 主线剧情变量表", "Story quest variables", "控制塔科夫 1.0 主线章节分支条件、对话状态与隐藏计数的键值字典。", "Key-value variables driving 1.0 main story branches, counters and triggers."),
        ["$.characters.*.Variables.<id>"] = new("单个剧情变量值", "Story variable value", "对应剧情变量 ID 的当前数值。", "Current numeric/state value of this story variable."),
        ["$.characters.*.moneyTransferLimitData"] = new("战局现金汇款每日限额", "In-raid money transfer limit", "控制 BTR / 地图过图服务每日可寄回仓库的现金上限与重置时间。", "Daily rouble transfer cap and reset timer for BTR / transit cash delivery."),
        ["$.characters.*.BattlePassProgress"] = new("赛季通行证进度", "Battle pass progress", "联动通行证等级进度与已领取奖励 ID 列表。", "Battle pass completion counters and claimed reward IDs."),
        ["$.characters.*.BattlePassUniversalDocumentBalance"] = new("通行证通用代币余额", "Battle pass document balance", "通行证通用情报/凭证货币余额。", "Universal battle pass currency balance."),
        ["$.characters.*.battlePassDocumentLimitData"] = new("通行证代币获取上限数据", "Battle pass token limit", "周期内可获取的通行证凭证上限记录。", "Periodic acquisition limit data for battle pass tokens."),
        ["$.characters.*.TarCoinBalance"] = new("商城币余额 (TarCoin)", "Store TarCoin balance", "角色账户内的商城币整数余额。", "Account TarCoin integer balance."),
        ["$.characters.*.SeasonalPerks"] = new("赛季天赋列表", "Seasonal perks", "已激活的季节活动/赛季专属被动天赋。", "Active seasonal perks array."),
        ["$.characters.*.SeasonalPerkEffectParameters"] = new("赛季天赋效果参数", "Seasonal perk parameters", "赛季天赋数值配置表。", "Parameters for active seasonal perk effects."),
        ["$.characters.*.SeasonalRewards"] = new("已领赛季奖励", "Seasonal rewards", "已发放的季节活动奖励记录。", "Claimed seasonal event rewards."),
        ["$.characters.*.Ending"] = new("1.0 主线结局状态", "Main story ending state", "记录当前选定结局路线 current、已达成结局 achieved、可选结局 available 与展示勋章 display。", "Tracks current story ending route, achieved endings, available endings and displayed ending badge."),
        ["$.characters.*.Ending.current"] = new("当前生效主线结局 ID", "Current story ending ID", "当前角色所处的主线结局标识。", "Active main story ending ID."),
        ["$.characters.*.Ending.achieved"] = new("已达成结局列表", "Achieved endings", "该角色已通关解锁过的全部主线结局 ID 数组。", "Array of all story ending IDs completed by this character."),
        ["$.characters.*.Ending.available"] = new("已解锁可选结局字典", "Available endings map", "当前满足条件可切换的结局项。", "Story endings available to this character."),
        ["$.characters.*.Ending.display"] = new("名片展示结局徽章 ID", "Displayed ending ID", "角色资料页展示的主线结局标识。", "Ending badge ID displayed on the profile card."),
        ["$.characters.*.PDT"] = new("转生关联候选账号数据", "Prestige candidate data", "原版转生系统候选账号关联结构。", "Native PDT candidate account metadata."),
        ["$.characters.*.QuestNotes"] = new("剧情任务线索笔记", "Story quest notes", "主线推进过程中自动收录到手册的任务线索与情报字典。", "Collected story clues and quest journal notes."),
        ["$.characters.*.ReadQuestData"] = new("已读任务红点记录", "Read quest IDs", "已在任务界面点阅消除「新任务」提示的任务 ID 列表。", "Quest IDs already viewed so the unread badge is hidden."),
        ["$.characters.*.CompletableItems"] = new("可交互剧情物品进度", "Completable story items", "磁带、加密优盘、信件等可阅读/播放剧情物品的完成状态表。", "Completion state for inspectable/playable story items such as tapes and documents."),
        ["$.characters.*.UnlockedLocations"] = new("已解锁大地图列表", "Unlocked raid locations", "主线流程中已开放进入的地图标识列表。", "Map location IDs unlocked on this character.")
    };

    public static bool IsLockedPath(string path) => ExactLockedPaths.Contains(path);

    public static bool CanDeletePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || ExactLockedPaths.Contains(path) || UndeletablePaths.Contains(path))
            return false;
        return path.Length > 2 && path.StartsWith("$", StringComparison.Ordinal);
    }

    public static Description Describe(string path, bool english = false)
    {
        bool locked = IsLockedPath(path);
        if (Entries.TryGetValue(path, out var exact))
            return Format(exact, english, locked);

        string normalized = NormalizePath(path, wildcardSide: false);
        if (Entries.TryGetValue(normalized, out var sideSpecific))
            return Format(sideSpecific, english, locked);

        string generic = NormalizePath(path, wildcardSide: true);
        if (Entries.TryGetValue(generic, out var shared))
            return Format(shared, english, locked);

        // Fallback heuristics by leaf key or mod prefix
        string leaf = LeafName(path);
        if (path.StartsWith("$.local", StringComparison.OrdinalIgnoreCase) && !path.AsSpan(2).Contains('.'))
            return english
                ? new("Assistant mod extension field", "Custom profile extension data stored by SPT Assistant.", locked)
                : new("辅助模组扩展字段 (MOD)", "由 SPT Assistant 模组写入角色存档顶层的扩展状态数据。", locked);

        Entry? byLeaf = leaf switch
        {
            "_id" => new("实体唯一 ID (_id)", "Unique entity _id", "24 位十六进制唯一对象标识。", "24-hex unique identifier."),
            "_tpl" => new("模板 ID (_tpl)", "Template ID (_tpl)", "指向服务端物品或实体定义模板的 24 位 ID。", "24-hex template definition ID."),
            "parentId" => new("父级容器／节点 ID", "Parent ID", "所属上级容器或父物品的 _id。", "Parent item or container _id."),
            "slotId" => new("安装槽位名称", "Slot identifier", "安装在父物品上的具体槽位或容器分区名。", "Attachment slot or container grid name."),
            "Current" => new("当前数值", "Current value", "该属性当前的实时数值（不超过 Maximum）。", "Current value of this vital/stat (up to Maximum)."),
            "Maximum" => new("最大上限数值", "Maximum value", "该属性允许恢复到的最大上限。", "Maximum cap for this vital/stat."),
            "Durability" => new("当前剩余耐久", "Current durability", "物品当前的剩余耐久点数。", "Current remaining durability points."),
            "MaxDurability" => new("当前最大耐久上限", "Current max durability", "物品当前可维修恢复到的耐久上限。", "Current repairable maximum durability."),
            "SpawnedInSession" => new("战局内发现标记 (FIR)", "Found in raid (FIR)", "true 表示带战局发现白勾。", "true marks the item as Found in Raid."),
            "StackObjectsCount" => new("堆叠数量", "Stack count", "当前堆叠组内的物品数量。", "Current stack quantity."),
            _ => null
        };
        if (byLeaf != null)
            return Format(byLeaf, english, locked);

        return english
            ? new("Profile JSON node", "Structured field in the SPT character profile.", locked)
            : new("存档 JSON 数据节点", "SPT 角色存档中的结构化字段。", locked);
    }

    static Description Format(Entry entry, bool english, bool locked) =>
        english
            ? new(entry.TitleEn, entry.DetailEn, locked || entry.Locked)
            : new(entry.TitleZh, entry.DetailZh, locked || entry.Locked);

    public static string NormalizePath(string path, bool wildcardSide = true)
    {
        if (string.IsNullOrEmpty(path) || path == "$") return "$";
        var segments = SplitSegments(path);
        var sb = new StringBuilder("$");
        var genericBuilder = new StringBuilder("$");
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg.IsIndex)
            {
                sb.Append("[]");
                genericBuilder.Append("[]");
                continue;
            }

            string parentExact = sb.ToString();
            string parentGeneric = genericBuilder.ToString();
            string key = seg.Key;

            if (wildcardSide && i == 1 && parentExact == "$.characters" && (key == "pmc" || key == "scav"))
            {
                sb.Append(".*");
                genericBuilder.Append(".*");
                continue;
            }
            if (i == 1 && parentExact == "$.characters" && (key == "pmc" || key == "scav"))
            {
                sb.Append('.').Append(key);
                genericBuilder.Append(".*");
                continue;
            }

            if (parentGeneric == "$.characters.*.Health.BodyParts" && BodyPartNames.Contains(key))
            {
                sb.Append(".<part>");
                genericBuilder.Append(".<part>");
                continue;
            }

            if (DynamicMapParents.Contains(parentExact) || DynamicMapParents.Contains(parentGeneric))
            {
                sb.Append(".<id>");
                genericBuilder.Append(".<id>");
                continue;
            }

            sb.Append('.').Append(key);
            genericBuilder.Append('.').Append(key);
        }
        return sb.ToString();
    }

    static string LeafName(string path)
    {
        var segments = SplitSegments(path);
        for (int i = segments.Count - 1; i >= 0; i--)
            if (!segments[i].IsIndex) return segments[i].Key;
        return "$";
    }

    readonly record struct PathToken(bool IsIndex, string Key);

    static List<PathToken> SplitSegments(string path)
    {
        var list = new List<PathToken>();
        int i = path.StartsWith("$", StringComparison.Ordinal) ? 1 : 0;
        while (i < path.Length)
        {
            char c = path[i];
            if (c == '.')
            {
                i++;
                int start = i;
                while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
                if (i > start) list.Add(new(false, path[start..i]));
            }
            else if (c == '[')
            {
                i++;
                if (i < path.Length && path[i] == '"')
                {
                    i++;
                    var key = new StringBuilder();
                    while (i < path.Length && path[i] != '"')
                    {
                        if (path[i] == '\\' && i + 1 < path.Length) i++;
                        key.Append(path[i++]);
                    }
                    while (i < path.Length && path[i] != ']') i++;
                    if (i < path.Length) i++;
                    list.Add(new(false, key.ToString()));
                }
                else
                {
                    while (i < path.Length && path[i] != ']') i++;
                    if (i < path.Length) i++;
                    list.Add(new(true, "[]"));
                }
            }
            else
            {
                int start = i;
                while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
                if (i > start) list.Add(new(false, path[start..i]));
            }
        }
        return list;
    }
}
