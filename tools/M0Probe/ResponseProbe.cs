using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Nexa.Xsr;
using Nexa.Xsr.State;
using NexaSM.Contracts;
using NexaSM.Domain;
using NexaSM.Persistence.Sqlite;
using NexaSM.Services;
using NexaSM.Services.Composition;

internal static class ResponseProbe
{
    public static async Task<int> Run(string database)
    {
        var store = new SqliteOperationStore(database, "latency");
        var instance = InstanceId.New(); store.CreateInstance(instance);
        var scope = new AuthorizedScope("latency", "probe");
        await using var management = new LocalManagement(new OperationService(store), 1);
        var stateBuilder = new XsrStateStoreBuilder();
        var name = XsrSemanticId.Parse("nexasm.latency.state");
        stateBuilder.Cell<int>(name, "Probe");
        var states = stateBuilder.Build(); var state = states.Resolve(name); states.Publish(state, 42);
        var command = new DesiredStateCommand(Guid.NewGuid(), "latency", instance, new(0), DesiredState.Running);
        var payload = JsonSerializer.SerializeToUtf8Bytes(command, ContractJson.Default.DesiredStateCommand);
        // Startup initialization, not a user operation. There is no Sidecar readiness gate.
        _ = CommandCodec.Read(payload); _ = states.Read<int>(state);
        var measurements = new List<ResponseMeasurement>();
        var maximum = 0.0;
        for (var i = 0; i < 10000; i++)
        {
            var start = Stopwatch.GetTimestamp(); var value = states.Read<int>(state);
            maximum = Math.Max(maximum, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            if (value.Value != 42) throw new InvalidOperationException("state mismatch");
        }
        measurements.Add(new("local-state-read", 10000, maximum, 10));
        maximum = 0;
        for (var i = 0; i < 1000; i++)
        {
            var start = Stopwatch.GetTimestamp(); _ = CommandCodec.Read(payload);
            maximum = Math.Max(maximum, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        measurements.Add(new("command-validation", 1000, maximum, 10));
        maximum = 0;
        for (var i = 0; i < 100; i++)
        {
            var intent = command with { IdempotencyKey = "latency-" + i, ExpectedVersion = new(i) };
            var start = Stopwatch.GetTimestamp(); var receipt = await management.SubmitAsync(scope, intent);
            maximum = Math.Max(maximum, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            if (receipt.State != OperationState.Pending) throw new InvalidOperationException("receipt mismatch");
        }
        measurements.Add(new("durable-admission", 100, maximum, 100));

        // Real SQLite write contention, not a synthetic delay inside the adapter.
        using (var blocked = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString()))
        {
            blocked.Open(); using var transaction = blocked.BeginTransaction();
            var delayed = command with { IdempotencyKey = "locked", ExpectedVersion = new(100) };
            var start = Stopwatch.GetTimestamp(); var inFlight = management.SubmitAsync(scope, delayed);
            var busyStart = Stopwatch.GetTimestamp();
            try { await management.SubmitAsync(scope, delayed); throw new InvalidOperationException("budget not enforced"); }
            catch (DomainException error) when (error.Code == "AdmissionBusy") { }
            measurements.Add(new("overload-rejection", 1, Stopwatch.GetElapsedTime(busyStart).TotalMilliseconds, 10));
            try { await inFlight; throw new InvalidOperationException("locked admission returned success"); }
            catch (DomainException error) when (error.Code == "AdmissionOutcomeUnknown") { }
            measurements.Add(new("locked-storage-response", 1, Stopwatch.GetElapsedTime(start).TotalMilliseconds, 100));
            transaction.Rollback();
        }
        var recoveryStart = Stopwatch.GetTimestamp();
        while (store.ReadInstance(instance).Version.Value != 101)
        {
            if (Stopwatch.GetElapsedTime(recoveryStart) > TimeSpan.FromSeconds(5)) throw new TimeoutException("late commit missing");
            await Task.Delay(25);
        }
        var report = new ResponseReport(Environment.OSVersion.ToString(), Environment.ProcessorCount,
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            measurements.ToArray(), measurements.All(x => x.MaximumMilliseconds <= x.BudgetMilliseconds));
        Console.WriteLine(JsonSerializer.Serialize(report, ResponseJson.Default.ResponseReport));
        return report.Passed ? 0 : 1;
    }
}
internal sealed record ResponseMeasurement(string Scenario, int Samples, double MaximumMilliseconds, double BudgetMilliseconds);
internal sealed record ResponseReport(string OperatingSystem, int LogicalProcessors, string Architecture,
    ResponseMeasurement[] Measurements, bool Passed);
[JsonSerializable(typeof(ResponseReport))]
internal partial class ResponseJson : JsonSerializerContext;
