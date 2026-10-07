namespace Diten.AuthService.Api.Operational;

/// <summary>
/// WP-AUTH-BL570-01 — the machine-wide single-instance lock of an operational command, on every OS.
///
/// <para><b>Why a named <see cref="Mutex"/> (option a), not a Mongo lease (option b).</b> The named
/// <see cref="Semaphore"/> it replaces throws <c>PlatformNotSupportedException</c> outside Windows. A named mutex is
/// supported by .NET on Unix as well (shared state under the runtime's temp directory, measured on macOS 2026-10-07),
/// keeps exactly the old scope — one machine, no database schema, no clock — and the OS gives it back when its owner
/// dies: the next caller sees it ABANDONED, not held. A Mongo lease would add a record, an expiry to choose and a clock
/// to trust, for a guard that was never more than machine-local.</para>
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

    public static OperationalSingleInstanceLock TryAcquire(string name)
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
                mutex = new Mutex(false, name);
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
            throw new InvalidOperationException("OPERATION_LOCK_UNAVAILABLE", failure);
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
