namespace LocalSpt5Esp;
internal static class CompanionVisualRules {
 internal const string Green="#37E679";
 internal static string Label(string actor,bool teammate)=>teammate?"队友阵营 · "+actor:actor;
}
