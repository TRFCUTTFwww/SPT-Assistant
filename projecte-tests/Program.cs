using ProjectE;
int checks=0;
void Check(bool valid,string label){if(!valid)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
void Reject(Action action,string label){try{action();}catch(ArgumentException){Check(true,label);return;}catch(OverflowException){Check(true,label);return;}throw new Exception("Accepted invalid operation: "+label);}
Check(Swords.All.Select(x=>x.Damage).SequenceEqual(new[]{13,17,24}),"original uncharged ProjectE weapon damage");
Check(Swords.All.Select(x=>x.Id).Distinct().Count()==3&&Swords.All.All(x=>x.Id.Length==24),"three independent stable template IDs");
Check(EmcRules.Quote(1,500000)==500000,"one ruble equals one EMC");
Check(EmcRules.Quote(14000,3)==42000,"stack value uses unit handbook rubles");
Check(EmcRules.Quote(14000,3,2500)==10500,"used resource value scales by remaining quality");
Check(EmcRules.Quote(3,1,5000)==1,"fractional EMC rounds down");
Check(EmcRules.Exchange(0,500,true)==500,"burn credits exact EMC");
Check(EmcRules.Exchange(500,500,false)==0,"exact balance can be withdrawn");
Check(EmcRules.Exchange(500,200,false)==300,"withdrawal subtracts exact amount");
Reject(()=>EmcRules.Exchange(499,500,false),"insufficient balance rejected");
Reject(()=>EmcRules.Exchange(-1,1,true),"negative wallet rejected");
Reject(()=>EmcRules.Exchange(1,0,true),"zero value consumption rejected");
Reject(()=>EmcRules.Exchange(long.MaxValue,1,true),"wallet overflow rejected");
Reject(()=>EmcRules.Quote(long.MaxValue,2),"quote overflow rejected");
Reject(()=>EmcRules.Quote(1,0),"zero count rejected");
Reject(()=>EmcRules.Quote(1,-1),"negative count rejected");
Reject(()=>EmcRules.Quote(0,1),"unknown price rejected");
Reject(()=>EmcRules.Quote(100,1,10001),"quality above full rejected");
Reject(()=>EmcRules.Quote(100,1,-1),"negative quality rejected");
Check(EmcRules.Progress(1000,1000,4017)==0,"level starts at zero progress");
Check(Math.Abs(EmcRules.Progress(2508.5,1000,4017)-.5)<1e-6,"level progress follows EFT thresholds");
Check(EmcRules.Progress(9000,1000,4017)==1&&EmcRules.Progress(0,1000,4017)==0,"experience progress clamps to level bounds");
Console.WriteLine($"Passed {checks} ProjectE economy, item and experience checks.");
