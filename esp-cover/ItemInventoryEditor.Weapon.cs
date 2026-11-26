using System.Globalization;
using EFT.InventoryLogic;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class ItemInventoryEditor
{
    static readonly List<WeaponEditor.TuningField> weaponFields = new();
    static readonly Dictionary<string, string> weaponDrafts = new(StringComparer.Ordinal);
    static readonly HashSet<string> weaponEnabled = new(StringComparer.Ordinal);
    static bool weaponScopeTemplate, weaponIgnoreMods, weaponShowPresets;
    static string weaponPresetName = "", weaponPresetId = "", weaponReadback = "", weaponFilter = "";
    static Vector2 weaponScroll, weaponPresetScroll;

    static void ResetWeapon(Item? item)
    {
        weaponFields.Clear();
        weaponDrafts.Clear();
        weaponEnabled.Clear();
        weaponScopeTemplate = false;
        weaponIgnoreMods = false;
        weaponShowPresets = false;
        weaponPresetName = "";
        weaponPresetId = "";
        weaponReadback = "";
        weaponFilter = "";
        weaponScroll = weaponPresetScroll = Vector2.zero;
        if (item?.TryCast<Weapon>() is Weapon gun) LoadWeapon(gun, target?.IsInRaid ?? CurrentRaid() != IntPtr.Zero);
    }

    static void LoadWeapon(Weapon gun, bool inRaid)
    {
        weaponFields.Clear();
        weaponFields.AddRange(WeaponEditor.InspectFields(gun.Template));
        weaponDrafts.Clear();
        weaponEnabled.Clear();
        foreach (var field in weaponFields)
            weaponDrafts[field.Path] = WeaponEditor.ReadFieldValue(gun.Template, field).ToString("G9", CultureInfo.InvariantCulture);
        weaponIgnoreMods = WeaponEditor.IsContextIgnoringMods(gun, inRaid);
        weaponReadback = WeaponEditor.FormatFinal(gun);
    }

    static Dictionary<string, double> ReadWeaponValues()
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var field in weaponFields)
            if (weaponEnabled.Contains(field.Path))
                values[field.Path] = WeaponEditorRules.Number(weaponDrafts.GetValueOrDefault(field.Path, "0"), field.Integer, field.Minimum);
        return values;
    }

    static void ApplyWeaponTab(Target current)
    {
        try
        {
            if (!current.IsCurrent()) throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
            var gun = current.Item.TryCast<Weapon>() ?? throw new ArgumentException("该物品不是枪械。");
            if (current.IsInRaid && ChatFikaSession.Role != 0) throw new ArgumentException("FIKA 战局暂不支持右键修改枪械参数。");
            var values = ReadWeaponValues();
            if (values.Count == 0 && weaponIgnoreMods == WeaponEditor.IsContextIgnoringMods(gun, current.IsInRaid))
                throw new ArgumentException("请至少勾选一项枪械参数，或切换忽略配件开关。");
            WeaponEditor.ApplyContextWeapon(gun, current.IsInRaid, weaponScopeTemplate, !weaponScopeTemplate && weaponIgnoreMods, values);
            weaponReadback = WeaponEditor.FormatFinal(gun);
            notice = (weaponScopeTemplate ? "同型号武器模板参数已应用。" : "当前这把枪的数值已应用。") + "  " + weaponReadback;
            FeatureToasts.Show("枪械数值已应用");
        }
        catch (Exception error) { notice = error.Message; }
    }

    static void RestoreWeaponTab(Target current)
    {
        try
        {
            if (!current.IsCurrent()) throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
            var gun = current.Item.TryCast<Weapon>() ?? throw new ArgumentException("该物品不是枪械。");
            WeaponEditor.RestoreContextWeapon(gun, current.IsInRaid, weaponScopeTemplate);
            LoadWeapon(gun, current.IsInRaid);
            notice = "已恢复枪械原值。  " + weaponReadback;
        }
        catch (Exception error) { notice = error.Message; }
    }

    static void DrawWeapon(Rect rect, Target current)
    {
        var gun = current.Item.TryCast<Weapon>();
        if (gun == null) return;
        if (weaponFields.Count == 0) LoadWeapon(gun, current.IsInRaid);
        float x = rect.x, y = rect.y, w = rect.width;
        InterfaceMenu.Label(new(x, y, Math.Max(80, w - 284), 26), weaponReadback, InterfaceMenu.Accent, 12, translate: false);
        if (InterfaceMenu.Button(new(x + w - 276, y, 88, 26), "重读数值")) { LoadWeapon(gun, current.IsInRaid); notice = "已重新读取当前枪械数值。"; }
        if (InterfaceMenu.Button(new(x + w - 182, y, 86, 26), "恢复原值")) RestoreWeaponTab(current);
        if (InterfaceMenu.Button(new(x + w - 90, y, 90, 26), "参数方案", weaponShowPresets)) weaponShowPresets = !weaponShowPresets;
        y += 32;

        float half = (w - 8) / 2;
        if (InterfaceMenu.Button(new(x, y, half, 28), weaponScopeTemplate ? "范围：同型号模板（含 AI）" : "范围：仅当前这把枪", weaponScopeTemplate))
        {
            weaponScopeTemplate = !weaponScopeTemplate;
            if (weaponScopeTemplate) weaponIgnoreMods = false;
        }
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !weaponScopeTemplate;
        if (InterfaceMenu.Button(new(x + half + 8, y, half, 28), weaponIgnoreMods ? "忽略配件影响：开启" : "忽略配件影响：关闭", weaponIgnoreMods))
            weaponIgnoreMods = !weaponIgnoreMods;
        GUI.enabled = oldEnabled;
        y += 34;

        if (weaponShowPresets)
        {
            var presets = WeaponEditor.SavedPresets();
            float boxH = Math.Min(160, Math.Max(68, presets.Count * 36 + 12));
            InterfaceMenu.Surface(new(x, y, w, boxH), Color.Lerp(InterfaceMenu.Background, InterfaceMenu.Text, .04f), 6);
            weaponPresetScroll = GUI.BeginScrollView(new(x + 6, y + 6, w - 12, boxH - 12), weaponPresetScroll, new(0, 0, w - 32, Math.Max(32, presets.Count * 34)));
            try
            {
                if (presets.Count == 0)
                    InterfaceMenu.Label(new(6, 4, w - 40, 24), "暂无已保存的枪械参数方案；勾选参数后可在下方保存。", null, 12);
                for (int i = 0; i < presets.Count; i++)
                {
                    var p = presets[i];
                    if (InterfaceMenu.Button(new(0, i * 34, w - 34, 30), $"{p.Name}  ·  {p.Values.Count} 项", weaponPresetId == p.Id))
                    {
                        weaponEnabled.Clear();
                        foreach (var pair in p.Values)
                            if (weaponFields.Any(f => f.Path == pair.Key))
                            {
                                weaponEnabled.Add(pair.Key);
                                weaponDrafts[pair.Key] = pair.Value.ToString("G9", CultureInfo.InvariantCulture);
                            }
                        weaponIgnoreMods = !weaponScopeTemplate && p.IgnoreAttachments;
                        weaponPresetId = p.Id; weaponPresetName = p.Name;
                        weaponShowPresets = false;
                        notice = "已载入参数方案草稿：" + p.Name;
                    }
                }
            }
            finally { GUI.EndScrollView(); }
            y += boxH + 6;
        }

        weaponFilter = InterfaceMenu.Field(new(x, y, Math.Max(80, w - 216), 28), weaponFilter);
        if (weaponFilter.Length == 0) InterfaceMenu.Label(new(x + 8, y, Math.Max(60, w - 230), 28), "筛选参数名称…", Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .45f), 11);
        if (InterfaceMenu.Button(new(x + w - 210, y, 100, 28), "全选当前"))
            foreach (var f in weaponFields) if (weaponFilter.Length == 0 || (f.Name + " " + f.Path).Contains(weaponFilter, StringComparison.OrdinalIgnoreCase)) weaponEnabled.Add(f.Path);
        if (InterfaceMenu.Button(new(x + w - 104, y, 104, 28), "清空勾选")) weaponEnabled.Clear();
        y += 34;

        var filtered = weaponFields.Where(f => weaponFilter.Length == 0 || (f.Name + " " + f.Path).Contains(weaponFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
        float listHeight = Math.Max(60, rect.yMax - y - 36);
        var listRect = new Rect(x, y, w, listHeight);
        weaponScroll = GUI.BeginScrollView(listRect, weaponScroll, new(0, 0, w - 20, filtered.Length * 36 + 8));
        try
        {
            int from = Math.Max(0, (int)(weaponScroll.y / 36) - 1), end = Math.Min(filtered.Length, (int)((weaponScroll.y + listHeight) / 36) + 2);
            for (int i = from; i < end; i++)
            {
                var field = filtered[i];
                float rowY = i * 36;
                bool checkedField = weaponEnabled.Contains(field.Path);
                if (InterfaceMenu.Button(new(0, rowY, Math.Max(100, (w - 28) * .64f), 30), (checkedField ? "✓ " : "＋ ") + field.Name, checkedField))
                {
                    if (!weaponEnabled.Remove(field.Path)) weaponEnabled.Add(field.Path);
                }
                string before = weaponDrafts.GetValueOrDefault(field.Path, "0");
                string edited = InterfaceMenu.Field(new((w - 28) * .66f, rowY, (w - 28) * .34f, 30), before);
                if (edited != before) { weaponDrafts[field.Path] = edited; weaponEnabled.Add(field.Path); }
            }
        }
        finally { GUI.EndScrollView(); }

        float sy = rect.yMax - 30;
        weaponPresetName = InterfaceMenu.Field(new(x, sy, Math.Max(80, w - 196), 28), weaponPresetName);
        if (weaponPresetName.Length == 0) InterfaceMenu.Label(new(x + 8, sy, Math.Max(60, w - 210), 28), "输入方案名以保存枪械参数预设…", Color.Lerp(InterfaceMenu.Text, InterfaceMenu.Background, .45f), 11);
        if (InterfaceMenu.Button(new(x + w - 190, sy, 90, 28), "保存方案"))
        {
            try { weaponPresetId = WeaponEditor.SaveTuningPreset(weaponPresetName, weaponPresetId, false, gun.StringTemplateId, weaponIgnoreMods, ReadWeaponValues()); notice = "已保存参数方案：" + weaponPresetName; }
            catch (Exception error) { notice = error.Message; }
        }
        if (InterfaceMenu.Button(new(x + w - 94, sy, 94, 28), "另存为"))
        {
            try { weaponPresetId = WeaponEditor.SaveTuningPreset(weaponPresetName, weaponPresetId, true, gun.StringTemplateId, weaponIgnoreMods, ReadWeaponValues()); notice = "已另存参数方案：" + weaponPresetName; }
            catch (Exception error) { notice = error.Message; }
        }
    }
}
