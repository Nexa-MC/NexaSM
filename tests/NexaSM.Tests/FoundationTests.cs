using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaSM.Domain;
using NexaSM.Contracts;
using NexaSM.Services;
using NexaSM.Persistence.Sqlite;
using Xunit;
namespace NexaSM.Tests;

public sealed class FoundationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "nexasm-tests-" + Guid.NewGuid());
    private readonly SqliteOperationStore store;
    private readonly InstanceId instance = InstanceId.New();
    private readonly AuthorizedScope actor = new("test-domain", "alice");
    private string Database => Path.Combine(directory, "control.db");
    public FoundationTests() { Directory.CreateDirectory(directory); store = new(Database, actor.ControlDomain); store.CreateInstance(instance); }
    private DesiredStateCommand Start() => new(Guid.NewGuid(), "start", instance, new(0), DesiredState.Running);
    [Fact] public async Task ConcurrentRetriesReturnOneDurableOperation()
    {
        var command = Start(); var service = new OperationService(store);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => service.Submit(actor, command))));
        Assert.Single(receipts.Distinct()); Assert.Single(store.ReadPending(500));
        Assert.Equal(1, store.ReadInstance(instance).Version.Value);
        Assert.Equal(OperationState.Pending, receipts[0].State);
    }
    [Fact] public void SameKeyDifferentPayloadDoesNotMutateOriginal()
    {
        var c = Start(); var service = new OperationService(store); var receipt = service.Submit(actor, c);
        Assert.Equal("IdempotencyConflict", Assert.Throws<DomainException>(() => service.Submit(actor, c with { DesiredState = DesiredState.Stopped })).Code);
        Assert.Equal(DesiredState.Running, store.ReadInstance(instance).DesiredState);
        Assert.Equal(receipt, store.Find(receipt.OperationId)); Assert.Single(store.ReadPending(10));
    }
    [Fact] public void ReopenAndLostResponseReturnOriginalReceipt()
    {
        var c = Start(); var receipt = new OperationService(store).Submit(actor, c);
        var reopened = new SqliteOperationStore(Database, actor.ControlDomain);
        Assert.Equal(receipt, new OperationService(reopened).Submit(actor, c with { CommandId = Guid.NewGuid() }));
        Assert.Equal(receipt, reopened.Find(receipt.OperationId));
    }
    [Fact] public void StaleCasRollsBackAllEffects()
    {
        var service = new OperationService(store); service.Submit(actor, Start());
        Assert.Throws<DomainException>(() => service.Submit(actor, Start() with { IdempotencyKey = "another" }));
        Assert.Single(store.ReadPending(10)); Assert.Equal(1, store.ReadInstance(instance).Version.Value);
    }
    [Fact] public void StopIntentPersistsAndRetryDoesNotRevertIt()
    {
        var service = new OperationService(store); var start = Start(); var first = service.Submit(actor, start);
        service.Submit(actor, start with { IdempotencyKey = "stop", ExpectedVersion = new(1), DesiredState = DesiredState.Stopped });
        Assert.Equal(first, service.Submit(actor, start));
        Assert.Equal(DesiredState.Stopped, store.ReadInstance(instance).DesiredState);
        Assert.Equal(2, store.ReadInstance(instance).Generation.Value);
    }
    [Fact] public void NoOpSpecDoesNotIncrementGeneration()
    {
        new OperationService(store).Submit(actor, Start() with { DesiredState = DesiredState.Stopped });
        Assert.Equal(0, store.ReadInstance(instance).Generation.Value);
        Assert.Equal(1, store.ReadInstance(instance).Version.Value);
    }
    [Fact] public void PrincipalHasIndependentIdempotencyScope()
    {
        var service = new OperationService(store); service.Submit(actor, Start());
        service.Submit(actor with { Principal = "bob" }, Start() with { ExpectedVersion = new(1) });
        Assert.Equal(2, store.ReadPending(10).Count);
    }
    [Fact] public void DatabaseCannotChangeControlDomain()
    { Assert.Throws<DomainException>(() => new SqliteOperationStore(Database, "other")); }
    [Fact] public void WrongDomainIsRejected()
    { Assert.Throws<DomainException>(() => new OperationService(store).Submit(actor with { ControlDomain = "other" }, Start())); Assert.Empty(store.ReadPending(10)); }
    [Fact] public void FailedOutboxWriteRollsBackIntentAndOperation()
    {
        using var db = new SqliteConnection($"Data Source={Database};Pooling=False"); db.Open();
        using var cmd = db.CreateCommand(); cmd.CommandText = "CREATE TRIGGER fail_outbox BEFORE INSERT ON outbox BEGIN SELECT RAISE(ABORT,'injected failure'); END"; cmd.ExecuteNonQuery();
        Assert.Throws<SqliteException>(() => new OperationService(store).Submit(actor, Start()));
        Assert.Equal(DesiredState.Stopped, store.ReadInstance(instance).DesiredState);
        Assert.Equal(0, store.ReadInstance(instance).Version.Value); Assert.Empty(store.ReadPending(10));
        cmd.CommandText = "SELECT COUNT(*) FROM operations"; Assert.Equal(0L, cmd.ExecuteScalar());
    }
    [Fact] public void GeneratedSerializationRoundTripsTypedIdentity()
    {
        var command = Start(); var bytes = JsonSerializer.SerializeToUtf8Bytes(command, ContractJson.Default.DesiredStateCommand);
        Assert.Equal(command, CommandCodec.Read(bytes));
    }
    [Theory] [InlineData("{}")] [InlineData("null")]
    public void MissingFieldsRejected(string json) { Assert.Throws<DomainException>(() => CommandCodec.Read(Encoding.UTF8.GetBytes(json))); }
    [Fact] public void UnsupportedSchemaAndOversizedPayloadRejected()
    {
        Assert.Throws<DomainException>(() => CommandCodec.Validate(Start() with { SchemaVersion = 2 }));
        Assert.Throws<DomainException>(() => CommandCodec.Read(new byte[CommandCodec.MaximumBytes + 1]));
    }
    [Fact] public void AdditiveUnknownFieldAccepted()
    {
        var json = JsonSerializer.Serialize(Start(), ContractJson.Default.DesiredStateCommand);
        Assert.Equal(instance, CommandCodec.Read(Encoding.UTF8.GetBytes(json[..^1] + ",\"future\":true}")).Target);
    }
    [Fact] public void StaleObservationDoesNotInventProcessExit()
    {
        var observation = new RunObservation(new(Guid.NewGuid()), RunPhase.Running, Health.Unknown,
            Availability.Available, ControlState.Attached, new(1), new(1), DateTimeOffset.UtcNow);
        var stale = observation with { Availability = Availability.Stale };
        Assert.Equal(RunPhase.Running, stale.Phase); Assert.Equal(Health.Unknown, stale.Health);
    }
    [Fact] public void OutboxReadsAreBounded() { Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadPending(501)); }
    [Fact] public void MissingExpectedVersionRejected()
    {
        var command = JsonSerializer.Serialize(Start(), ContractJson.Default.DesiredStateCommand);
        var omitted = command.Replace("\"ExpectedVersion\":{\"Value\":0},", "");
        Assert.NotEqual(command, omitted);
        Assert.Throws<DomainException>(() => CommandCodec.Read(Encoding.UTF8.GetBytes(omitted)));
    }
    [Theory] [InlineData("{}")] [InlineData("null")] [InlineData("{\"Value\":null}")]
    [InlineData("{\"Value\":\"0\"}")]
    public void MissingOrMalformedCasTokenRejected(string version)
    {
        var json = JsonSerializer.Serialize(Start(), ContractJson.Default.DesiredStateCommand);
        json = json.Replace("\"ExpectedVersion\":{\"Value\":0}", "\"ExpectedVersion\":" + version);
        Assert.Throws<DomainException>(() => CommandCodec.Read(Encoding.UTF8.GetBytes(json)));
    }
    [Fact] public void DocumentedExamplesExecuteThroughRealConsumer()
    {
        var start = CommandCodec.Read(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "examples", "desired-state-start.json")));
        var stop = CommandCodec.Read(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "examples", "desired-state-stop.json")));
        store.CreateInstance(start.Target);
        var service = new OperationService(store);
        var receipt = service.Submit(actor, start);
        service.Submit(actor, stop);
        Assert.Equal(receipt, service.Submit(actor, start));
        Assert.Equal(DesiredState.Stopped, store.ReadInstance(start.Target).DesiredState);
        Assert.Equal(2, store.ReadPending(10).Count);
    }
    [Fact] public void AcceptedDoesNotMeanCompleted()
    {
        Assert.Throws<DomainException>(() => OperationTransitions.Move(OperationState.Pending, OperationState.Succeeded));
        Assert.Equal(OperationState.Succeeded, OperationTransitions.Move(OperationState.Running, OperationState.Succeeded));
    }
    [Theory] [InlineData(OperationState.Succeeded)] [InlineData(OperationState.Failed)]
    [InlineData(OperationState.Canceled)] [InlineData(OperationState.NeedsAttention)]
    public void TerminalOperationCannotBeSilentlyReplayed(OperationState state)
    { Assert.Throws<DomainException>(() => OperationTransitions.Move(state, OperationState.Running)); }
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
