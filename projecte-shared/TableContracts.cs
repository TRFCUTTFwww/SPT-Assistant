namespace ProjectE;
public sealed class TableRequest {
 public string Action{get;set;}="state";
 public string Operation{get;set;}="";
 public string Profile{get;set;}="";
 public bool Raid{get;set;}
 public bool Burn{get;set;}
 public string Item{get;set;}="";
 public string Template{get;set;}="";
 public int Count{get;set;}=1;
 public int Quality{get;set;}=10000;
 public string Parent{get;set;}="";
 public string Slot{get;set;}="";
 public int X{get;set;}
 public int Y{get;set;}
 public int Rotation{get;set;}
}
public sealed record TablePrice(string Id,long Value,int Stack);
public sealed class TableReply {
 public bool Ok{get;set;}
 public string Error{get;set;}="";
 public long Balance{get;set;}
 public string[] Learned{get;set;}=[];
 public TablePrice[] Prices{get;set;}=[];
 public string Created{get;set;}="";
 public bool NewKnowledge{get;set;}
}
public static class EmcRules {
 public const string Ruble="5449016a4bdc2d6f028b456f";
 public static long Quote(long price,int count,int quality=10000){
  if(price<=0||count<=0||quality is <0 or >10000)throw new ArgumentException("无效的价格、数量或物品状态。");
  return checked((long)decimal.Floor((decimal)price*count*quality/10000));
 }
 public static long Exchange(long balance,long quote,bool burn){
  if(balance<0||quote<=0)throw new ArgumentException("EMC 数额必须大于零。");
  if(!burn&&quote>balance)throw new ArgumentException("EMC 不足。");
  return burn?checked(balance+quote):balance-quote;
 }
 public static float Progress(double current,double minimum,double maximum)=>maximum>minimum?(float)Math.Clamp((current-minimum)/(maximum-minimum),0,1):1;
}
