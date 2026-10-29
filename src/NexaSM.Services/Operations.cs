using NexaSM.Contracts;
using NexaSM.Domain;
namespace NexaSM.Services;

public sealed record AuthorizedScope(string ControlDomain, string Principal);
// Host must authorize the resource/action before constructing this scope.
// No execution or XSR routing is implemented in M0.
public interface IOperationStore
{
    void CreateInstance(InstanceId id);
    InstanceSpec ReadInstance(InstanceId id);
    OperationReceipt Accept(AuthorizedScope scope, DesiredStateCommand command);
    OperationReceipt? Find(OperationId id);
    IReadOnlyList<OutboxItem> ReadPending(int limit);
}
public sealed class OperationService(IOperationStore store)
{
    public OperationReceipt Submit(AuthorizedScope scope, DesiredStateCommand command)
    {
        if (string.IsNullOrWhiteSpace(scope.ControlDomain) || string.IsNullOrWhiteSpace(scope.Principal))
            throw new DomainException("InvalidScope");
        CommandCodec.Validate(command);
        return store.Accept(scope, command);
    }
}
