using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal sealed class ImpactGate {
 readonly HashSet<(string Weapon,int Fire)> seen=new();readonly Queue<(string Weapon,int Fire)> order=new();long world;double next;
 internal void Reset(){seen.Clear();order.Clear();world=0;next=0;}
 internal bool Accept(long raid,string weapon,int fire,double now,double cooldown){
  if(raid==0||string.IsNullOrEmpty(weapon)||!double.IsFinite(now)||!double.IsFinite(cooldown)||cooldown<0)return false;
  if(world!=raid){Reset();world=raid;}
  var key=(weapon,fire);if(!seen.Add(key))return false;order.Enqueue(key);while(order.Count>2048)seen.Remove(order.Dequeue());
  if(now<next)return false;next=now+cooldown;return true;
 }
}
