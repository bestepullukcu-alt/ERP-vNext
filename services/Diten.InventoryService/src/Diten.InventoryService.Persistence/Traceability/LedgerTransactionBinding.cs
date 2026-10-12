using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// WP-0174-P2 — the posting transaction's session, made visible to <see cref="InventoryLotDirectory"/> for the duration of
/// ONE transaction attempt (request-scoped, set by the ledger repository inside <c>WithTransactionAsync</c> and cleared at
/// the end of the attempt). While it is bound, every lot / serial read and write of the directory runs in that
/// transaction; unbound, the directory answers the pre-check outside any transaction.
/// </summary>
public sealed class LedgerTransactionBinding
{
    public IClientSessionHandle? Session { get; private set; }

    public IDisposable Bind(IClientSessionHandle session)
    {
        if (Session is not null)
        {
            throw new InvalidOperationException("A posting transaction is already bound to this request.");
        }

        Session = session;
        return new Release(this);
    }

    private sealed class Release(LedgerTransactionBinding owner) : IDisposable
    {
        public void Dispose() => owner.Session = null;
    }
}

/// <summary>
/// A registered lot or a serial forbids the posting, decided INSIDE the posting transaction (pack §12 code). Thrown by
/// <see cref="InventoryLotDirectory"/> / the ledger repository; aborts the transaction, so nothing is written.
/// </summary>
public sealed class LotPostingRefusedException : Exception
{
    public LotPostingRefusedException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }
}
