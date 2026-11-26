using System;
namespace LocalSpt5Esp;
internal sealed class AiCombatSchedule {
 readonly int size;internal int Source{get;private set;}internal int Target{get;private set;}
 internal AiCombatSchedule(int count){if(count<0)throw new ArgumentOutOfRangeException(nameof(count));size=count;}
 internal bool Done=>Source>=size;
 internal bool SourceComplete=>Target>=size;
 internal int TakeTarget(){if(Done||SourceComplete)throw new InvalidOperationException("交战同步阶段已结束。");return Target++;}
 internal void NextSource(){Source++;Target=0;}
}
