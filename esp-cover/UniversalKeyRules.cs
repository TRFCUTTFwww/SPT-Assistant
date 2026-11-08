namespace LocalSpt5Esp;
internal static class UniversalKeyRules {
 internal static bool Allowed(bool card,bool keys,bool cards,int maximum,int used,int stack)=>
  (card?cards:keys)&&stack>0&&(maximum==0||used<maximum);
}
