using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.ESignature;

public sealed class ESignatureRepository : IESignatureRepository
{
    private readonly IMongoClient _mongoClient;
    private readonly ITenantContext _tenantContext;
    private readonly IMongoCollection<SignatureEnvelope> _envelopes;
    private readonly IMongoCollection<SignatureParticipant> _participants;
    private readonly IMongoCollection<SignerAttestation> _attestations;
    private readonly IMongoCollection<SignatureVerificationArtifact> _verifications;
    private readonly IMongoCollection<InternalSignatureArtifact> _artifacts;
    private readonly IMongoCollection<SignatureAuditExport> _auditExports;
    private readonly IMongoCollection<SignatureEnvelopeCounter> _counters;

    public ESignatureRepository(
        IPlatformDbContext dbContext,
        IMongoClient mongoClient,
        ITenantContext tenantContext)
    {
        _mongoClient = mongoClient;
        _tenantContext = tenantContext;
        _envelopes = dbContext.GetCollection<SignatureEnvelope>(ESignatureCollectionNames.Envelopes);
        _participants = dbContext.GetCollection<SignatureParticipant>(ESignatureCollectionNames.Participants);
        _attestations = dbContext.GetCollection<SignerAttestation>(ESignatureCollectionNames.Attestations);
        _verifications = dbContext.GetCollection<SignatureVerificationArtifact>(ESignatureCollectionNames.Verifications);
        _artifacts = dbContext.GetCollection<InternalSignatureArtifact>(ESignatureCollectionNames.Artifacts);
        _auditExports = dbContext.GetCollection<SignatureAuditExport>(ESignatureCollectionNames.AuditExports);
        _counters = dbContext.GetCollection<SignatureEnvelopeCounter>(ESignatureCollectionNames.Counters);
    }

    public async Task<string> GenerateEnvelopeNumberAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var year = DateTimeOffset.UtcNow.Year;
        var id = $"{tenantId:N}:{year}";
        var filter = Builders<SignatureEnvelopeCounter>.Filter.Eq(x => x.Id, id);
        var update = Builders<SignatureEnvelopeCounter>.Update
            .SetOnInsert(x => x.Id, id)
            .SetOnInsert(x => x.TenantId, tenantId)
            .SetOnInsert(x => x.Year, year)
            .Inc(x => x.LastSequence, 1);
        var counter = await _counters.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<SignatureEnvelopeCounter>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            ct);
        return $"ESIGN-{year}-{counter.LastSequence:D8}";
    }

    public async Task CreateEnvelopeAsync(
        SignatureEnvelope envelope,
        IReadOnlyList<SignatureParticipant> participants,
        CancellationToken ct = default)
    {
        EnsureTenant(envelope.TenantId);
        if (participants.Any(x => x.TenantId != TenantId || x.EnvelopeId != envelope.Id))
        {
            throw new InvalidOperationException("Signature participants must belong to the current tenant and envelope.");
        }

        using var session = await _mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            await _envelopes.InsertOneAsync(session, envelope, cancellationToken: ct);
            if (participants.Count > 0)
            {
                await _participants.InsertManyAsync(session, participants, cancellationToken: ct);
            }
            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public Task<SignatureEnvelope?> GetEnvelopeAsync(Guid id, CancellationToken ct = default) =>
        _envelopes.Find(EnvelopeFilter(id)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<SignatureEnvelope>> GetEnvelopesAsync(
        int skip,
        int take,
        CancellationToken ct = default) =>
        await _envelopes.Find(ActiveTenantFilter<SignatureEnvelope>())
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);

    public Task<long> CountEnvelopesAsync(CancellationToken ct = default) =>
        _envelopes.CountDocumentsAsync(ActiveTenantFilter<SignatureEnvelope>(), cancellationToken: ct);

    public async Task<IReadOnlyList<SignatureParticipant>> GetParticipantsAsync(
        Guid envelopeId,
        CancellationToken ct = default) =>
        await _participants.Find(Builders<SignatureParticipant>.Filter.And(
                ActiveTenantFilter<SignatureParticipant>(),
                Builders<SignatureParticipant>.Filter.Eq(x => x.EnvelopeId, envelopeId)))
            .SortBy(x => x.SigningOrder)
            .ToListAsync(ct);

    public Task<SignatureParticipant?> GetParticipantAsync(
        Guid envelopeId,
        Guid participantId,
        CancellationToken ct = default) =>
        _participants.Find(Builders<SignatureParticipant>.Filter.And(
                ActiveTenantFilter<SignatureParticipant>(),
                Builders<SignatureParticipant>.Filter.Eq(x => x.EnvelopeId, envelopeId),
                Builders<SignatureParticipant>.Filter.Eq(x => x.Id, participantId)))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> UpdateParticipantAsync(
        SignatureParticipant participant,
        int expectedVersion,
        CancellationToken ct = default)
    {
        EnsureTenant(participant.TenantId);
        var result = await _participants.ReplaceOneAsync(
            VersionedFilter<SignatureParticipant>(participant.Id, expectedVersion),
            participant,
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> UpdateEnvelopeAsync(
        SignatureEnvelope envelope,
        int expectedVersion,
        CancellationToken ct = default)
    {
        EnsureTenant(envelope.TenantId);
        var result = await _envelopes.ReplaceOneAsync(
            VersionedFilter<SignatureEnvelope>(envelope.Id, expectedVersion),
            envelope,
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> CommitAttestationAsync(
        SignatureEnvelope envelope,
        int expectedEnvelopeVersion,
        SignatureParticipant participant,
        int expectedParticipantVersion,
        SignerAttestation attestation,
        InternalSignatureArtifact? artifact,
        SignatureVerificationArtifact? verification,
        CancellationToken ct = default)
    {
        EnsureTenant(envelope.TenantId);
        EnsureTenant(participant.TenantId);
        EnsureTenant(attestation.TenantId);
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            var participantResult = await _participants.ReplaceOneAsync(
                session,
                VersionedFilter<SignatureParticipant>(participant.Id, expectedParticipantVersion),
                participant,
                cancellationToken: ct);
            if (participantResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(ct);
                return false;
            }

            var envelopeResult = await _envelopes.ReplaceOneAsync(
                session,
                VersionedFilter<SignatureEnvelope>(envelope.Id, expectedEnvelopeVersion),
                envelope,
                cancellationToken: ct);
            if (envelopeResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(ct);
                return false;
            }

            await _attestations.InsertOneAsync(session, attestation, cancellationToken: ct);
            if (artifact is not null)
            {
                EnsureTenant(artifact.TenantId);
                await _artifacts.InsertOneAsync(session, artifact, cancellationToken: ct);
            }
            if (verification is not null)
            {
                EnsureTenant(verification.TenantId);
                await _verifications.InsertOneAsync(session, verification, cancellationToken: ct);
            }

            await session.CommitTransactionAsync(ct);
            return true;
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public Task AddAttestationAsync(SignerAttestation attestation, CancellationToken ct = default)
    {
        EnsureTenant(attestation.TenantId);
        return _attestations.InsertOneAsync(attestation, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<SignerAttestation>> GetAttestationsAsync(
        Guid envelopeId,
        CancellationToken ct = default) =>
        await _attestations.Find(Builders<SignerAttestation>.Filter.And(
                ActiveTenantFilter<SignerAttestation>(),
                Builders<SignerAttestation>.Filter.Eq(x => x.EnvelopeId, envelopeId)))
            .SortBy(x => x.AttestedAt)
            .ToListAsync(ct);

    public Task AddArtifactAsync(InternalSignatureArtifact artifact, CancellationToken ct = default)
    {
        EnsureTenant(artifact.TenantId);
        return _artifacts.InsertOneAsync(artifact, cancellationToken: ct);
    }

    public Task<InternalSignatureArtifact?> GetArtifactAsync(Guid artifactId, CancellationToken ct = default) =>
        _artifacts.Find(Builders<InternalSignatureArtifact>.Filter.And(
                ActiveTenantFilter<InternalSignatureArtifact>(),
                Builders<InternalSignatureArtifact>.Filter.Eq(x => x.Id, artifactId)))
            .FirstOrDefaultAsync(ct);

    public Task AddVerificationAsync(SignatureVerificationArtifact verification, CancellationToken ct = default)
    {
        EnsureTenant(verification.TenantId);
        return _verifications.InsertOneAsync(verification, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<SignatureVerificationArtifact>> GetVerificationsAsync(
        Guid envelopeId,
        CancellationToken ct = default) =>
        await _verifications.Find(Builders<SignatureVerificationArtifact>.Filter.And(
                ActiveTenantFilter<SignatureVerificationArtifact>(),
                Builders<SignatureVerificationArtifact>.Filter.Eq(x => x.EnvelopeId, envelopeId)))
            .SortBy(x => x.VerifiedAt)
            .ToListAsync(ct);

    public Task AddAuditExportAsync(SignatureAuditExport auditExport, CancellationToken ct = default)
    {
        EnsureTenant(auditExport.TenantId);
        return _auditExports.InsertOneAsync(auditExport, cancellationToken: ct);
    }

    private Guid TenantId => _tenantContext.TenantId;

    private FilterDefinition<SignatureEnvelope> EnvelopeFilter(Guid id) =>
        Builders<SignatureEnvelope>.Filter.And(
            ActiveTenantFilter<SignatureEnvelope>(),
            Builders<SignatureEnvelope>.Filter.Eq(x => x.Id, id));

    private FilterDefinition<T> ActiveTenantFilter<T>() where T : Diten.Platform.Common.Persistence.TenantScopedEntity =>
        Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(x => x.TenantId, TenantId),
            Builders<T>.Filter.Eq(x => x.IsDeleted, false));

    private FilterDefinition<T> VersionedFilter<T>(Guid id, int expectedVersion)
        where T : Diten.Platform.Common.Persistence.TenantScopedEntity =>
        Builders<T>.Filter.And(
            ActiveTenantFilter<T>(),
            Builders<T>.Filter.Eq(x => x.Id, id),
            Builders<T>.Filter.Eq(x => x.Version, expectedVersion));

    private void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty || tenantId != TenantId)
        {
            throw new InvalidOperationException("Cross-tenant signature persistence is forbidden.");
        }
    }
}
