namespace Diten.AuthService.Api.Operational;

/// <summary>
/// WP-AUTH-BL570-01 — the single-instance lock of an operational command, on every OS.
///
/// <para><b>Scope — measured, not assumed (FIX1 R4, CT decision A).</b> A named <see cref="Mutex"/> WITHOUT a prefix is
/// scoped to the Unix SESSION on .NET (measured 2026-10-07 on macOS: a second process in a new session, <c>setsid</c>,
/// acquired the name while the first session held it). So the name is <see cref="MachineWide"/>: <c>Global\</c> —
/// machine-local, ACROSS SESSIONS (measured on macOS: a new session is refused); across USERS not measured (no second
/// account on the measuring machine). Windows: <c>Global\</c> is the machine namespace, shared by every session. Any
/// user may CREATE a <c>Global\</c> mutex — <c>SeCreateGlobalPrivilege</c> is required only for file-mapping and
/// symbolic-link objects (Microsoft Learn, "Kernel object namespaces"; an earlier version of this comment was wrong).
/// The Windows risk is the DACL: the mutex gets its CREATOR's default DACL, so a different account — or a non-elevated
/// token after an elevated owner — cannot OPEN it: <see cref="UnauthorizedAccessException"/> → this lock answers
/// <c>OPERATION_LOCK_HELD_BY_ANOTHER_ACCOUNT</c> (fail closed: "another account holds or created the lock"). Any other
/// failure to create it → <c>OPERATION_LOCK_UNAVAILABLE</c>. An explicit <c>MutexSecurity</c> is not set (proposal in
/// BL570 FIX2 report). Not measured on Windows — the live deploy step measures it.</para>
///
/// <para><b>Security note.</b> On Unix the lock state lives under <c>/tmp/.dotnet</c>, whose directories are world-writable
/// without the sticky bit (files 0666; measured). (1) A local user who HOLDS the lock — or, on Windows, CREATES the
/// <c>Global\</c> name first under their own account — only stops the command (<c>OPERATION_ALREADY_RUNNING</c> /
/// <c>OPERATION_LOCK_HELD_BY_ANOTHER_ACCOUNT</c>): fail closed, availability only. (2) A local user who DELETES the lock file could
/// break the exclusion — not tried (deleting is out of bounds for the measurement), so not measured; the unprefixed name
/// had the same exposure, so the integrity exposure does not change. A store-level lease (across servers) is backlog.</para>
///
/// <para><b>Why a named mutex, not a Mongo lease (option b).</b> The named <see cref="Semaphore"/> it replaces throws
/// <c>PlatformNotSupportedException</c> outside Windows. A named mutex keeps the old scope with no database schema and no
/// clock, and the OS gives it back when its owner dies — the takeover itself always works. Whether the OS also SAYS so
/// (<see cref="AbandonedMutexException"/> → <see cref="RecoveredFromAbandonedRun"/>) varies: macOS gives no signal when
/// the owner PROCESS dies, and with <c>Global\</c> lost it in 2 of 3 in-process owner-thread exits (measured 2026-10-09);
/// Windows is documented to signal it (not measured). The warning is a diagnostic, never a safety condition.</para>
///
/// <para><b>Why a dedicated thread.</b> A mutex is owned by the THREAD that took it, and the command awaits while it
/// holds the lock — its continuation may resume on another thread, where <c>ReleaseMutex</c> throws. So one private
/// thread takes the mutex, holds it until <see cref="Dispose"/>, and releases it on that same thread.</para>
/// </summary>
public sealed class OperationalSingleInstanceLock : IDisposable
{
    private readonly ManualResetEventSlim _release;
    private readonly Thread? _owner;
    private int _disposed;

    /// <summary>This caller holds the lock. False: another run holds it now — answer at once, never wait.</summary>
    public bool Acquired { get; }

    /// <summary>The previous owner ended without releasing (its process crashed): the lock was taken over, by name.</summary>
    public bool RecoveredFromAbandonedRun { get; }

    private OperationalSingleInstanceLock(bool acquired, bool recovered, Thread? owner, ManualResetEventSlim release)
    {
        _release = release;
        Acquired = acquired;
        RecoveredFromAbandonedRun = recovered;
        _owner = owner;
    }

    /// <summary>
    /// A lock in a given state without the OS (tests only, via InternalsVisibleTo): the OS's "abandoned" signal is not
    /// reliable on macOS (measured — none after an owner PROCESS dies; with <c>Global\</c>, lost in 2 of 3 in-process
    /// owner-thread exits), so the runner's reaction to it is tested on a lock that SAYS so.
    /// </summary>
    internal static OperationalSingleInstanceLock InState(bool acquired, bool recovered) =>
        new(acquired, recovered, null, new ManualResetEventSlim(false));

    /// <summary>The machine-wide (cross-session) name: <c>Global\</c> + <paramref name="name"/> (FIX1 R4).</summary>
    public static string MachineWide(string name) => @"Global\" + name;

    public const string HeldByAnotherAccount = "OPERATION_LOCK_HELD_BY_ANOTHER_ACCOUNT";
    public const string Unavailable = "OPERATION_LOCK_UNAVAILABLE";

    public static OperationalSingleInstanceLock TryAcquire(string name) => TryAcquire(name, n => new Mutex(false, n));

    // The seam (tests only, via InternalsVisibleTo): how the named mutex is opened — so the mapping of an open the OS
    // refuses (another account's DACL) is measured without a second account.
    internal static OperationalSingleInstanceLock TryAcquire(string name, Func<string, Mutex> open)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var decided = new ManualResetEventSlim(false);
        var acquired = false;
        var recovered = false;
        Exception? failure = null;
        var release = new ManualResetEventSlim(false);
        var owner = new Thread(() =>
        {
            Mutex? mutex = null;
            try
            {
                mutex = open(name);
                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    // The OS handed us the mutex its dead owner never released.
                    acquired = true;
                    recovered = true;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                decided.Set();
            }

            if (!acquired)
            {
                mutex?.Dispose();
                return;
            }

            release.Wait();
            mutex!.ReleaseMutex();
            mutex.Dispose();
        })
        {
            IsBackground = true,
            Name = "operational-single-instance-lock"
        };
        owner.Start();
        decided.Wait();
        decided.Dispose();
        if (failure is not null)
        {
            release.Dispose();
            // The name exists but this account may not open it: another account holds or created it (Windows DACL).
            throw new InvalidOperationException(
                failure is UnauthorizedAccessException ? HeldByAnotherAccount : Unavailable, failure);
        }

        return new OperationalSingleInstanceLock(acquired, recovered, acquired ? owner : null, release);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _release.Set();
        _owner?.Join();
        _release.Dispose();
    }
}
