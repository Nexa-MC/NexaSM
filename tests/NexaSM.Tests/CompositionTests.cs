using System.Reflection;
using NexaSM.Contracts;
using NexaSM.Domain;
using NexaSM.Services;
using NexaSM.Services.Composition;
using Xunit;

namespace NexaSM.Tests;

public sealed class CompositionTests
{
    private static DesiredStateCommand Command() => new(Guid.NewGuid(), "composition", InstanceId.New(), new(0), DesiredState.Running);
    private static readonly AuthorizedScope Scope = new("domain", "actor");

    [Fact] public async Task DoesNotReturnReceiptUntilPersistenceCompletesAndLimitsConcurrency()
    {
        using var store = new BlockingStore();
        await using var management = new LocalManagement(new OperationService(store), 1);
        var pending = management.SubmitAsync(Scope, Command());
        try
        {
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            var error = await Assert.ThrowsAsync<DomainException>(() => management.SubmitAsync(Scope, Command()));
            Assert.Equal("AdmissionBusy", error.Code);
        }
        finally { store.Release.Set(); }
        Assert.Equal(store.Receipt, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact] public async Task CallerCancellationDoesNotCancelAcceptedWork()
    {
        using var store = new BlockingStore();
        await using var management = new LocalManagement(new OperationService(store));
        using var cancel = new CancellationTokenSource();
        var pending = management.SubmitAsync(Scope, Command(), cancel.Token);
        try
        {
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally { store.Release.Set(); }
        await store.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(store.Committed);
    }

    [Fact] public async Task PersistenceFailureNeverBecomesSuccess()
    {
        using var store = new BlockingStore { Fail = true }; store.Release.Set();
        await using var management = new LocalManagement(new OperationService(store));
        var failure = await Assert.ThrowsAsync<DomainException>(() => management.SubmitAsync(Scope, Command()));
        Assert.Equal("AdmissionFailed", failure.Code);
    }

    [Fact] public async Task SlowPersistenceReturnsUnknownInsteadOfFalseSuccessAndKeepsCapacityBounded()
    {
        using var store = new BlockingStore();
        await using var management = new LocalManagement(new OperationService(store), 1);
        try
        {
            var timeout = await Assert.ThrowsAsync<DomainException>(() => management.SubmitAsync(Scope, Command()));
            Assert.Equal("AdmissionOutcomeUnknown", timeout.Code);
            Assert.False(store.Committed);
            var busy = await Assert.ThrowsAsync<DomainException>(() => management.SubmitAsync(Scope, Command()));
            Assert.Equal("AdmissionBusy", busy.Code);
        }
        finally { store.Release.Set(); }
        await store.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact] public async Task PreCanceledCallDoesNotEnterStore()
    {
        using var store = new BlockingStore();
        await using var management = new LocalManagement(new OperationService(store));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => management.SubmitAsync(Scope, Command(), new CancellationToken(true)));
        Assert.False(store.Entered.Task.IsCompleted);
    }

    [Fact] public void DefaultCompositionDoesNotEnableHostPatchingOrReferenceSidecarDirectly()
    {
        var assembly = typeof(LocalManagement).Assembly;
        Assert.DoesNotContain(assembly.GetCustomAttributes<AssemblyMetadataAttribute>(), a => a.Key == "Nexa.AutomaticFunctionPatches");
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("Nexa.Sidecar", StringComparison.Ordinal));
    }

    private sealed class BlockingStore : IOperationStore, IDisposable
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public OperationReceipt Receipt { get; } = new(new(Guid.NewGuid()), OperationState.Pending);
        public bool Committed { get; private set; }
        public bool Fail { get; init; }
        public OperationReceipt Accept(AuthorizedScope scope, DesiredStateCommand command)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            if (Fail) throw new IOException("private implementation detail");
            Committed = true; Finished.TrySetResult(); return Receipt;
        }
        public void CreateInstance(InstanceId id) => throw new NotSupportedException();
        public InstanceSpec ReadInstance(InstanceId id) => throw new NotSupportedException();
        public OperationReceipt? Find(OperationId id) => throw new NotSupportedException();
        public IReadOnlyList<OutboxItem> ReadPending(int limit) => throw new NotSupportedException();
        public void Dispose() => Release.Dispose();
    }
}
