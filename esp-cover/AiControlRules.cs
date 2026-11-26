using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal static class AiControlRules {
 internal static bool SameActor(long expectedWorld,long expectedActor,string expectedProfile,long world,long actor,string profile,bool ai,bool local,bool alive)=>expectedWorld!=0&&expectedActor!=0&&expectedWorld==world&&expectedActor==actor&&expectedProfile==profile&&ai&&!local&&alive;
 internal static float Damage(float amount,double factor,float available){if(!float.IsFinite(amount)||amount<0||!double.IsFinite(factor)||factor<0||!float.IsFinite(available)||available<0)throw new ArgumentException("伤害参数必须是非负有限数字。");return(float)Math.Min((double)amount*factor,available);}
}
internal sealed class AiRelationTable {
 internal readonly Dictionary<(string From,string To),bool> Actors=new();internal readonly Dictionary<(int From,int To),bool> Teams=new();
 internal bool? Resolve(string from,string to,int? fromTeam,int? toTeam){if(Actors.TryGetValue((from,to),out bool personal))return personal;return fromTeam.HasValue&&toTeam.HasValue&&Teams.TryGetValue((fromTeam.Value,toTeam.Value),out bool team)?team:null;}
 internal void ActorPair(string a,string b,bool hostile){if(a==b)throw new ArgumentException("不能对同一个 AI 设置双方关系。");Actors[(a,b)]=Actors[(b,a)]=hostile;}
 internal void TeamPair(int a,int b,bool hostile){if(a==b)throw new ArgumentException("双方属于同一队伍，请选择两个不同队伍。");Teams[(a,b)]=Teams[(b,a)]=hostile;}
 internal void Clear(){Actors.Clear();Teams.Clear();}
}
