using System;
namespace LocalSpt5Esp;
internal static class AiPmcProfileRules {
 // This SPT 5 backend supplies PMC bot profiles with Savage as the transport side.
 // Native selection must match that side before the exclusively owned profile is normalized.
 internal static string LoadingSide(string role,string side)=>IsPmc(role,side)?"Savage":side;
 internal static bool IsPmc(string role,string side)=>(role=="pmcUSEC"&&side=="Usec")||(role=="pmcBEAR"&&side=="Bear");
 internal static bool Accepts(string requestedRole,string requestedSide,string actualRole,string actualSide)=>
  IsPmc(requestedRole,requestedSide)&&actualRole==requestedRole&&(actualSide==requestedSide||actualSide=="Savage");
}
