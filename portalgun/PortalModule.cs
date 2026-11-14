using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.InputSystem;
using HarmonyLib;
using Local.ModHost;
using UnityEngine;
using System.Text.Json;
using Object=UnityEngine.Object;

namespace LocalPortalGun;

public sealed partial class PortalModule : IHotModule {
    sealed class Settings {
        public float Volume {get;set;}=.65f;
        public float Range {get;set;}=100;
        public bool LiveView {get;set;}=true;
        public int ViewSize {get;set;}=512;
        public int ViewFps {get;set;}=20;
    }
    ModuleContext context=null!;
    Settings settings=new();
    static PortalModule? active;
    bool equipped,valid,havePrevious;
    int mask;
    IntPtr worldId,playerId;
    Player? player;
    Camera? view;
    Vector3 previous;
    float cooldown,nextShot,noticeUntil,lastShot=-100;
    string notice="";
    readonly Portal?[] portals=new Portal?[2];
    public int ApiVersion=>1;
    public string Version=>"0.1.0";
    public bool CanReload=>true;

    public void Initialize(ModuleContext value) {
        context=value;
        context.OnShutdown(DisposeAssets);
        context.OnShutdown(()=>{ResetRaid();if(ReferenceEquals(active,this))active=null;});
        string file=Path.Combine(context.DataDirectory,"settings.json");
        if(File.Exists(file))settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(file))??new();
        else File.WriteAllText(file,JsonSerializer.Serialize(settings,new JsonSerializerOptions {WriteIndented=true}));
        settings.Volume=float.IsFinite(settings.Volume)?Math.Clamp(settings.Volume,0,1):.65f;
        settings.Range=float.IsFinite(settings.Range)?Math.Clamp(settings.Range,5,200):100;
        settings.ViewSize=Math.Clamp(settings.ViewSize,256,1024);settings.ViewFps=Math.Clamp(settings.ViewFps,5,60);
        mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
        if(mask==0)throw new InvalidOperationException("EFT world collision layers unavailable");
        LoadAssets();
        var h=new Harmony(context.PatchId);
        h.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed",[typeof(bool)]),prefix:new HarmonyMethod(typeof(PortalModule),nameof(Trigger)));
        h.Patch(AccessTools.Method(typeof(GamePlayerOwner),"TranslateCommand"),prefix:new HarmonyMethod(typeof(PortalModule),nameof(Command)));
        active=this;
        context.Log(LogLevel.Info,$"Portal gun {Version} ready; standalone item {PortalIdentity.Template}; LMB blue, RMB orange, Backspace clear. Assets preloaded.");
    }
    static bool IsGun(Player? actor)=>actor?.HandsController?.Item?.TemplateId==PortalIdentity.Template;
    static bool Trigger(Player.FirearmController __instance,bool __0)=>!__0||active==null||!IsGun(__instance._player);
    static bool Command(GamePlayerOwner __instance,ECommand __0,ref InputNode.ETranslateResult __result) {
        if(active==null||__instance.Player?.IsYourPlayer!=true||!IsGun(__instance.Player))return true;
        // Only intercept held-weapon commands. Movement and opening menus stay native.
        if(__0 is not (ECommand.ToggleShooting or ECommand.EndShooting or ECommand.ToggleAlternativeShooting or ECommand.PressAlternativeShooting or ECommand.EndAlternativeShooting or ECommand.ReloadWeapon or ECommand.QuickReloadWeapon or ECommand.CheckChamber or ECommand.ChamberUnload or ECommand.CheckAmmo or ECommand.UnloadMagazine or ECommand.ExamineWeapon or ECommand.CheckFireMode or ECommand.ChangeWeaponMode or ECommand.ForceAutoWeaponMode))return true;
        __result=InputNode.ETranslateResult.BlockAll;return false;
    }
    public void Tick() {
        if(!context.IsActive)return;
        var world=Singleton<GameWorld>.Instance;var game=Singleton<AbstractGame>.Instance;
        player=world?.MainPlayer;
        valid=game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null&&player?.IsYourPlayer==true&&player.HealthController?.IsAlive==true;
        if(!valid){if(worldId!=IntPtr.Zero)ResetRaid();return;}
        if(worldId!=world!.Pointer||playerId!=player!.Pointer){ResetRaid();worldId=world.Pointer;playerId=player!.Pointer;}
        view=CameraManager.Instance?.Camera??Camera.main;
        if(view==null)return;
        bool usable=Application.isFocused&&!Cursor.visible&&Cursor.lockState==CursorLockMode.Locked&&Time.timeScale>.001f;
        Equip(IsGun(player));
        if(!usable){havePrevious=false;gunRoot?.SetActive(false);RestoreHands();return;}
        if(equipped&&!Input.GetKey(KeyCode.LeftControl)&&!Input.GetKey(KeyCode.RightControl)&&!Input.GetKey(KeyCode.LeftAlt)&&!Input.GetKey(KeyCode.RightAlt)) {
            if(Input.GetKeyDown(KeyCode.Backspace)){ClearPortals();Play("close");Tell("Portals cleared");}
            if(Time.unscaledTime>=nextShot) {
                if(Input.GetMouseButtonDown(0))Shoot(0);
                else if(Input.GetMouseButtonDown(1))Shoot(1);
            }
        }
        Vector3 body=player!.Position+Vector3.up*.9f;
        if(havePrevious&&Time.unscaledTime>=cooldown&&portals[0]!=null&&portals[1]!=null) {
            for(int i=0;i<2;i++) {
                var from=portals[i]!;
                if(!Alive(from)){ClearPortals();break;}
                bool floor=(from.Rotation*Vector3.forward).y>.9f;
                var correction=floor?Vector3.down*.78f:Vector3.zero;
                var before=Local(from,previous+correction);var after=Local(from,body+correction);
                if(floor?PortalRules.EntersFloor(N(before),N(after)):PortalRules.Enters(N(before),N(after))){Travel(from,portals[1-i]!,after);break;}
            }
        }
        previous=player.Position+Vector3.up*.9f;havePrevious=true;
    }
    void Equip(bool value) {
        if(equipped==value)return;
        if(value)player?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
        equipped=value;nextShot=Time.unscaledTime+.2f;
        if(!value){gunRoot?.SetActive(false);RestoreHands();}
        Play(value?"equip":"unequip");Tell(value?"PORTAL GUN | LMB blue / RMB orange | Backspace clear":"Portal gun holstered; paired portals remain active");
    }
    static System.Numerics.Vector3 N(Vector3 v)=>new(v.x,v.y,v.z);
    static Vector3 Local(Portal p,Vector3 world)=>Quaternion.Inverse(p.Rotation)*(world-p.Center);
    static Vector3 Through(Portal a,Portal b,Vector3 world)=>b.Center+b.Rotation*Quaternion.Euler(0,180,0)*Local(a,world);
    static Quaternion Turn(Portal a,Portal b)=>b.Rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(a.Rotation);
    static bool Alive(Portal p)=>p.Surface!=null&&p.Surface.enabled&&(p.Surface.transform.position-p.SurfacePosition).sqrMagnitude<.0001f&&Quaternion.Angle(p.Surface.transform.rotation,p.SurfaceRotation)<.1f;
    void Shoot(int index) {
        nextShot=Time.unscaledTime+.28f;lastShot=Time.unscaledTime;Play(index==0?"blue":"orange");
        if(!Physics.Raycast(view!.transform.position,view.transform.forward,out var hit,settings.Range,mask,QueryTriggerInteraction.Ignore)){Reject("No solid surface in range");return;}
        if(hit.collider==null||hit.rigidbody!=null||hit.collider.GetComponentInParent<Player>()!=null||hit.collider.GetComponentInParent<EFT.Interactive.WorldInteractiveObject>()!=null){Reject("Use a fixed wall or floor");return;}
        // Steep slopes and ceilings are intentionally rejected: require an upright safe landing.
        bool floor=hit.normal.y>.96f;
        if(!floor&&Math.Abs(hit.normal.y)>.12f){Reject("Use a flat wall or floor");return;}
        Vector3 normal=hit.normal.normalized;
        Vector3 up=floor?Vector3.ProjectOnPlane(view.transform.forward,normal).normalized:Vector3.up;
        if(up.sqrMagnitude<.5f)up=Vector3.forward;
        Quaternion rotation=Quaternion.LookRotation(normal,up);
        Vector3 center=hit.point;
        for(int i=0;i<12;i++) {
            float a=i*MathF.PI/6;
            Vector3 sample=center+rotation*new Vector3(MathF.Cos(a)*PortalRules.HalfWidth,MathF.Sin(a)*PortalRules.HalfHeight,0);
            if(!Physics.Raycast(sample+normal*.12f,-normal,out var edge,.24f,mask,QueryTriggerInteraction.Ignore)||Vector3.Dot(edge.normal,normal)<.98f||Math.Abs(Vector3.Dot(edge.point-center,normal))>.045f){Reject("Surface too small or uneven");return;}
        }
        var other=portals[1-index];
        if(other!=null&&(other.Center-center).sqrMagnitude<6.25f){Reject("Place the two portals farther apart");return;}
        var proposed=new Portal {Center=center+normal*.025f,Rotation=rotation,Surface=hit.collider,SurfacePosition=hit.collider.transform.position,SurfaceRotation=hit.collider.transform.rotation,Index=index};
        if(!Exit(proposed,Vector3.zero,out _)){Reject("Not enough standing room at this portal");return;}
        var made=BuildPortal(proposed);
        portals[index]?.Dispose();portals[index]=made;
        havePrevious=false;Play(index==0?"open_blue":"open_orange");Tell(index==0?"Blue portal placed":"Orange portal placed");
    }
    void Reject(string text){Play("invalid");Tell(text);}
    void Tell(string text){notice=text;noticeUntil=Time.unscaledTime+4;}
    bool Exit(Portal p,Vector3 offset,out Vector3 feet) {
        feet=default;
        Vector3 normal=p.Rotation*Vector3.forward;
        bool floor=normal.y>.9f;
        Vector3 probe=p.Center+p.Rotation*new Vector3(Math.Clamp(offset.x,-.28f,.28f),Math.Clamp(offset.y,-.7f,.7f),floor?.1f:.62f);
        if(!Physics.Raycast(probe+Vector3.up*(floor?.3f:0),Vector3.down,out var ground,floor?.6f:2.7f,mask,QueryTriggerInteraction.Ignore)||ground.normal.y<.65f)return false;
        feet=ground.point+Vector3.up*.06f;
        if(!floor&&Math.Abs(feet.y+.9f-probe.y)>.65f)return false;
        // Include bodies and doors, exclude only this player's colliders and triggers.
        var collisions=Physics.OverlapCapsule(feet+Vector3.up*.32f,feet+Vector3.up*1.5f,.3f,~(1<<2),QueryTriggerInteraction.Ignore);
        foreach(var c in collisions)if(c!=null&&!c.transform.IsChildOf(player!.transform))return false;
        return true;
    }
    void Travel(Portal from,Portal to,Vector3 offset) {
        cooldown=Time.unscaledTime+.6f;
        if(!Alive(to)||!Exit(to,new Vector3(-offset.x,offset.y,0),out var destination)){Reject("Exit blocked; portal kept for retry");return;}
        Vector3 facing=Turn(from,to)*view!.transform.forward;
        // EFT stays upright; retain transformed yaw/pitch without forcing camera roll.
        float yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;
        float pitch=-Mathf.Asin(Mathf.Clamp(facing.y,-1,1))*Mathf.Rad2Deg;
        Play("enter");
        player!.Teleport(destination,true);
        player.MovementContext.SetRotation(new Vector2(yaw,Math.Clamp(pitch,-85,85)));
        Play("exit");
        context.Log(LogLevel.Info,$"Portal transit {from.Index}->{to.Index}");
    }
    void ClearPortals(){for(int i=0;i<2;i++){portals[i]?.Dispose();portals[i]=null;}havePrevious=false;}
    void ResetRaid(){equipped=false;havePrevious=false;worldId=playerId=IntPtr.Zero;RestoreHands();gunRoot?.SetActive(false);ClearPortals();}
    public void Shutdown(){ResetRaid();}
    public void Draw() {
        if(!valid||!Application.isFocused||Cursor.visible)return;
        if(equipped) {
            var old=GUI.color;
            GUI.color=new Color(.1f,.65f,1);GUI.DrawTexture(new Rect(Screen.width/2-11,Screen.height/2-2,7,4),Texture2D.whiteTexture);
            GUI.color=new Color(1,.42f,.05f);GUI.DrawTexture(new Rect(Screen.width/2+4,Screen.height/2-2,7,4),Texture2D.whiteTexture);GUI.color=old;
            GUI.Box(new Rect(Screen.width/2-260,Screen.height-86,520,28),"PORTAL GUN    LMB: BLUE    RMB: ORANGE    Backspace: CLEAR");
        }
        if(Time.unscaledTime<noticeUntil)GUI.Box(new Rect(Screen.width/2-280,Screen.height-120,560,28),notice);
    }
}
