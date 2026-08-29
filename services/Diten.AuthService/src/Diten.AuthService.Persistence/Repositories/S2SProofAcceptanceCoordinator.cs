using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.S2S;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class S2SProofAcceptanceCoordinator : IS2SProofAcceptanceCoordinator
{
    private readonly IS2SMongoContext _context;
    public S2SProofAcceptanceCoordinator(IS2SMongoContext context) => _context = context;

    public async Task<S2SProofAcceptanceResult> TryAcceptAsync(S2SProofAcceptanceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _context.EnsureCompatibleAsync(cancellationToken);
        using var session = await _context.StartSessionAsync(cancellationToken);
        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        try
        {
            var principal = await _context.ServicePrincipals.UpdateOneAsync(session, PrincipalFilter(request),
                Builders<ServicePrincipal>.Update.Inc(x => x.ProofValidationFence, 1), cancellationToken: cancellationToken);
            if (principal.MatchedCount != 1) return await AbortStaleAsync(session);
            var credential = await _context.ServiceCredentialDescriptors.UpdateOneAsync(session, CredentialFilter(request),
                Builders<ServiceCredentialDescriptor>.Update.Inc(x => x.ProofValidationFence, 1), cancellationToken: cancellationToken);
            if (credential.MatchedCount != 1) return await AbortStaleAsync(session);
            await _context.ReplayReceipts.InsertOneAsync(session, request.ReplayReceipt, cancellationToken: cancellationToken);
            await CommitOnlyAsync(session, cancellationToken);
            return S2SProofAcceptanceResult.Accepted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { await AbortAsync(session); throw; }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey) { await AbortAsync(session); return S2SProofAcceptanceResult.Replay(); }
        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult")) { await AbortAsync(session); return await ReconcileAsync(request, cancellationToken); }
        catch (MongoException) { await AbortAsync(session); return await ReconcileConcurrentFailureAsync(request, cancellationToken); }
        catch (TimeoutException) { await AbortAsync(session); return S2SProofAcceptanceResult.AuthorityUnavailable(); }
    }

    private static FilterDefinition<ServicePrincipal> PrincipalFilter(S2SProofAcceptanceRequest r) => Builders<ServicePrincipal>.Filter.And(
        Builders<ServicePrincipal>.Filter.Eq(x => x.ServicePrincipalId, r.ServicePrincipalId), Builders<ServicePrincipal>.Filter.Eq(x => x.ClientId, r.ClientId),
        Builders<ServicePrincipal>.Filter.Eq(x => x.PrincipalVersion, r.PrincipalVersion), Builders<ServicePrincipal>.Filter.Eq(x => x.CredentialGeneration, r.CredentialGeneration),
        Builders<ServicePrincipal>.Filter.Eq(x => x.Status, ServicePrincipalStatus.Active), Builders<ServicePrincipal>.Filter.Eq(x => x.NotBeforeUtc, r.PrincipalNotBeforeUtc),
        Builders<ServicePrincipal>.Filter.Eq(x => x.ExpiresAtUtc, r.PrincipalExpiresAtUtc), Builders<ServicePrincipal>.Filter.Eq(x => x.IsDeleted, false));
    private static FilterDefinition<ServiceCredentialDescriptor> CredentialFilter(S2SProofAcceptanceRequest r) => Builders<ServiceCredentialDescriptor>.Filter.And(
        Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.CredentialId, r.CredentialId), Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.ServicePrincipalId, r.ServicePrincipalId),
        Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.Generation, r.CredentialGeneration), Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.Kid, r.Kid),
        Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.Status, ServiceCredentialStatus.Active), Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.NotBeforeUtc, r.CredentialNotBeforeUtc),
        Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.ExpiresAtUtc, r.CredentialExpiresAtUtc), Builders<ServiceCredentialDescriptor>.Filter.Eq(x => x.IsDeleted, false));
    private static async Task CommitOnlyAsync(IClientSessionHandle session, CancellationToken ct)
    { try { await session.CommitTransactionAsync(ct); } catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult")) { await session.CommitTransactionAsync(ct); } }
    private async Task<S2SProofAcceptanceResult> ReconcileAsync(S2SProofAcceptanceRequest r, CancellationToken ct)
    {
        var receipt = await _context.ReplayReceipts.Find(Builders<S2SReplayReceipt>.Filter.And(
            Builders<S2SReplayReceipt>.Filter.Eq(x => x.Issuer, r.ReplayReceipt.Issuer), Builders<S2SReplayReceipt>.Filter.Eq(x => x.Jti, r.ReplayReceipt.Jti),
            Builders<S2SReplayReceipt>.Filter.Eq(x => x.Nonce, r.ReplayReceipt.Nonce), Builders<S2SReplayReceipt>.Filter.Eq(x => x.RequestHash, r.ReplayReceipt.RequestHash))).AnyAsync(ct);
        if (!receipt) return S2SProofAcceptanceResult.AuthorityUnavailable();
        var p = await _context.ServicePrincipals.Find(PrincipalFilter(r)).AnyAsync(ct);
        var c = await _context.ServiceCredentialDescriptors.Find(CredentialFilter(r)).AnyAsync(ct);
        return p && c ? S2SProofAcceptanceResult.Accepted() : S2SProofAcceptanceResult.Replay();
    }
    private async Task<S2SProofAcceptanceResult> ReconcileConcurrentFailureAsync(S2SProofAcceptanceRequest r, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var receipt = await _context.ReplayReceipts.Find(Builders<S2SReplayReceipt>.Filter.And(
                Builders<S2SReplayReceipt>.Filter.Eq(x => x.Issuer, r.ReplayReceipt.Issuer),
                Builders<S2SReplayReceipt>.Filter.Or(
                    Builders<S2SReplayReceipt>.Filter.Eq(x => x.Jti, r.ReplayReceipt.Jti),
                    Builders<S2SReplayReceipt>.Filter.Eq(x => x.Nonce, r.ReplayReceipt.Nonce)))).AnyAsync(ct);
            if (receipt) return S2SProofAcceptanceResult.Replay();
            if (attempt < 4) await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }
        return S2SProofAcceptanceResult.AuthorityUnavailable();
    }
    private static async Task<S2SProofAcceptanceResult> AbortStaleAsync(IClientSessionHandle s) { await s.AbortTransactionAsync(CancellationToken.None); return S2SProofAcceptanceResult.StaleAuthority(); }
    private static async Task AbortAsync(IClientSessionHandle s) { if (!s.IsInTransaction) return; try { await s.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { } }
}
