using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
namespace LocalSpt5Esp;
internal static class BossLabels {
 sealed record Identity(string Prefix,string Role,string Name);
 static ConfigEntry<BossNameLanguage> language=null!;
 static readonly Dictionary<string,Identity> identities=new();
 internal static void Initialize(){language=FeatureConfig.Bind("1. ESP","Boss name language","BOSS 名称语言",BossNameLanguage.Russian,"在英文和俄文之间切换 BOSS 透视名称；没有对应译名的呼号保留原名。");}
 internal static string Label(Player player){var identity=new Identity(BodyTint.KindName(player),player.Profile?.Info?.Settings?.Role.ToString()??"",player.Profile?.Nickname??player.ProfileId);identities[player.ProfileId]=identity;return Format(identity);}
 static string Format(Identity identity)=>identity.Prefix+" · "+BossNameRules.Resolve(identity.Role,identity.Name,language.Value);
 internal static string Cached(string id,string fallback)=>identities.TryGetValue(id,out var identity)?Format(identity):fallback;
 internal static void Reset()=>identities.Clear();
}
