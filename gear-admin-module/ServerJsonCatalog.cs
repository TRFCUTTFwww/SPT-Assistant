using System.Text.RegularExpressions;
using System.Text.Json;
using LocalSpt.Shared;

namespace LocalGearMail;

public sealed record JsonPurpose(string TitleZh, string TitleEn, string DetailZh, string DetailEn);

public static class ServerJsonCatalog
{
    static readonly Dictionary<string, string> CuratedEnglish = ReadEnglish();
    static readonly Dictionary<string, (string Zh, string En)> ItemFields = ReadItemFields();
    static Dictionary<string, (string Zh, string En)> ReadItemFields()
    {
        using var stream = typeof(ServerJsonCatalog).Assembly.GetManifestResourceStream("LocalGearMail.ServerJsonItemFields.tsv");
        if (stream == null) return new();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r').Split('\t')).ToDictionary(p => p[0], p => (p[1], p[2]), StringComparer.Ordinal);
    }
    static Dictionary<string, string> ReadEnglish()
    {
        using var stream = typeof(ServerJsonCatalog).Assembly.GetManifestResourceStream("LocalGearMail.ServerJsonCurated.en.json");
        return stream == null ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
    }
    static readonly Dictionary<string, (string Zh, string En)> Configs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["airdrop"] = ("空投触发、航线与战利品生成", "Airdrop triggers, flights and loot generation"),
        ["backup"] = ("服务端自动存档备份与保留策略", "Automatic profile backups and retention"),
        ["bot"] = ("AI 生成、装备、背包战利品与耐久随机化", "Bot generation, equipment, carried loot and durability"),
        ["btrdelivery"] = ("BTR 运送物品的费用、延迟与邮件", "BTR item delivery costs, delays and mail"),
        ["core"] = ("服务端基础运行规则与调试选项", "Core server behavior and debugging"),
        ["gifts"] = ("兑换码和系统赠礼内容", "Gift codes and system gift contents"),
        ["health"] = ("战后生命、伤势与恢复规则", "Post-raid health, injuries and regeneration"),
        ["hideout"] = ("藏身处建造、制作、消耗与邪教圈", "Hideout construction, crafting, consumption and cultist circle"),
        ["http"] = ("HTTP 服务监听、缓存、证书与网络配置", "HTTP listener, caching, certificates and networking"),
        ["inraid"] = ("出击默认选项、结算、战局标记和撤离好感", "Raid defaults, results, Found in Raid and extraction standing"),
        ["insurance"] = ("保险返还概率、时间与装备回收模拟", "Insurance return chances, delays and recovery simulation"),
        ["inventory"] = ("库存操作、物品移动与容器处理规则", "Inventory operations, item movement and container rules"),
        ["item"] = ("物品黑名单、属性修正与物品生成规则", "Item blacklists, property adjustments and generation rules"),
        ["locale"] = ("服务端语言选择与本地化回退", "Server language selection and localization fallback"),
        ["location"] = ("地图 AI 波次、刷新与地图规则修正", "Map bot waves, spawns and location rule adjustments"),
        ["loot"] = ("地图静态与散落战利品生成倍率", "Static and loose map loot generation multipliers"),
        ["lostondeath"] = ("死亡后各装备栏位与物品的丢失规则", "Equipment slots and items lost on death"),
        ["match"] = ("匹配、战局创建与相关联机选项", "Matchmaking, raid creation and related multiplayer options"),
        ["playerscav"] = ("玩家 Scav 生成、冷却与装备规则", "Player Scav generation, cooldown and equipment"),
        ["pmc"] = ("PMC 阵营、等级、名称、装备与 AI 生成", "PMC factions, levels, names, equipment and bot generation"),
        ["pmcchatresponse"] = ("PMC 对玩家聊天和战后邮件的回应", "PMC responses to player chat and post-raid messages"),
        ["quest"] = ("任务条件、奖励、邮件与任务修正", "Quest conditions, rewards, mail and adjustments"),
        ["ragfair"] = ("跳蚤市场报价、价格、税费与售出模拟", "Flea market offers, prices, fees and sale simulation"),
        ["repair"] = ("商人和修理包费用、耐久损失与技能收益", "Trader and repair kit costs, durability loss and skill gains"),
        ["scavcase"] = ("情报中心 Scav 宝箱的耗时和奖励池", "Scav case durations and reward pools"),
        ["seasonalevents"] = ("季节活动、事件日期与活动开关", "Seasonal events, event dates and activation"),
        ["trader"] = ("商人刷新、价格、购买与 Fence 规则", "Trader restocks, prices, purchases and Fence rules"),
        ["weather"] = ("天气、时间流速与季节切换", "Weather, time progression and seasons")
    };
    static readonly Dictionary<string, (string Zh, string En)> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DELTA_GRENADE_START_TIME"] = ("AI 开始处理手雷危险的时间参数（秒）。", "Time parameter before the bot starts handling grenade danger, in seconds."),
        ["ADD_GRENADE_AS_DANGER"] = ("把手雷加入危险判断的距离（米）；对应 _SQR 字段为距离平方。", "Distance in meters for adding a grenade as a danger; the corresponding _SQR field stores the squared distance."),
        ["ADD_GRENADE_AS_DANGER_SQR"] = ("手雷危险判定距离的平方，与 ADD_GRENADE_AS_DANGER 配套。", "Squared grenade danger distance, paired with ADD_GRENADE_AS_DANGER."),
        ["RUN_AWAY"] = ("AI 躲避手雷的距离阈值（米）；对应 RUN_AWAY_SQR 为距离平方。", "Grenade evasion distance threshold in meters, paired with RUN_AWAY_SQR."),
        ["RUN_AWAY_SQR"] = ("手雷躲避距离的平方，与 RUN_AWAY 配套。", "Squared grenade evasion distance, paired with RUN_AWAY."),
        ["NO_RUN_FROM_AI_GRENADES"] = ("是否不躲避 AI 投掷的手雷；不控制玩家投掷的手雷。", "Whether bots avoid running from grenades thrown by AI. This does not control grenades thrown by players."),
        ["CAN_THROW_GRENADE"] = ("允许该 AI 投掷手雷。", "Allow this bot to throw grenades."),
        ["Grenade"] = ("AI 手雷投掷、危险感知和躲避参数。", "Bot grenade throwing, danger recognition and evasion settings."),
        ["difficulty"] = ("按难度保存的 AI 行为参数。", "Bot behavior settings grouped by difficulty."),
        ["easy"] = ("简单难度的行为参数。", "Behavior settings for easy difficulty."),
        ["normal"] = ("普通难度的行为参数。", "Behavior settings for normal difficulty."),
        ["hard"] = ("困难难度的行为参数。", "Behavior settings for hard difficulty."),
        ["impossible"] = ("极难难度的行为参数。", "Behavior settings for impossible difficulty."),
        ["Aiming"] = ("AI 瞄准速度、精度与瞄准修正。", "Bot aiming speed, accuracy and aiming adjustments."),
        ["Look"] = ("AI 视野、可见距离与目标识别。", "Bot field of view, visible distance and target recognition."),
        ["Hearing"] = ("AI 对声音的探测与反应。", "Bot sound detection and reactions."),
        ["Mind"] = ("AI 思维、敌我关系与决策。", "Bot decisions, enemy relationships and behavior."),
        ["Shoot"] = ("AI 射击、点射与开火间隔。", "Bot shooting, bursts and firing intervals."),
        ["Core"] = ("AI 基础感知与战斗参数。", "Core bot perception and combat parameters."),
        ["Patrol"] = ("AI 巡逻与非战斗移动。", "Bot patrol and movement outside combat."),
        ["Move"] = ("AI 移动与转向参数。", "Bot movement and turning parameters."),
        ["inventory"] = ("可生成的装备、弹药与战利品池。", "Available equipment, ammunition and loot pools."),
        ["appearance"] = ("AI 外观部件及随机选取权重。", "Bot appearance parts and selection weights."),
        ["chances"] = ("装备或物品出现的概率配置。", "Equipment or item spawn chance settings."),
        ["generation"] = ("生成物品数量、类别与随机范围。", "Generated item counts, categories and random ranges."),
        ["experience"] = ("角色等级、击杀经验等经验数据。", "Level, kill experience and related experience data."),
        ["health"] = ("身体部位生命值与相关生成范围。", "Body part health and related generation ranges."),
        ["skills"] = ("技能等级或技能效果配置。", "Skill levels or skill effects."),
        ["_id"] = ("当前记录的唯一标识。", "Unique identifier of this record."),
        ["_tpl"] = ("引用的物品模板 ID。", "Referenced item template ID."),
        ["_parent"] = ("所属父模板或物品分类的 ID。", "ID of the parent template or item category."),
        ["_props"] = ("物品模板的属性集合。", "Properties of the item template."),
        ["_name"] = ("内部名称。", "Internal name."),
        ["_type"] = ("模板或记录的类型。", "Template or record type."),
        ["parentId"] = ("父物品或父记录 ID。", "Parent item or record ID."),
        ["slotId"] = ("物品所在槽位。", "Slot containing this item."),
        ["StackObjectsCount"] = ("当前堆叠数量。", "Current stack size."),
        ["Caliber"] = ("弹药或武器的口径标识。", "Ammunition or weapon caliber identifier."),
        ["Damage"] = ("弹药基础伤害。", "Base ammunition damage."),
        ["PenetrationPower"] = ("弹药穿透能力。", "Ammunition penetration power."),
        ["InitialSpeed"] = ("弹药初速（米/秒）。", "Muzzle velocity in meters per second."),
        ["Weight"] = ("物品重量（千克）。", "Item weight in kilograms."),
        ["Width"] = ("物品在格子库存中的宽度。", "Item width in inventory cells."),
        ["Height"] = ("物品在格子库存中的高度。", "Item height in inventory cells."),
        ["MaxDurability"] = ("物品最大耐久值。", "Maximum item durability."),
        ["Durability"] = ("物品耐久或耐久生成设置。", "Item durability or durability generation settings."),
        ["Ergonomics"] = ("武器或配件的人体工学数值。", "Weapon or attachment ergonomics value."),
        ["RecoilForceUp"] = ("武器垂直后坐力参数。", "Vertical weapon recoil parameter."),
        ["RecoilForceBack"] = ("武器水平后坐力参数。", "Horizontal weapon recoil parameter."),
        ["armorClass"] = ("护甲防护等级。", "Armor protection class."),
        ["ammoType"] = ("弹药类型。", "Ammunition type."),
        ["StackMaxSize"] = ("物品每堆允许的最大数量。", "Maximum number of items in one stack."),
        ["SpawnPointParams"] = ("地图出生点、阵营和位置参数。", "Map spawn points, factions and positions."),
        ["BossLocationSpawn"] = ("BOSS 及随从的出生位置、概率和触发规则。", "Boss and follower spawn locations, chances and triggers."),
        ["waves"] = ("AI 刷新波次。", "Bot spawn waves."),
        ["exits"] = ("地图撤离点与撤离条件。", "Map extractions and extraction conditions."),
        ["EscapeTimeLimit"] = ("战局时限（分钟）。", "Raid time limit in minutes."),
        ["BossChance"] = ("BOSS 刷新概率（百分比）。", "Boss spawn chance as a percentage."),
        ["BossName"] = ("生成的 BOSS AI 类型。", "Bot type used for the boss."),
        ["BossEscortType"] = ("BOSS 随从 AI 类型。", "Bot type used for boss followers."),
        ["BossEscortAmount"] = ("BOSS 随从数量或数量范围。", "Boss follower count or count range."),
        ["BossZone"] = ("BOSS 可生成的地图区域。", "Map zones where the boss can spawn."),
        ["items"] = ("物品记录列表或物品 ID 对照表。", "Item records or an item ID lookup table."),
        ["barter_scheme"] = ("商品兑换需求，外层为可选方案，内层为同时需要的物品。", "Barter requirements: outer arrays are alternatives; inner entries are required together."),
        ["loyal_level_items"] = ("购买各商品所需的商人忠诚度等级。", "Trader loyalty level required to purchase each item."),
        ["loyaltyLevels"] = ("商人各忠诚度等级的解锁条件和交易规则。", "Unlock requirements and trade rules for each trader loyalty level."),
        ["conditions"] = ("任务、交易或事件的触发条件。", "Conditions for quests, trades or events."),
        ["rewards"] = ("任务或事件奖励。", "Quest or event rewards."),
        ["production"] = ("藏身处制作配方。", "Hideout production recipes."),
        ["requirements"] = ("执行该操作所需的前置条件和消耗。", "Prerequisites and costs for this operation."),
        ["min"] = ("所属参数的下限。", "Lower bound of the containing setting."),
        ["max"] = ("所属参数的上限。", "Upper bound of the containing setting."),
        ["enabled"] = ("所属功能的启用开关。", "Enable switch for the containing feature."),
        ["enable"] = ("所属功能的启用开关。", "Enable switch for the containing feature.")
    };
    public static JsonPurpose File(string id)
    {
        string p = id.Replace('\\', '/'), name = Path.GetFileNameWithoutExtension(p);
        (string Zh, string En) purpose;
        if (p.StartsWith("SPT_Data/configs/", StringComparison.OrdinalIgnoreCase) && Configs.TryGetValue(name, out purpose)) { }
        else if (p.Contains("/bots/types/")) purpose = ($"{name} AI 的难度行为、装备、外观与生成数据", $"Difficulty behavior, equipment, appearance and generation data for {name} bots");
        else if (p.EndsWith("/bots/core.json")) purpose = ("全局 AI 行为、感知与战斗参数", "Global bot behavior, perception and combat settings");
        else if (p.Contains("/locations/")) purpose = name.ToLowerInvariant() switch
        {
            "base" => ("地图基础规则、AI 波次、出生点与撤离条件", "Map rules, bot waves, spawn points and extraction conditions"),
            "allextracts" => ("地图撤离点定义", "Map extraction definitions"),
            "looseloot" => ("地图散落战利品位置、概率与物品池", "Loose loot positions, chances and item pools"),
            "staticloot" => ("静态容器战利品数量与物品池", "Static container loot counts and item pools"),
            "staticcontainers" => ("静态容器位置与容器类型", "Static container positions and types"),
            "staticammo" => ("地图静态弹药生成池", "Static map ammunition pools"),
            _ => ("地图场景与刷新数据", "Map scene and spawn data")
        };
        else if (p.Contains("/traders/")) purpose = name.ToLowerInvariant() switch
        {
            "assort" => ("商人商品、兑换需求与忠诚度要求", "Trader assortment, barter costs and loyalty requirements"),
            "questassort" => ("任务进度解锁的商人商品", "Trader items unlocked by quest progress"),
            "dialogue" => ("商人对话与邮件模板", "Trader dialogue and mail templates"),
            "base" => ("商人身份、修理、保险、忠诚度与交易限制", "Trader identity, repair, insurance, loyalty and trade limits"),
            _ => ("商人扩展数据", "Additional trader data")
        };
        else if (p.Contains("/hideout/")) purpose = ("藏身处设施、配方、升级条件与奖励", "Hideout areas, recipes, upgrade requirements and rewards");
        else if (p.Contains("/locales/", StringComparison.OrdinalIgnoreCase)) purpose = ("游戏文本、名称与本地化字符串", "Game text, names and localization strings");
        else if (p.Contains("/templates/")) purpose = name.ToLowerInvariant() switch
        {
            "items" => ("全部物品模板、武器、弹药与装备属性", "Item templates, weapon, ammunition and equipment properties"),
            "quests" => ("任务定义、条件、奖励与解锁关系", "Quest definitions, conditions, rewards and unlock relationships"),
            "prices" => ("物品基础价格表", "Base item price table"),
            "handbook" => ("百科物品分类与手册价格", "Handbook item categories and prices"),
            "profiles" => ("新角色存档模板与初始装备", "New profile templates and starting equipment"),
            _ => ("游戏模板和基础数据：" + name, "Game templates and base data: " + name)
        };
        else if (p.EndsWith("/globals.json")) purpose = ("全局经济、技能、战斗、经验与角色规则", "Global economy, skills, combat, experience and character rules");
        else if (p.EndsWith("/server.json")) purpose = ("服务端连接地址与端口信息", "Server connection address and port information");
        else if (p.EndsWith("/settings.json") && p.Contains("/database/")) purpose = ("原生客户端使用的全局基础设置", "Global base settings consumed by the native client");
        else if (p.Contains("/season/")) purpose = ("季节与事件的基础数据", "Base season and event data");
        else if (p.Contains("/match/")) purpose = ("匹配与战局数据", "Matchmaking and raid data");
        else if (p.Contains("/shop/")) purpose = ("游戏商店与购买数据", "Game shop and purchase data");
        else if (p.StartsWith("user/mods/")) purpose = ("已安装 MOD 的 JSON 配置或数据：" + p.Split('/')[2], "Installed mod JSON configuration or data: " + p.Split('/')[2]);
        else purpose = ("服务端 JSON 配置或数据", "Server JSON configuration or data");
        return new(name, name, purpose.Zh + "。", purpose.En + ".");
    }
    public static JsonPurpose Field(string id, string path, JsonValueKind kind, Func<string, string, string?>? modelDoc = null)
    {
        if (path == "$") return File(id);
        var parts = JsonEditPath.Parse(path);
        string key = parts[^1].Key ?? "[" + parts[^1].Index + "]";
        string? parentKey = parts.Count > 1 ? parts[^2].Key : null;
        if (id.StartsWith("SPT_Data/configs/", StringComparison.OrdinalIgnoreCase))
        {
            string file = Path.GetFileNameWithoutExtension(id);
            string dotted = string.Join(".", parts.Select(p => p.Key ?? p.Index.ToString()));
            var curated = ServerSettingsRules.Definitions.FirstOrDefault(d => d.File == file && d.Path == dotted);
            if (curated != null) return new(curated.Name, CuratedEnglish.GetValueOrDefault(curated.Name, key), curated.Help, CuratedEnglish.GetValueOrDefault(curated.Help) ?? modelDoc?.Invoke(id, path) ?? "Server setting: " + key);
        }
        if (Fields.TryGetValue(key, out var description)) return new(key, key, description.Zh, description.En);
        string? doc = id.StartsWith("SPT_Data/", StringComparison.OrdinalIgnoreCase) ? modelDoc?.Invoke(id, path) : null;
        if (!string.IsNullOrWhiteSpace(doc)) return new(key, key, "原生字段说明：" + doc, doc);
        if (id.EndsWith("/templates/items.json", StringComparison.OrdinalIgnoreCase) && ItemFields.TryGetValue(key, out var item)) return new(key, key, item.Zh, item.En);
        if (parts[^1].Key == null) return new(key, key, $"{parentKey ?? "当前数组"} 的第 {parts[^1].Index + 1} 个元素；顺序和下标属于配置内容。", $"Element {parts[^1].Index + 1} in {parentKey ?? "the current array"}; order and indices are part of the configuration.");
        if (Regex.IsMatch(key, "^[0-9a-fA-F]{24}$"))
        {
            bool weights = kind == JsonValueKind.Number && id.Contains("/bots/types/") && (path.Contains("[\"appearance\"]") || path.Contains("[\"inventory\"]"));
            return new(key, key, weights ? "物品或外观 ID 对应的随机选择权重；值越大，被选中的相对机会越高。" : "以实体 ID 为键的数据项；用途由上级表决定。", weights ? "Random selection weight for this item or appearance ID; larger weights increase its relative chance." : "Record keyed by an entity ID; its role is determined by the parent table.");
        }
        string type = kind.ToString().ToLowerInvariant();
        string container = parentKey ?? Path.GetFileNameWithoutExtension(id);
        return new(key, key, $"{container} 中的字段 {key}（{type}）；当前服务端未提供该字段的用途说明。", $"Field {key} in {container} ({type}); this server provides no declared description for this field.");
    }
}
