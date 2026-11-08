using System;
namespace LocalSpt5Esp;
internal static class HomeOverviewRules {
 internal static int Currency(string template)=>template switch{"5449016a4bdc2d6f028b456f"=>0,"5696686a4bdc2da3298b456a"=>1,"569668774bdc2da2298b4568"=>2,_=>-1};
 internal static double? Survival(long sessions,long survived)=>sessions<=0||survived<0||survived>sessions?null:survived/(double)sessions;
 internal static int Columns(float width,float minimum,int maximum)=>Math.Clamp((int)((Math.Max(1,width)+12)/(minimum+12)),1,maximum);
 internal static bool SameOwner(string expected,long pointer,string actual,long current)=>expected.Length>0&&expected==actual&&pointer!=0&&pointer==current;
}
