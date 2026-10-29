using System.Collections.Concurrent;
using System.Diagnostics;
using Nexa.Xsr;
using Nexa.Xsr.Runtime;
using NexaSM.Contracts;
using NexaSM.Domain;

namespace NexaSM.Services.Composition;

/// <summary>Local typed management entry; no Sidecar discovery, sessions or readiness prerequisite.</summary>
public sealed class LocalManagement : IAsyncDisposable
{
    // Reserve 20 ms of the 100 ms external response budget for adapters/serialization.
    public static readonly TimeSpan AdmissionResponseBudget = TimeSpan.FromMilliseconds(80);
    private static readonly XsrSemanticId SubmitRoute = XsrSemanticId.Parse("nexasm.operations.submit");
    private readonly XsrCommandRouter router;
    private readonly XsrCommandId submitId;
    private readonly OperationService service;
    private readonly int maximumConcurrent;
    private int active;
    private int disposed;
    private readonly BlockingCollection<AdmissionCall> work;
    private readonly Task worker;

    public LocalManagement(OperationService service, int maximumConcurrent = 4)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (maximumConcurrent is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(maximumConcurrent));
        this.service = service;
        this.maximumConcurrent = maximumConcurrent;
        work = new BlockingCollection<AdmissionCall>(maximumConcurrent);
        worker = Task.Factory.StartNew(ProcessAdmissions, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var builder = new XsrCommandRouterBuilder();
        builder.Register<AdmissionCall>(SubmitRoute, AdmitAsync);
        router = builder.Build(new Observer());
        if (!router.TryResolve(SubmitRoute, out submitId)) throw new InvalidOperationException("Missing admission route.");
    }

    public async Task<OperationReceipt> SubmitAsync(AuthorizedScope scope, DesiredStateCommand command,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        var call = new AdmissionCall(scope, command);
        // Cancellation affects this caller's wait, not an operation already being committed.
        var dispatch = router.Dispatch(submitId, call);
        var remaining = AdmissionResponseBudget - Stopwatch.GetElapsedTime(started);
        XsrResult result;
        try
        {
            if (remaining <= TimeSpan.Zero) throw new TimeoutException();
            result = await dispatch.Completion.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The storage call may already have committed. Never retry it with a new key.
            throw new DomainException("AdmissionOutcomeUnknown");
        }
        if (!result.IsSuccess || call.Receipt is null) throw new DomainException(call.ErrorCode ?? "AdmissionFailed");
        return call.Receipt;
    }

    private async ValueTask<XsrResult> AdmitAsync(AdmissionCall call, CancellationToken unused)
    {
        if (Volatile.Read(ref disposed) != 0) return Reject(call, "AdmissionUnavailable");
        if (Interlocked.Increment(ref active) > maximumConcurrent)
        {
            Interlocked.Decrement(ref active);
            return Reject(call, "AdmissionBusy");
        }
        try
        {
            try
            {
                if (!work.TryAdd(call)) return Reject(call, "AdmissionBusy");
            }
            catch (InvalidOperationException) { return Reject(call, "AdmissionUnavailable"); }
            call.Receipt = await call.Persisted.Task.ConfigureAwait(false);
            return XsrResult.Success();
        }
        catch (DomainException error) { return Reject(call, error.Code); }
        catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
        { return Reject(call, "AdmissionFailed"); }
        finally { Interlocked.Decrement(ref active); }
    }

    private void ProcessAdmissions()
    {
        foreach (var call in work.GetConsumingEnumerable())
        {
            try { call.Persisted.TrySetResult(service.Submit(call.Scope, call.Command)); }
            catch (DomainException error) { call.Persisted.TrySetException(error); }
            catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
            { call.Persisted.TrySetException(new DomainException("AdmissionFailed")); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) work.CompleteAdding();
        // The daemon owns this lifetime. Drain accepted writes; caller cancellation cannot undo them.
        await worker.ConfigureAwait(false);
    }

    private static XsrResult Reject(AdmissionCall call, string code)
    {
        call.ErrorCode = code;
        return XsrResult.Failure(new XsrError(XsrErrorKind.Rejected,
            XsrSemanticId.Parse("nexasm.admission_rejected"), code));
    }
    private sealed class AdmissionCall(AuthorizedScope scope, DesiredStateCommand command)
    {
        public AuthorizedScope Scope { get; } = scope;
        public DesiredStateCommand Command { get; } = command;
        public TaskCompletionSource<OperationReceipt> Persisted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public OperationReceipt? Receipt { get; set; }
        public string? ErrorCode { get; set; }
    }
    private sealed class Observer : IXsrDispatchObserver
    {
        public void OnCompleted(XsrDispatchObservation observation) { }
    }
}
