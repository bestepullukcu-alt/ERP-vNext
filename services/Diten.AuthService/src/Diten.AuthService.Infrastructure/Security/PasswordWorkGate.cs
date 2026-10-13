using Diten.AuthService.Application.Common.Exceptions;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX5 addendum item 1 — THE CPU GUARD SITS AROUND THE BCRYPT CALL ONLY. FIX4 took its slot in the
/// door's resource filter, before the body was even read, and held it for the whole request (body, Platform call, store,
/// audit): a client trickling an 8 KB body at Kestrel's minimum rate held a slot ~34 s, and a few such requests a minute shut
/// every sign-in. Now a slot is taken by the password hasher itself (<see cref="BoundedPasswordHasher"/>), only for the
/// hash or the verification: at most <see cref="Slots"/> at once (BCrypt is CPU-bound: one per core), at most
/// <see cref="QueueLength"/> waiting, each for at most <see cref="QueueTimeout"/>; past that the work is refused
/// (<see cref="PasswordWorkCapacityExceededException"/> → 429).
/// <para>FIX6 S2 — THE WAIT HOLDS NO THREAD. FIX5 waited with <c>SemaphoreSlim.Wait</c>: up to 2× cores threads blocked for
/// up to 2 s each, and a starved thread pool slows every request of the service (its health check included). A request
/// path now waits with <see cref="EnterAsync"/> (<c>WaitAsync</c> with the timeout); the synchronous <see cref="Enter"/>
/// never waits at all — a free slot or a refusal at once.</para>
/// <para>The queue (2× slots) is sized to drain within the timeout: one BCrypt verification takes ~250 ms, so every slot
/// works through ~8 in 2 s and a full queue is served in ~0.5 s (measured: <c>A_full_queue_of_quarter_second_work_drains_within_the_timeout</c>).
/// A longer queue would only turn waits into refusals at the 2-second mark.</para>
/// </summary>
public sealed class PasswordWorkGate : IDisposable
{
    public static readonly int DefaultSlots = Math.Max(2, Environment.ProcessorCount);
    public static readonly TimeSpan DefaultQueueTimeout = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _slots;
    private int _waiting;

    public PasswordWorkGate() : this(DefaultSlots, 2 * DefaultSlots, DefaultQueueTimeout) { }

    public PasswordWorkGate(int slots, int queueLength, TimeSpan queueTimeout)
    {
        Slots = slots;
        QueueLength = queueLength;
        QueueTimeout = queueTimeout;
        _slots = new SemaphoreSlim(slots, slots);
    }

    public int Slots { get; }
    public int QueueLength { get; }
    public TimeSpan QueueTimeout { get; }

    /// <summary>A FREE slot, now, or a refusal — never a wait (FIX6 S2: a synchronous caller would wait on a thread). Dispose
    /// it when the work is done.</summary>
    public IDisposable Enter()
        => _slots.Wait(0) ? new Slot(_slots) : throw new PasswordWorkCapacityExceededException();

    /// <summary>A slot for one piece of password work, waited for WITHOUT holding a thread: a free one at once, else a place
    /// in the queue (refused when it is full) for at most <see cref="QueueTimeout"/>. Dispose it when the work is done.</summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        if (_slots.Wait(0)) return new Slot(_slots); // a free slot: taken, nothing waited for

        if (Interlocked.Increment(ref _waiting) > QueueLength)
        {
            Interlocked.Decrement(ref _waiting);
            throw new PasswordWorkCapacityExceededException();
        }
        try
        {
            if (!await _slots.WaitAsync(QueueTimeout, cancellationToken).ConfigureAwait(false))
                throw new PasswordWorkCapacityExceededException();
            return new Slot(_slots);
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }
    }

    public T Run<T>(Func<T> work)
    {
        using var slot = Enter();
        return work();
    }

    /// <summary>The work runs after the slot is taken; the slot is given back whatever the work does (an exception too).</summary>
    public async Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        using var slot = await EnterAsync(cancellationToken).ConfigureAwait(false);
        return work();
    }

    public void Dispose() => _slots.Dispose();

    private sealed class Slot(SemaphoreSlim slots) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) slots.Release();
        }
    }
}

/// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX5 addendum item 1 — the production hasher behind <see cref="PasswordWorkGate"/>.</summary>
public sealed class BoundedPasswordHasher(Diten.AuthService.Application.Common.Interfaces.IPasswordHasher inner, PasswordWorkGate gate)
    : Diten.AuthService.Application.Common.Interfaces.IPasswordHasher
{
    // FIX6 S2 — a synchronous caller takes a FREE slot or is refused; it never waits on a thread. Request paths use the
    // asynchronous pair below.
    public string Hash(string password) => gate.Run(() => inner.Hash(password));

    public bool Verify(string password, string hash) => gate.Run(() => inner.Verify(password, hash));

    public Task<string> HashAsync(string password, CancellationToken cancellationToken = default)
        => gate.RunAsync(() => inner.Hash(password), cancellationToken);

    public Task<bool> VerifyAsync(string password, string hash, CancellationToken cancellationToken = default)
        => gate.RunAsync(() => inner.Verify(password, hash), cancellationToken);
}
