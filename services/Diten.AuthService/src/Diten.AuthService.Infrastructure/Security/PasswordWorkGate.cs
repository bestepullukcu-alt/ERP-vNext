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

    /// <summary>A slot for one piece of password work; dispose it when the work is done. Throws when none can be had.</summary>
    public IDisposable Enter()
    {
        if (_slots.Wait(0)) return new Slot(_slots);

        if (Interlocked.Increment(ref _waiting) > QueueLength)
        {
            Interlocked.Decrement(ref _waiting);
            throw new PasswordWorkCapacityExceededException();
        }
        try
        {
            if (!_slots.Wait(QueueTimeout)) throw new PasswordWorkCapacityExceededException();
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
    public string Hash(string password) => gate.Run(() => inner.Hash(password));

    public bool Verify(string password, string hash) => gate.Run(() => inner.Verify(password, hash));
}
