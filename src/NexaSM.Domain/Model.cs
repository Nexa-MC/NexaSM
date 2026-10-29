namespace NexaSM.Domain;

public readonly record struct InstanceId(Guid Value)
{
    public static InstanceId New() => new(Guid.NewGuid());
    public void Validate() { if (Value == Guid.Empty) throw new ArgumentException("Empty instance identity."); }
}
public readonly record struct RunId(Guid Value);
public readonly record struct OperationId(Guid Value);
public readonly record struct ResourceVersion(long Value);
public readonly record struct SpecGeneration(long Value);
public readonly record struct StateRevision(long Value);
public readonly record struct OwnerEpoch(long Value);
public enum DesiredState { Stopped, Running }
public enum RunPhase { Pending, Preparing, Starting, Running, Stopping, Exited, Failed }
public enum Health { Unknown, Healthy, Degraded, Unhealthy }
public enum Availability { Available, Stale, Unavailable }
public enum ControlState { Attached, ControlChannelLost, UnmanagedObserved }
public enum Admission { Closed, Open, Draining }
public enum OperationState { Pending, Running, WaitingForCapacity, WaitingForNode, Paused, Succeeded, Failed, Canceled, NeedsAttention }

public sealed record InstanceSpec(InstanceId Id, DesiredState DesiredState, ResourceVersion Version, SpecGeneration Generation);
public sealed record RunObservation(RunId Id, RunPhase Phase, Health Health, Availability Availability,
    ControlState Control, OwnerEpoch OwnerEpoch, StateRevision Revision, DateTimeOffset ObservedAt);

public sealed class DomainException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class OperationTransitions
{
    public static OperationState Move(OperationState current, OperationState next)
    {
        var allowed = current switch
        {
            OperationState.Pending => next is OperationState.Running or OperationState.Canceled or OperationState.Failed,
            OperationState.Running => next is OperationState.Succeeded or OperationState.Failed or OperationState.Canceled
                or OperationState.NeedsAttention or OperationState.WaitingForCapacity or OperationState.WaitingForNode or OperationState.Paused,
            OperationState.WaitingForCapacity or OperationState.WaitingForNode or OperationState.Paused =>
                next is OperationState.Running or OperationState.Canceled or OperationState.Failed or OperationState.NeedsAttention,
            _ => false
        };
        return allowed ? next : throw new DomainException("InvalidOperationTransition");
    }
}
