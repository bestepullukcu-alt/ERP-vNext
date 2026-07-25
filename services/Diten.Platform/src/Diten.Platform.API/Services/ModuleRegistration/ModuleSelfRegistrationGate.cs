namespace Diten.Platform.API.Services.ModuleRegistration;

/// <summary>
/// Startup ordering gate between module self-registration and permission auto-registration (A1).
///
/// <para><b>Why this exists.</b> Both run as <see cref="BackgroundService"/>s. The manifest reconcile syncs each
/// permission WITH its owning ModuleCode and its route-derived authz Scope; A1 syncs the same keys with
/// <c>moduleCode/scope = null</c>, so whichever reaches a given key FIRST determines its attribution. A key that A1
/// creates first is stamped <c>Module = "platform"</c> + <c>Scope = PlatformAdmin</c>, and AuthService's scope
/// tie-break ("most restrictive wins") has NO downgrade path — so the key can never afterwards be assigned to a
/// tenant role. Registration order alone does not fix this: the manifest worker walks every provider (each doing
/// several database round-trips) while A1 blasts through a flat key list, so A1 still reaches a late provider's key
/// first.</para>
///
/// <para><b>What it guarantees.</b> A1 does not begin syncing until self-registration has actually FINISHED
/// (a real completion signal, never a timed guess — a delay would just be a slower race). The manifest therefore
/// always wins attribution for every module it owns.</para>
///
/// <para><b>Fail-safe.</b> Completion is signalled from a <c>finally</c>, so a crashing/failing manifest worker
/// still releases the gate. A1 additionally waits with a bounded timeout: if the signal never arrives, A1 proceeds
/// anyway and logs it, because a key that EXISTS with imperfect attribution is better than a missing key (the
/// endpoint would otherwise 403 for everyone).</para>
/// </summary>
public sealed class ModuleSelfRegistrationGate
{
    private readonly TaskCompletionSource _completed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>How long A1 waits for self-registration before falling back (bounded, never infinite).</summary>
    public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>True once self-registration has signalled completion (successfully or not).</summary>
    public bool IsCompleted => _completed.Task.IsCompleted;

    /// <summary>Signals that self-registration has finished. Idempotent — safe to call from a finally block.</summary>
    public void MarkCompleted() => _completed.TrySetResult();

    /// <summary>
    /// Waits for the completion signal. Returns <c>true</c> when self-registration completed, <c>false</c> when the
    /// timeout elapsed or the host is shutting down (caller decides the fallback).
    /// </summary>
    public async Task<bool> WaitForCompletionAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await _completed.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
