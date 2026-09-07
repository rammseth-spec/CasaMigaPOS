using System.Text.Json;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;
[TestClass]
public sealed class FailureDiagnosticsTests
{
    [TestMethod]
    [DataRow(FailureOrigin.Dispatcher)] [DataRow(FailureOrigin.AppDomain)]
    [DataRow(FailureOrigin.UnobservedTask)] [DataRow(FailureOrigin.Startup)]
    public void UnknownFailuresTerminateAndDoNotSerializeSecrets(FailureOrigin origin)
    {
        var directory = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N"));
        try
        {
            Exception error;
            try { throw new InvalidOperationException("SECRET password path", new Exception("INNER SECRET")); }
            catch (Exception e) { error = e; }
            error.Data["credential"] = "SECRET";
            var report = new UnhandledFailureDiagnostics(directory).TryRecord(origin, error)!;
            Assert.IsTrue(report.Terminate); Assert.AreEqual(origin, report.Origin);
            Assert.AreEqual(typeof(InvalidOperationException).FullName, report.ExceptionType);
            Assert.IsNotEmpty(report.Frames);
            var json = File.ReadAllText(Directory.GetFiles(directory).Single());
            Assert.IsFalse(json.Contains("SECRET", StringComparison.Ordinal));
            Assert.IsFalse(json.Contains("credential", StringComparison.Ordinal));
            Assert.AreEqual(report.IncidentId, JsonSerializer.Deserialize<FailureReport>(json)!.IncidentId);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [TestMethod] public void FailedSinkCannotPreventTerminationPolicy()
    {
        var file = Path.GetTempFileName();
        try
        {
            var report = new UnhandledFailureDiagnostics(file).TryRecord(FailureOrigin.Dispatcher, new Exception());
            Assert.IsNotNull(report); Assert.IsTrue(report.Terminate);
        }
        finally { File.Delete(file); }
    }
}
