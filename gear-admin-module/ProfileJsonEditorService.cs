using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

public static class ProfileJsonEditorService
{
    static readonly string Database = Path.Combine(AppContext.BaseDirectory, "user", "mods", "LocalGearMail", "backups", "profiles.sqlite");
    const string AutoBackupPrefix = "JSON编辑前自动备份";
    const int AutoBackupKeep = 15;
    static readonly MemoryCache ReadSnapshots = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });

    static void Remember(MongoId session, string revision, string snapshot) =>
        ReadSnapshots.Set((session, revision), snapshot, new MemoryCacheEntryOptions { Size = snapshot.Length * 2L });

    public static async ValueTask<string> Handle(
        CharacterDataRequest request,
        MongoId session,
        SptProfile profile,
        SaveServer saves,
        JsonUtil json,
        HttpResponseUtil http,
        TemplateTable templates,
        NotificationSendHelper notifications,
        CancellationToken ct)
    {
        string profilePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "user", "profiles", session + ".json"));
        string current = json.Serialize(profile) ?? throw new ArgumentException("无法序列化当前角色存档。");
        string revision = ProfileRestoreRules.Revision(current);

        if (request.Action == "profileJsonRead")
        {
            Remember(session, revision, current);
            return http.GetBody(new
            {
                ok = true,
                session = session.ToString(),
                profilePath,
                nickname = ProfileRestoreRules.Nickname(current),
                revision,
                bytes = Encoding.UTF8.GetByteCount(current),
                profile = JsonNode.Parse(current)
            });
        }

        if (request.Action != "profileJsonSave")
            throw new ArgumentException("未知 JSON 编辑器操作。");

        string targetPath = string.IsNullOrWhiteSpace(request.Id) ? "$" : request.Id.Trim();
        string? childKey = string.IsNullOrWhiteSpace(request.ChildKey) ? null : request.ChildKey.Trim();
        bool replacingRoot = targetPath == "$" && !request.DeleteNode && childKey == null;
        string? baseline = current;
        if (request.Revision != revision &&
            (replacingRoot || !ReadSnapshots.TryGetValue((session, request.Revision), out baseline)))
            throw new ArgumentException(ProfileJsonEditorRules.EditConflict);

        string? rawValue = request.DeleteNode
            ? null
            : !string.IsNullOrWhiteSpace(request.Preview)
                ? request.Preview
                : request.StashItems is { } element
                    ? element.GetRawText()
                    : throw new ArgumentException("未提供要保存的 JSON 值。");

        SptProfile ApplyEdit(string liveJson)
        {
            string mergedJson = ProfileJsonEditorRules.Apply(
                liveJson, targetPath, rawValue, request.DeleteNode, childKey,
                tpl => templates.Items.ContainsKey(new MongoId(tpl)),
                liveJson == baseline ? null : baseline);
            var candidate = json.Deserialize<SptProfile>(mergedJson)
                ?? throw new ArgumentException("修改后的 JSON 无法转换为当前 SPT 服务端存档模型。");
            if (candidate.CharacterData?.PmcData?.Id != profile.CharacterData?.PmcData?.Id)
                throw new ArgumentException("目标角色身份校验失败。");
            return candidate;
        }

        var updated = ApplyEdit(current);
        ct.ThrowIfCancellationRequested();

        var store = new SqliteSnapshots(Database);
        string tagSuffix = request.DeleteNode
            ? "删除 " + targetPath
            : childKey == ProfileJsonEditorRules.ArrayAppendToken
                ? targetPath + "[+]"
                : childKey != null
                    ? $"{targetPath}.{childKey}"
                    : targetPath;
        string backupLabel = AutoBackupPrefix + " · " + tagSuffix;
        if (backupLabel.Length > 120) backupLabel = backupLabel[..120];
        var rollback = store.Add(session.ToString(), ProfileRestoreRules.Nickname(current), backupLabel, current);
        store.TrimPrefix(session.ToString(), AutoBackupPrefix, AutoBackupKeep);

        if (!ReferenceEquals(profile, saves.GetProfile(session)))
            throw new ArgumentException(ProfileJsonEditorRules.EditConflict);
        if (profile.InraidData?.Location is { Length: > 0 } location && location is not ("none" or "hideout"))
            throw new ArgumentException("请先退出战局。");

        // The SQLite write can overlap native updates. Apply the same edit to the newest
        // state and keep that state for an in-memory rollback if persistence fails.
        string latest = json.Serialize(profile) ?? throw new ArgumentException("无法序列化当前角色存档。");
        if (latest != current)
        {
            if (replacingRoot)
                throw new ArgumentException(ProfileJsonEditorRules.EditConflict);
            updated = ApplyEdit(latest);
            current = latest;
        }
        ct.ThrowIfCancellationRequested();
        int previousTarCoin = profile.CharacterData?.PmcData?.TarCoinBalance ?? 0;
        string expectedJson = json.Serialize(updated) ?? throw new IOException("无法序列化修改后的角色存档。");

        try
        {
            LiveProfileState.Replace(profile, updated);
            await saves.SaveProfileAsync(session, CancellationToken.None);
            string persisted = await File.ReadAllTextAsync(profilePath, CancellationToken.None);
            ProfileJsonEditorRules.VerifySaved(expectedJson, persisted, targetPath, request.DeleteNode, childKey);
        }
        catch
        {
            LiveProfileState.Replace(profile, json.Deserialize<SptProfile>(current) ?? throw new IOException("原存档无法回退。"));
            try { await saves.SaveProfileAsync(session, CancellationToken.None); }
            catch { throw new IOException("JSON 写入失败，内存已回退。可在「数据 → 存档备份」中使用快照 " + rollback.Id[..8] + " 恢复。"); }
            throw new IOException("JSON 保存校验未通过，已自动回退到修改前存档。");
        }

        int nextTarCoin = profile.CharacterData?.PmcData?.TarCoinBalance ?? 0;
        if (nextTarCoin != previousTarCoin)
        {
            try
            {
                await notifications.SendMessageAsync(session, new WsExpansionsBalance
                {
                    EventType = NotificationEventType.UpdateAccountTarcoinBalance,
                    Balance = nextTarCoin
                });
            }
            catch { }
        }

        string savedJson = json.Serialize(profile)!;
        string nextRevision = ProfileRestoreRules.Revision(savedJson);
        Remember(session, nextRevision, savedJson);
        return http.GetBody(new
        {
            ok = true,
            saved = true,
            session = session.ToString(),
            profilePath,
            nickname = ProfileRestoreRules.Nickname(savedJson),
            revision = nextRevision,
            backupId = rollback.Id,
            bytes = Encoding.UTF8.GetByteCount(savedJson),
            profile = JsonNode.Parse(savedJson)
        });
    }
}
