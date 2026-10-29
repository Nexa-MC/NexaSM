using System.Text.Json;
using System.Text.Json.Serialization;
using NexaSM.Domain;
namespace NexaSM.Contracts;

// Actor is supplied separately by a trusted host, never accepted from the wire.
public sealed record DesiredStateCommand([property: JsonRequired] Guid CommandId,
    [property: JsonRequired] string IdempotencyKey, [property: JsonRequired] InstanceId Target,
    [property: JsonRequired] ResourceVersion ExpectedVersion, [property: JsonRequired] DesiredState DesiredState,
    [property: JsonRequired] int SchemaVersion = 1);
public sealed record OperationReceipt(OperationId OperationId, OperationState State);
// Future Node Inbox uses this durable execution identity, not transport sequence numbers.
public sealed record ExecutionStepKey(OperationId OperationId, string StepId, OwnerEpoch OwnerEpoch);
public enum StepOutcome { Pending, Completed, Failed, OutcomeUnknown }
public sealed record ExecutionStepReceipt(ExecutionStepKey Key, StepOutcome Outcome);
public sealed record OutboxItem(OperationId OperationId, InstanceId Target, DesiredState DesiredState);

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(DesiredStateCommand))]
[JsonSerializable(typeof(OperationReceipt))]
[JsonSerializable(typeof(RunObservation))]
[JsonSerializable(typeof(ExecutionStepReceipt))]
public partial class ContractJson : JsonSerializerContext;

public static class CommandCodec
{
    public const int MaximumBytes = 256 * 1024;
    public static DesiredStateCommand Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new DomainException("PayloadTooLarge");
        try
        {
            // A default struct value is not evidence that the caller supplied a CAS token.
            using var document = JsonDocument.Parse(bytes.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ExpectedVersion", out var version) ||
                version.ValueKind != JsonValueKind.Object ||
                !version.TryGetProperty("Value", out var value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
                throw new DomainException("InvalidCommand");
            var command = JsonSerializer.Deserialize(bytes, ContractJson.Default.DesiredStateCommand)
                ?? throw new DomainException("InvalidCommand");
            Validate(command);
            return command;
        }
        catch (JsonException) { throw new DomainException("InvalidCommand"); }
    }
    public static void Validate(DesiredStateCommand command)
    {
        if (command.SchemaVersion != 1) throw new DomainException("UnsupportedSchema");
        if (command.CommandId == Guid.Empty || command.Target.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128 ||
            command.ExpectedVersion.Value < 0 || !Enum.IsDefined(command.DesiredState))
            throw new DomainException("InvalidCommand");
    }
}
