using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using LocalSpt5Esp;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

internal static class ProfileJsonEditorChecks
{
    internal static void Run(string gameRoot, JsonUtil nativeJson, HashSet<string> installedTemplates)
    {
        string sampleProfile = """
        {
          "info": {
            "id": "6adfe3f6ac082b0a00181eaa",
            "scavId": "6adfe3f6ac082b0a00181eab",
            "aid": 1992553,
            "username": "Shirasu_Azusa",
            "wipe": false,
            "edition": "Unheard",
            "totalInGame": 100
          },
          "inraid": {},
          "characters": {
            "pmc": {
              "_id": "6adfe3f6ac082b0a00181eaa",
              "aid": 1992553,
              "sessionId": "6adfe3f6ac082b0a00181eaa",
              "savage": "6adfe3f6ac082b0a00181eab",
              "Info": { "Nickname": "Shirasu_Azusa", "Side": "Usec", "Level": 54, "Experience": 12546579 },
              "Health": { "Energy": { "Current": 100, "Maximum": 110 }, "Hydration": { "Current": 100, "Maximum": 100 }, "BodyParts": {} },
              "Inventory": {
                "equipment": "664115f68b8ae85875060b3b",
                "stash": "664115f68b8ae85875060b39",
                "sortingTable": "664115f68b8ae85875060b3c",
                "questRaidItems": "66c903b7cbd137db330413a2",
                "questStashItems": "676db384777490e23c45b657",
                "items": [
                  { "_id": "664115f68b8ae85875060b3b", "_tpl": "55d7217a4bdc2d86028b456d" },
                  { "_id": "664115f68b8ae85875060b39", "_tpl": "6602bcf19cc643f44a04274b" },
                  { "_id": "664115f68b8ae85875060b3c", "_tpl": "602543c13fee350cd564d032" },
                  { "_id": "66c903b7cbd137db330413a2", "_tpl": "5963866286f7747bf429b572" },
                  { "_id": "676db384777490e23c45b657", "_tpl": "5963866b86f7747bfa1c4462" },
                  { "_id": "6adfe437ac082b0a00181edd", "_tpl": "544fb45d4bdc2dee738b4568", "parentId": "664115f68b8ae85875060b39", "slotId": "hideout", "upd": { "MedKit": { "HpResource": 300 }, "SpawnedInSession": false } }
                ]
              },
              "Skills": { "Common": [ { "Id": "Endurance", "Progress": 2500 } ], "Mastering": [], "Points": 0 },
              "TradersInfo": { "54cb50c76803fa8b248b4571": { "standing": 0.5, "loyaltyLevel": 2, "salesSum": 100000, "unlocked": true } },
              "Quests": [ { "qid": "59674cd986f7744ab26e32f2", "status": 2 } ],
              "TarCoinBalance": 55500
            },
            "scav": {
              "_id": "6adfe3f6ac082b0a00181eab",
              "aid": 1992553,
              "sessionId": "6adfe3f6ac082b0a00181eaa",
              "savage": null,
              "Info": { "Nickname": "Scav", "Side": "Savage", "Level": 10, "Experience": 1000 },
              "Health": {},
              "Inventory": {
                "equipment": "664115f68b8ae85875060b4b",
                "items": [
                  { "_id": "664115f68b8ae85875060b4b", "_tpl": "55d7217a4bdc2d86028b456d" }
                ]
              }
            }
          },
          "localProjectETransmutation": { "Balance": 1200, "Revision": 1, "Learned": [], "Receipts": {} }
        }
        """;

        bool Installed(string tpl) => installedTemplates.Contains(tpl);

        // 1. Scalar & nested path updates
        string afterXp = ProfileJsonEditorRules.Apply(sampleProfile, "$.characters.pmc.Info.Experience", "15000000", false, null, Installed);
        var xpNode = JsonNode.Parse(afterXp)!;
        if (xpNode["characters"]!["pmc"]!["Info"]!["Experience"]!.GetValue<int>() != 15000000)
            throw new Exception("Scalar path update did not persist Experience");

        // 2. Item upd modification & bracket index path
        string afterFir = ProfileJsonEditorRules.Apply(afterXp, "$.characters.pmc.Inventory.items[5].upd.SpawnedInSession", "true", false, null, Installed);
        if (JsonNode.Parse(afterFir)!["characters"]!["pmc"]!["Inventory"]!["items"]![5]!["upd"]!["SpawnedInSession"]!.GetValue<bool>() != true)
            throw new Exception("Nested array item update failed");

        // 3. Add property to object and append to array, then delete
        string withField = ProfileJsonEditorRules.Apply(afterFir, "$.localProjectETransmutation", "9999", false, "BonusEmc", Installed);
        if (JsonNode.Parse(withField)!["localProjectETransmutation"]!["BonusEmc"]!.GetValue<int>() != 9999)
            throw new Exception("Adding object child property failed");

        string withQuest = ProfileJsonEditorRules.Apply(withField, "$.characters.pmc.Quests", "{\"qid\":\"5936d90786f7742b1420ba5b\",\"status\":4}", false, ProfileJsonEditorRules.ArrayAppendToken, Installed);
        if (JsonNode.Parse(withQuest)!["characters"]!["pmc"]!["Quests"]!.AsArray().Count != 2)
            throw new Exception("Appending array element failed");

        string deleted = ProfileJsonEditorRules.Apply(withQuest, "$.localProjectETransmutation.BonusEmc", null, true, null, Installed);
        if (JsonNode.Parse(deleted)!["localProjectETransmutation"]!.AsObject().ContainsKey("BonusEmc"))
            throw new Exception("Deleting custom property failed");

        // 4. Null value assignment works cleanly
        string withNull = ProfileJsonEditorRules.Apply(deleted, "$.characters.pmc.Info.lastCompletedWipe", "null", false, null, Installed);
        if (!JsonNode.Parse(withNull)!["characters"]!["pmc"]!["Info"]!.AsObject().ContainsKey("lastCompletedWipe"))
            throw new Exception("Literal JSON null value was not applied");

        // 5. Unrestricted editing: formerly locked paths (info.id, inraid, custom _tpl) are now editable; only root deletion and syntax errors are rejected
        string editedInraid = ProfileJsonEditorRules.Apply(sampleProfile, "$.inraid", "{\"location\":\"bigmap\"}", false, null, Installed);
        if (JsonNode.Parse(editedInraid)!["inraid"]!["location"]!.GetValue<string>() != "bigmap")
            throw new Exception("inraid field should be freely editable");

        string editedCustomTpl = ProfileJsonEditorRules.Apply(sampleProfile, "$.characters.pmc.Inventory.items[5]._tpl", "\"ffffffffffffffffffffffff\"", false, null, Installed);
        if (JsonNode.Parse(editedCustomTpl)!["characters"]!["pmc"]!["Inventory"]!["items"]![5]!["_tpl"]!.GetValue<string>() != "ffffffffffffffffffffffff")
            throw new Exception("custom/modded item template should be freely editable");

        void ExpectReject(Action act, string reason)
        {
            try { act(); }
            catch (ArgumentException) { return; }
            throw new Exception("Expected rejection for: " + reason);
        }

        ExpectReject(() => ProfileJsonEditorRules.Apply(sampleProfile, "$", null, true, null, Installed), "deleting root document");
        ExpectReject(() => ProfileJsonEditorRules.Apply(sampleProfile, "$.characters.pmc.Info", "{broken json", false, null, Installed), "invalid JSON syntax");

        const string xpPath = "$.characters.pmc.Info.Experience";
        const string itemPath = "$.characters.pmc.Inventory.items[5]";
        const string firPath = itemPath + ".upd.SpawnedInSession";
        const string modPath = "$.localProjectETransmutation";
        var background = JsonNode.Parse(sampleProfile)!;
        background["info"]!["totalInGame"] = 101;
        var liveItem = background["characters"]!["pmc"]!["Inventory"]!["items"]![5]!;
        liveItem["upd"]!["MedKit"]!["HpResource"] = 250;
        string liveJson = background.ToJsonString();
        string rebased = ProfileJsonEditorRules.Apply(liveJson, firPath, "true", false, null, Installed, sampleProfile);
        var rebasedNode = JsonNode.Parse(rebased)!;
        if (rebasedNode["info"]!["totalInGame"]!.GetValue<int>() != 101
            || ProfileJsonEditorRules.ReadNode(rebasedNode, itemPath + ".upd.MedKit.HpResource")!.GetValue<int>() != 250
            || !ProfileJsonEditorRules.ReadNode(rebasedNode, firPath)!.GetValue<bool>())
            throw new Exception("Rebasing a field edit lost unrelated native updates");

        ExpectReject(() => ProfileJsonEditorRules.Apply(afterXp, xpPath, "16000000", false, null, Installed, sampleProfile), "concurrent target edit");
        ExpectReject(() => ProfileJsonEditorRules.Apply(liveJson, itemPath, null, true, null, Installed, sampleProfile), "deleting a concurrently modified item");
        liveItem["_id"] = "6adfe437ac082b0a00181ede";
        ExpectReject(() => ProfileJsonEditorRules.Apply(background.ToJsonString(), firPath, "true", false, null, Installed, sampleProfile), "array index now points to a different item");

        background[modPath[2..]]!["BonusEmc"] = null;
        string withOtherKey = ProfileJsonEditorRules.Apply(background.ToJsonString(), modPath, "42", false, "Bonus", Installed, sampleProfile);
        if (!ProfileJsonEditorRules.ReadNode(JsonNode.Parse(withOtherKey), modPath)!.AsObject().ContainsKey("BonusEmc"))
            throw new Exception("Adding a field discarded a concurrent sibling property");
        ExpectReject(() => ProfileJsonEditorRules.Apply(background.ToJsonString(), modPath, "42", false, "BonusEmc", Installed, sampleProfile), "new field exists as JSON null");
        ExpectReject(() => ProfileJsonEditorRules.Apply(background.ToJsonString(), modPath + ".BonusEmc", "42", false, null, Installed, sampleProfile), "formerly missing target exists as JSON null");

        string appendedAgain = ProfileJsonEditorRules.Apply(withQuest, "$.characters.pmc.Quests",
            "{\"qid\":\"59689fbd86f7740d137ebfc4\",\"status\":1}", false, ProfileJsonEditorRules.ArrayAppendToken, Installed, sampleProfile);
        if (ProfileJsonEditorRules.ReadNode(JsonNode.Parse(appendedAgain), "$.characters.pmc.Quests")!.AsArray().Count != 3)
            throw new Exception("Rebasing an array append lost another append");
        ExpectReject(() => ProfileJsonEditorRules.Apply(liveJson, "$", sampleProfile, false, null, Installed, sampleProfile), "whole-profile replacement after a native update");

        // 6. Field catalog coverage & bilingual purity (no fields are artificially locked)
        string[] checkPaths =
        [
            "$", "$.info.edition", "$.info.id", "$.characters.pmc.Info.Experience",
            "$.characters.pmc.Inventory.items[3].upd.SpawnedInSession",
            "$.characters.pmc.Skills.Common[0].Progress",
            "$.characters.pmc.TradersInfo.54cb50c76803fa8b248b4571.standing",
            "$.characters.pmc.Health.BodyParts.Chest.Health",
            "$.characters.pmc.Quests[0].status",
            "$.localProjectETransmutation.Balance"
        ];
        foreach (string p in checkPaths)
        {
            var zh = ProfileFieldCatalog.Describe(p, false);
            var en = ProfileFieldCatalog.Describe(p, true);
            if (zh.Title == "存档 JSON 数据节点" || en.Title == "Profile JSON node")
                throw new Exception("Catalog missing authored field description for: " + p);
            if (en.Title.Any(c => c is >= '\u3400' and <= '\u9fff') || en.Detail.Any(c => c is >= '\u3400' and <= '\u9fff'))
                throw new Exception("English catalog entry contains Chinese characters: " + p);
            if (zh.Locked || en.Locked)
                throw new Exception("Profile JSON fields must not be artificially locked: " + p);
        }

        // 7. Real profile round-trip check when present on this machine
        string liveProfile = Path.Combine(gameRoot, "SPT_Runtime", "user", "profiles", "6adfe3f6ac082b0a00181eaa.json");
        if (File.Exists(liveProfile))
        {
            string raw = File.ReadAllText(liveProfile);
            var rootObj = (JsonObject)JsonNode.Parse(raw)!;
            foreach (var rootProp in rootObj)
            {
                var d = ProfileFieldCatalog.Describe("$." + rootProp.Key, false);
                if (string.IsNullOrWhiteSpace(d.Title) || string.IsNullOrWhiteSpace(d.Detail))
                    throw new Exception("Empty catalog description for root key: " + rootProp.Key);
            }
            string origBalance = rootObj["characters"]!["pmc"]!["TarCoinBalance"]!.ToJsonString();
            string noopApplied = ProfileJsonEditorRules.Apply(raw, "$.characters.pmc.TarCoinBalance", origBalance, false, null, null);
            var deserialized = nativeJson.Deserialize<SptProfile>(noopApplied)!;
            if (!JsonNode.DeepEquals(rootObj, JsonNode.Parse(nativeJson.Serialize(deserialized)!)))
                throw new Exception("Real 6adfe3f6ac082b0a00181eaa.json profile lost data during SptProfile round-trip");
        }

        Console.WriteLine("PASS profile JSON editor unrestricted path mutations, concurrent edit checks, field catalog and real SptProfile round-trip");
    }
}
