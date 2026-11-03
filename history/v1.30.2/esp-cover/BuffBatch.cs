using System;
using System.Collections.Generic;
using System.Linq;
namespace LocalSpt5Esp;
internal sealed record BuffBatchResult(int Succeeded,string[] Failures) {
 internal int Total=>Succeeded+Failures.Length;
 internal string Summary(string action)=>action+"：成功 "+Succeeded+"／"+Total+"，失败／跳过 "+Failures.Length+"。";
}
internal static class BuffBatch {
 internal static BuffBatchResult Run<T>(IEnumerable<T> selected,Func<T,string> name,Action<T> apply){var targets=selected.ToArray();if(targets.Length==0)throw new ArgumentException("请先选择 AI。");int succeeded=0;var failures=new List<string>();foreach(var target in targets)try{apply(target);succeeded++;}catch(Exception e){failures.Add(name(target)+"："+e.Message);}return new(succeeded,failures.ToArray());}
}
