// The linked hook router logs through the client plugin; this test executable has no client plugin instance.
namespace LocalSpt5Esp;
internal sealed class Plugin
{
    internal static Plugin? Self { get; set; }
    internal TestLog Log { get; } = new();
}
internal sealed class TestLog
{
    internal void LogWarning(string message) => System.Console.Error.WriteLine(message);
}
