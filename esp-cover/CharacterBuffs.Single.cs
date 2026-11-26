using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Stimulator = EFT.HealthSystem.ActiveHealthController.Stimulator;
using BuffSettings = EFT.HealthSystem.EffectsSettings.StimulatorSettings.StimulatorBuffSettings;

namespace LocalSpt5Esp;

internal static partial class CharacterBuffs
{
    const string SingleBuffPrefix = "LocalSpt5.SingleBuff.";
    static readonly EStimulatorBuffType[] SingleTypes = Enum.GetValues<EStimulatorBuffType>();
    static readonly EBodyPart[] Limbs = { EBodyPart.LeftArm, EBodyPart.RightArm, EBodyPart.LeftLeg, EBodyPart.RightLeg };
    static EStimulatorBuffType singleType = EStimulatorBuffType.HealthRate;
    static EBodyPart singlePart = EBodyPart.Chest;
    static string singleDuration = "15", singleStrength = "4", singleSkill = "Strength";
    static Vector2 singleScroll;

    static bool LocalSingleBuff => Singleton<AbstractGame>.Instance?.TryCast<LocalGame>() != null;

    static bool UsesPart(EStimulatorBuffType type) =>
        type is EStimulatorBuffType.LightBleeding or EStimulatorBuffType.HeavyBleeding or EStimulatorBuffType.Fracture;

    static string? SingleBuffName(string key)
    {
        if (!key.StartsWith(SingleBuffPrefix, StringComparison.Ordinal))
            return null;
        int end = key.IndexOf('.', SingleBuffPrefix.Length);
        return end < 0 ? null : BuffRules.StimulatorName(key[SingleBuffPrefix.Length..end]);
    }

    static void DrawSingleBuff(Rect rect)
    {
        float x = rect.x, y = rect.y, w = rect.width;
        float listHeight = Math.Clamp(rect.height - 90, 70, 170);
        float cell = (w - 30) / 3;
        singleScroll = GUI.BeginScrollView(new Rect(x,y,w,listHeight), singleScroll,
            new Rect(0,0,w-20,MathF.Ceiling(SingleTypes.Length/3f)*37));
        for (int i = 0; i < SingleTypes.Length; i++)
        {
            var type = SingleTypes[i];
            if (InterfaceMenu.Button(new Rect(i%3*(cell+5),i/3*37,cell,31), BuffName(type), singleType == type) && singleType != type)
                SelectSingleBuff(type);
        }
        GUI.EndScrollView();
        y += listHeight + 8;
        InterfaceMenu.Label(new Rect(x,y,72,28), "持续秒数", InterfaceMenu.Text,12);
        singleDuration = InterfaceMenu.Field(new Rect(x+74,y,Math.Min(100,w*.19f),28), singleDuration);
        InterfaceMenu.Label(new Rect(x+w*.50f,y,65,28), "强度", InterfaceMenu.Text,12);
        singleStrength = InterfaceMenu.Field(new Rect(x+w*.50f+64,y,Math.Min(100,w*.19f),28), singleStrength);
        y += 36;
        if (UsesPart(singleType) && InterfaceMenu.Button(new Rect(x,y,Math.Min(175,w*.42f),30), "作用部位："+Part(singlePart)))
        {
            var available = singleType == EStimulatorBuffType.Fracture ? Limbs : parts;
            int index = Array.IndexOf(available,singlePart);
            index = (index + 1) % available.Length;
            singlePart = available[index] == EBodyPart.Common ? available[1] : available[index];
        }
        if (singleType == EStimulatorBuffType.SkillRate)
        {
            InterfaceMenu.Label(new Rect(x,y,55,28), "技能", InterfaceMenu.Text,12);
            singleSkill = InterfaceMenu.Field(new Rect(x+56,y,Math.Min(130,w*.34f),28), singleSkill);
        }
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && CanApply && LocalSingleBuff;
        if (InterfaceMenu.Button(new Rect(x+w-160,y,160,30), Batch ? "添加到所选 AI" : "添加／刷新状态"))
            Action(AddSingleBuff);
        GUI.enabled = enabled;
    }

