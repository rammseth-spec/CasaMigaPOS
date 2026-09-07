using System.Diagnostics;
using System.Text.Json;
namespace CasaMiga.Infrastructure;
public enum FailureOrigin { Dispatcher, AppDomain, UnobservedTask, Startup }
public sealed record FailureReport(string IncidentId, DateTimeOffset Timestamp, FailureOrigin Origin,
    bool Terminate, string ExceptionType, int HResult, string[] Frames, string[] InnerTypes);

/// <summary>Last-resort diagnostics: no messages, Data, file paths or arguments.</summary>
public sealed class UnhandledFailureDiagnostics(string directory)
{
    public static FailureReport Describe(FailureOrigin origin, Exception? error)
    {
        var frames = error is null ? [] : new StackTrace(error, false).GetFrames().Take(16)
            .Select(f => f.GetMethod()).Where(m => m is not null)
            .Select(m => $"{m!.DeclaringType?.FullName}.{m.Name}").ToArray();
        var inner = new List<string>();
        for (var e = error?.InnerException; e is not null && inner.Count < 4; e = e.InnerException)
            inner.Add(e.GetType().FullName ?? "Exception");
        return new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, origin, true,
            error?.GetType().FullName ?? "Unknown", error?.HResult ?? 0, frames, inner.ToArray());
    }
    // Unique files avoid locks shared with business logging or another failing thread.
    public FailureReport? TryRecord(FailureOrigin origin, Exception? error)
    {
        FailureReport? report = null;
        try
        {
            report = Describe(origin, error);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"failure-{report.IncidentId}.json"), JsonSerializer.Serialize(report));
        }
        catch (Exception) { /* Logging failure must not suppress termination. */ }
        return report;
    }
}
