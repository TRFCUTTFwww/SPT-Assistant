using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
// Read native group membership only. This helper never joins, merges or rewrites groups.
internal static class NativeAiGroups {
 internal static BotsGroup? Group(IPlayer? player)=>player?.AIData?.BotOwner?.BotsGroup??player?.TryCast<Player>()?.BotsGroup;
 internal static bool Same(BotsGroup? from,IPlayer? target)=>from!=null&&Group(target)?.Pointer==from.Pointer;
 internal static bool HasPlayer(BotsGroup group)=>Singleton<GameWorld>.Instance?.MainPlayer is {} local&&Group(local)?.Pointer==group.Pointer;
 internal static bool ContainsOwner(BotsGroup group,BotOwner owner){var list=group._members;if(list==null)return false;for(int i=0;i<list.Count;i++)if(list[i]?.Pointer==owner.Pointer)return true;return false;}
}
