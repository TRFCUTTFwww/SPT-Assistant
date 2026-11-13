using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace LocalQuickhacks;

internal sealed class DoorLease {
    internal readonly WorldInteractiveObject Target;
    internal readonly GamePlayerOwner Owner;
    internal readonly IntPtr World;
    internal readonly string Id;
    internal readonly AuthorizationLatch Authorization = new();
    internal readonly InteractionAction Menu;
    readonly Action callback;
    internal DoorLease(GamePlayerOwner owner, WorldInteractiveObject target) {
        Target=target; Owner=owner; World=Singleton<GameWorld>.Instance.Pointer; Id=target.Id;
        callback=()=>Runtime.OpenDoor(this);
        Menu=new InteractionAction { Name="破解", Disabled=false, Action=DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(callback) };
    }
    internal bool Valid() {
        var world=Singleton<GameWorld>.Instance;
        var player=world?.MainPlayer;
        return Runtime.Active && Runtime.InRaid && Application.isFocused && world?.Pointer==World && Target!=null && Target.Id==Id &&
            Owner?.Player?.Pointer==player?.Pointer && player?.IsYourPlayer==true && player.HealthController?.IsAlive==true &&
            Target.gameObject.activeInHierarchy && !Target.NoInteractionsAllowed &&
            Target.DoorState is EDoorState.Locked or EDoorState.Shut &&
            Vector3.Distance(player.Position,Target.GetInteractionPosition(player.Position))<=4;
    }
    internal void Authorize(bool won) {
        if (!Authorization.Take(won,Valid())) throw new InvalidOperationException("门禁已改变、超出距离，或授权已经处理。");
        var player=Singleton<GameWorld>.Instance.MainPlayer;
        if (Target.TryCast<CardReader>() is {} reader) {
            // Follow the reader's actual success output. Never replace KeyId,
            // key maps or inventory, and never emit the failed-card output.
            var successes=reader._keyMap?.Where(p=>p!=null&&p.Success&&!string.IsNullOrWhiteSpace(p.TriggerId)).Select(p=>p.TriggerId).Distinct().ToArray() ?? [];
            if (successes.Length!=1) throw new InvalidOperationException("该刷卡器存在多个授权出口，暂不支持自动选择。");
            var emitter=Singleton<GameWorld>.Instance.TriggersEmitter;
            if (emitter==null) throw new InvalidOperationException("门禁触发系统尚未就绪。");
            emitter.Emit(successes[0],player.RaidId);
            reader.StartCoroutine(reader.PlayResultSound(true));
        } else if (Target.TryCast<KeycardDoor>() is {} door) {
            door.SetUser(player);
            door.Unlock();
        } else throw new InvalidOperationException("所选对象不是门禁。");
        Plugin.Logger.LogInfo("Breach authorization submitted: "+Id);
    }
}

internal static class DoorHacking {
    static readonly Dictionary<IntPtr,DoorLease> entries=[];
    static IntPtr world;
    internal static int MenuCount { get; private set; }
    internal static void Install(Harmony h) {
        foreach(var type in new[]{typeof(CardReader),typeof(KeycardDoor)}) {
            var method=AccessTools.Method(typeof(InteractionContextHelper),"GetAvailableActions",[typeof(GamePlayerOwner),type,typeof(bool)]);
            if(method==null) throw new MissingMethodException("Native keycard action list: "+type.Name);
            h.Patch(method,postfix:new HarmonyMethod(typeof(DoorHacking),nameof(Append)));
        }
    }
    static void Append(GamePlayerOwner __0, WorldInteractiveObject __1, ref AvailableInteractionState __result) {
        try {
            if(!Runtime.Active||!Plugin.Enabled.Value||!Runtime.InRaid||__0?.Player?.IsYourPlayer!=true||__1==null||__1.NoInteractionsAllowed||!__1.gameObject.activeInHierarchy||__1.DoorState is not (EDoorState.Locked or EDoorState.Shut)) return;
            var current=Singleton<GameWorld>.Instance;
            if(world!=current.Pointer) { Reset(); world=current.Pointer; }
            if(!entries.TryGetValue(__1.Pointer,out var lease)||lease.Id!=__1.Id||lease.Owner.Pointer!=__0.Pointer) {
                if(entries.Count>=32) entries.Clear();
                lease=new DoorLease(__0,__1); entries[__1.Pointer]=lease;
            }
            __result ??=new AvailableInteractionState();
            __result.Actions ??=new Il2CppSystem.Collections.Generic.List<InteractionAction>();
            for(int i=0;i<__result.Actions.Count;i++) if(__result.Actions[i]?.Pointer==lease.Menu.Pointer) return;
            bool hadChoices=__result.Actions.Count>0;
            __result.Actions.Add(lease.Menu);
            // The overload is reached after native proxy/power dispatch. With
            // no carried cards, expose the new operation instead of "no keys".
            if(!hadChoices) __result.Error="";
            if(__result.SelectedAction==null) __result.InitSelected();
            MenuCount++;
        } catch(Exception e) { Runtime.Warn("Keycard menu",e); }
    }
    internal static void Reset() { entries.Clear(); world=IntPtr.Zero; }
}