    static BuffSettings? FindSingleBuffDefaults(ActiveHealthController? health, EStimulatorBuffType type)
    {
        if (health == null || Settings(health)?.Stimulator?.Buffs is not { } registered)
            return null;
        foreach (var candidate in registered)
        {
            if (candidate.Key.StartsWith(SingleBuffPrefix,StringComparison.Ordinal) || candidate.Value == null)
                continue;
            for (int i = 0; i < candidate.Value.Length; i++)
                if (candidate.Value[i] is { } entry && entry.BuffType == type)
                    return entry;
        }
        return null;
    }

    static void SelectSingleBuff(EStimulatorBuffType type)
    {
        singleType = type;
        singlePart = type == EStimulatorBuffType.Fracture ? EBodyPart.LeftArm : EBodyPart.Chest;
        var defaults = FindSingleBuffDefaults(Current(), type);
        singleDuration = (defaults?.Duration ?? 60f).ToString(System.Globalization.CultureInfo.InvariantCulture);
        singleStrength = (defaults?.Value ?? 1f).ToString(System.Globalization.CultureInfo.InvariantCulture);
        singleSkill = string.IsNullOrEmpty(defaults?.SkillName) ? nameof(ESkillId.Strength) : defaults!.SkillName;
    }

    static void AddSingleBuff()
    {
        if (!LocalSingleBuff)
            throw new InvalidOperationException("单项 Buff");
        var type = singleType;
        var body = UsesPart(type) ? singlePart : EBodyPart.Common;
        float seconds = BuffRules.Number(singleDuration,-1,float.MaxValue,"持续秒数");
        float value = BuffRules.Number(singleStrength,-float.MaxValue,float.MaxValue,"强度");
        string skill = "";
        if (type == EStimulatorBuffType.SkillRate)
        {
            string input = singleSkill.Trim();
            if (!Enum.TryParse<ESkillId>(input,true,out var id) || !Enum.IsDefined(id))
            {
                string? matched = Enum.GetNames<ESkillId>().FirstOrDefault(name => SkillName(name) == input || I18n.Text(SkillName(name)) == input);
                if (matched == null)
                    throw new ArgumentException("无效技能");
                skill = matched;
            }
            else
                skill = id.ToString();
        }
        ApplyTargets(BuffName(type),(health,tracked) => ApplySingleBuff(health,tracked,type,body,seconds,value,skill));
    }

    static string ApplySingleBuff(ActiveHealthController health, Dictionary<long,int> tracked,
        EStimulatorBuffType type, EBodyPart body, float seconds, float value, string skill)
    {
        if (body != EBodyPart.Common && health.BodyState?.ContainsKey(body) != true)
            throw new ArgumentException("该角色没有可用的效果作用部位。");
        var registered = Settings(health)?.Stimulator?.Buffs;
        if (registered == null)
            throw new InvalidOperationException("效果尚未载入。");

        var defaults = FindSingleBuffDefaults(health,type);
        var definition = new BuffSettings
        {
            BuffType = type, Chance = 1, Delay = 0, Duration = BuffRules.NativeDuration(seconds), Value = value,
            AbsoluteValue = defaults?.AbsoluteValue ?? true, SkillName = skill, ApplyToSide = EPlayerSideMask.All
        };
        if (body != EBodyPart.Common)
        {
            definition.AppliesTo = new Il2CppStructArray<EBodyPart>(1);
            definition.AppliesTo[0] = body;
        }
        var bundle = new Il2CppReferenceArray<BuffSettings>(1);
        bundle[0] = definition;
        string key = SingleBuffPrefix + type + "." + Guid.NewGuid().ToString("N");
        var before = new HashSet<long>(Effects(health).Select(effect => effect.Pointer.ToInt64()));
        registered.Add(key,bundle);
        try
        {
            health.DoExternalBuff(key,0);
            // The native effect must retain the definition before the temporary registration is removed.
            var initialized = FindAppliedEffect()?._buffs;
            if (initialized == null || initialized.Length != 1 || initialized[0]?.Settings?.Pointer != definition.Pointer)
                throw new InvalidOperationException("效果未生效。");
            return "已应用："+BuffName(type);
        }
        catch
        {
            FindAppliedEffect()?.ForceRemove();
            throw;
        }
        finally
        {
            registered.Remove(key);
            Remember(health,before,tracked);
        }

        Stimulator? FindAppliedEffect() => Effects(health).Select(entry => entry.TryCast<Stimulator>())
            .FirstOrDefault(stim => stim?.Name == key);
    }
}