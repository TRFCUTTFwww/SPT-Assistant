using System;
using System.IO;
using System.Threading.Tasks;

namespace LocalSpt5Esp.HotReload;

public sealed partial class RuntimeReloader
{
    public DiagnosticService Diagnostics { get; private set; } = null!;
    public bool SafetyBlocked => pause != null && failure.Length > 0;
    public string DiagnosticExportStatus => Diagnostics.ExportStatus;

    Task<Prepared>? rollbackPreparing;
    byte[]? previousGood;
    string swapStage = "idle";
    bool cleanupUnsafe;

    public void SetPerformanceSnapshot(string json) => Diagnostics.Performance(json);

    public string[] DiagnosticSettings() =>
        [Diagnostics.Minimum.Value, Diagnostics.IncludeHardware.Value.ToString(), Diagnostics.IncludePerformance.Value.ToString(),
         Diagnostics.IncludeSpt.Value.ToString(), Diagnostics.IncludeGame.Value.ToString(), Diagnostics.DetailedTiming.Value.ToString(),
         bool.FalseString, Diagnostics.PhysicsTracing.Value.ToString()];

    public void SetDiagnosticOption(string key, string value)
    {
        switch (key)
        {
            case "level":
                if (Array.IndexOf(AssistantDiagnostics.LogBundle.Levels, value) < 0)
                    throw new ArgumentException("日志等级无效");
                Diagnostics.Minimum.Value = value;
                break;
            case "hardware": Diagnostics.IncludeHardware.Value = bool.Parse(value); break;
            case "performance": Diagnostics.IncludePerformance.Value = bool.Parse(value); break;
            case "spt": Diagnostics.IncludeSpt.Value = bool.Parse(value); break;
            case "game": Diagnostics.IncludeGame.Value = bool.Parse(value); break;
            case "timing": Diagnostics.DetailedTiming.Value = bool.Parse(value); break;
            case "physics": Diagnostics.PhysicsTracing.Value = bool.Parse(value); break;
            default: throw new ArgumentException("未知诊断设置");
        }
    }

    public void ExportLogs() => Diagnostics.BeginExport();

    void StartDiagnostics()
    {
        Diagnostics = new(Config, PluginDirectory);
        if (File.Exists(fallback))
            try { lastGood = ReadPayload(fallback); }
            catch (Exception error) { Diagnostics.Record("WARNING", "Reload", error.Message); }
        string previous = Path.Combine(PluginDirectory, "runtime", "previous-good.module");
        if (File.Exists(previous))
            try { previousGood = ReadPayload(previous); }
            catch (Exception error) { Diagnostics.Record("WARNING", "Reload", error.Message); }
        AppDomain.CurrentDomain.UnhandledException += Unhandled;
        TaskScheduler.UnobservedTaskException += Unobserved;
    }

    void Unhandled(object sender, UnhandledExceptionEventArgs args) =>
        Diagnostics.Fault("未处理异常", args.ExceptionObject?.ToString() ?? "未知运行时错误");

    void Unobserved(object? sender, UnobservedTaskExceptionEventArgs args) =>
        Diagnostics.Record("ERROR", "Task", args.Exception.ToString());

    void StopDiagnostics()
    {
        AppDomain.CurrentDomain.UnhandledException -= Unhandled;
        TaskScheduler.UnobservedTaskException -= Unobserved;
        Diagnostics.Dispose();
    }

    void PollAfterReloadFailure()
    {
        if (rollbackPreparing != null) { ContinueRollback(); return; }
        if (observing != null)
        {
            if (!observing.IsCompleted) return;
            var done = observing;
            observing = null;
            try
            {
                var observation = done.GetAwaiter().GetResult();
                observedStamp = observation.Stamp;
                observedLength = observation.Length;
                seenRequest = observation.Request;
                if (observation.Prepared == null) return;
                if (cleanupUnsafe)
                {
                    phase = "旧模块清理失败，已阻止再次切换；请导出日志并重启。";
                    observation.Prepared.Context.Release();
                    return;
                }
                Begin(observation.Prepared);
            }
            catch (Exception error) { Diagnostics.Record("ERROR", "Reload preflight", error.ToString()); }
            return;
        }
        if (DateTime.UtcNow < nextPoll) return;
        nextPoll = DateTime.UtcNow.AddSeconds(1);
        try
        {
            var file = new FileInfo(payload);
            var requestTime = File.Exists(request) ? File.GetLastWriteTimeUtc(request) : DateTime.MinValue;
            if (file.LastWriteTimeUtc == observedStamp && file.Length == observedLength && requestTime == seenRequest && !manual) return;
            manual = false;
            var stamp = observedStamp;
            var length = observedLength;
            var requestStamp = seenRequest;
            observedStamp = file.LastWriteTimeUtc;
            observedLength = file.Length;
            seenRequest = requestTime;
            observing = Task.Run(() => Observe(true, stamp, length, requestStamp, loadedHash));
        }
        catch (Exception error) { Diagnostics.Record("ERROR", "Reload watcher", error.Message); }
    }

    void ReloadFailure(Exception error)
    {
        Report(error);
        sequence?.Dispose();
        sequence = null;
        persistence = null;
        cleanupUnsafe |= swapStage == "unloading";
        failure = "热更新失败：" + error.GetBaseException().Message;
        phase = cleanupUnsafe ? "模块清理未完成，请重启游戏。" : "可以回退上一版本，或加载修复后的模块。";
        pause ??= new ReloadPause();
        Config.SaveOnConfigSet = autoSave;
        Diagnostics.Fault(failure, error.ToString());
        nextPoll = DateTime.UtcNow.AddSeconds(1);
        try
        {
            observedStamp = File.GetLastWriteTimeUtc(payload);
            observedLength = new FileInfo(payload).Length;
            seenRequest = File.Exists(request) ? File.GetLastWriteTimeUtc(request) : DateTime.MinValue;
        }
        catch (IOException) { }
        var state = StatusBytes(failure);
        _ = Task.Run(() => AtomicWrite(status, state));
    }

    public void RollbackPreviousVersion() => RequestRollback();

    void RequestRollback()
    {
        if (cleanupUnsafe || rollbackPreparing != null || sequence != null || persistence != null) return;
        var bytes = candidate != null || swapStage != "idle" ? lastGood : previousGood ?? lastGood;
        if (bytes == null) { phase = "没有可回退的版本。"; return; }
        phase = "正在后台校验回退版本…";
        rollbackPreparing = Task.Run(() => Prepare(bytes));
    }

    void ContinueRollback()
    {
        if (rollbackPreparing?.IsCompleted != true) return;
        var pendingRollback = rollbackPreparing;
        rollbackPreparing = null;
        try
        {
            var prepared = pendingRollback.GetAwaiter().GetResult();
            rollingBack = true;
            candidate = prepared;
            painted = stepCount = 0;
            peakMs = 0;
            phase = "正在清理当前模块并回退…";
            sequence = new ReloadSequence(Swap(prepared));
        }
        catch (Exception error) { ReloadFailure(error); }
    }
}
