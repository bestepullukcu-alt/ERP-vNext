using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.S2S;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class S2SReplayReceiptStore : IS2SReplayReceiptStore
{
    public const string CollectionName = "s2sReplayReceipts";
    public const string IssuerJtiUniqueIndexName = "ux_s2s_replay_issuer_jti";
    public const string IssuerNonceUniqueIndexName = "ux_s2s_replay_issuer_nonce";

    private readonly IMongoCollection<S2SReplayReceipt> _collection;
    private readonly IS2SMongoContext _context;

    public S2SReplayReceiptStore(IS2SMongoContext context)
    {
        _context = context;
        _collection = context.ReplayReceipts;
    }

    public async Task<ReplayReceiptAcceptance> TryAcceptAsync(S2SReplayReceipt receipt, CancellationToken cancellationToken)
    {
        try
        {
            await _context.EnsureCompatibleAsync(cancellationToken);
            await _collection.InsertOneAsync(receipt, cancellationToken: cancellationToken);
            return ReplayReceiptAcceptance.Accepted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return ReplayReceiptAcceptance.Replay();
        }
        catch (MongoException)
        {
            return ReplayReceiptAcceptance.AuthorityUnavailable();
        }
        catch (TimeoutException)
        {
            return ReplayReceiptAcceptance.AuthorityUnavailable();
        }
    }
}
