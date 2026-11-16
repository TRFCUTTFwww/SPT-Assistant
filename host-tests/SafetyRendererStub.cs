using System;
using AssistantSafetyUi;
namespace AssistantSafetyUi;
internal static class SafetyScreen {
 internal static void Draw(SafetyPresentation view,UnityEngine.Color color,Action<SafetyAction> selected){if(TestState.Click is "结束调试模拟并继续" or "结束测试，返回游戏"){TestState.Click="";selected(SafetyAction.Continue);}}
}
