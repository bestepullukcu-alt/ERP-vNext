using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0029-FU23 — tenant-scoped Mongo repositories for the electronic signature foundation. No delete operation on
// any of them. Only governance metadata, canonical metadata hashes and reference strings are persisted — never
// document bytes, never a signature file.

public sealed class DocumentSignaturePolicyRepository
    : TenantRepository<DocumentSignaturePolicy>, IDocumentSignaturePolicyRepository
{
    public DocumentSignaturePolicyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_signature_policies") { }

    public new Task<DocumentSignaturePolicy> CreateAsync(DocumentSignaturePolicy p, CancellationToken ct = default) =>
        base.CreateAsync(p, ct);

    public async Task<DocumentSignaturePolicy?> GetByKeyAsync(string policyKey, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentSignaturePolicy>.Filter.And(
                ExecutionFilter, Builders<DocumentSignaturePolicy>.Filter.Eq(x => x.PolicyKey, policyKey)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentSignaturePolicy>> GetActiveBySubjectTypeAsync(
        SignableSubjectType subjectType, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentSignaturePolicy>.Filter.And(
                ExecutionFilter,
                Builders<DocumentSignaturePolicy>.Filter.Eq(x => x.SignableSubjectType, subjectType),
                Builders<DocumentSignaturePolicy>.Filter.Eq(x => x.PolicyStatus, SignaturePolicyStatus.Active)))
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentSignaturePolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentSignaturePolicy p, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentSignaturePolicy>.Filter.And(ExecutionFilter,
                Builders<DocumentSignaturePolicy>.Filter.Eq(x => x.Id, p.Id)),
            p, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentSignatureRequestRepository
    : TenantRepository<DocumentSignatureRequest>, IDocumentSignatureRequestRepository
{
    public DocumentSignatureRequestRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_signature_requests") { }

    public new Task<DocumentSignatureRequest> CreateAsync(DocumentSignatureRequest r, CancellationToken ct = default) =>
        base.CreateAsync(r, ct);

    public async Task<IReadOnlyList<DocumentSignatureRequest>> GetBySubjectAsync(
        SignableSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentSignatureRequest>.Filter.And(
                ExecutionFilter,
                Builders<DocumentSignatureRequest>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentSignatureRequest>.Filter.Eq(x => x.SubjectId, subjectId)))
            .SortByDescending(x => x.RequestedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentSignatureRequest>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.RequestedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentSignatureRequest r, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentSignatureRequest>.Filter.And(ExecutionFilter,
                Builders<DocumentSignatureRequest>.Filter.Eq(x => x.Id, r.Id)),
            r, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentSignatureRecordRepository
    : TenantRepository<DocumentSignatureRecord>, IDocumentSignatureRecordRepository
{
    public DocumentSignatureRecordRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_signature_records") { }

    public new Task<DocumentSignatureRecord> CreateAsync(DocumentSignatureRecord s, CancellationToken ct = default) =>
        base.CreateAsync(s, ct);

    public async Task<IReadOnlyList<DocumentSignatureRecord>> GetBySubjectAsync(
        SignableSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentSignatureRecord>.Filter.And(
                ExecutionFilter,
                Builders<DocumentSignatureRecord>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentSignatureRecord>.Filter.Eq(x => x.SubjectId, subjectId)))
            .SortByDescending(x => x.SignedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentSignatureRecord>> GetByRequestAsync(
        Guid signatureRequestId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentSignatureRecord>.Filter.And(
                ExecutionFilter,
                Builders<DocumentSignatureRecord>.Filter.Eq(x => x.SignatureRequestId, signatureRequestId)))
            .SortBy(x => x.SignedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentSignatureRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
        await Collection.Find(ExecutionFilter).SortByDescending(x => x.SignedAt).ToListAsync(ct);

    public async Task<bool> UpdateAsync(DocumentSignatureRecord s, CancellationToken ct = default)
    {
        var result = await Collection.ReplaceOneAsync(
            Builders<DocumentSignatureRecord>.Filter.And(ExecutionFilter,
                Builders<DocumentSignatureRecord>.Filter.Eq(x => x.Id, s.Id)),
            s, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}

public sealed class DocumentSignedObjectFingerprintRepository
    : TenantRepository<DocumentSignedObjectFingerprint>, IDocumentSignedObjectFingerprintRepository
{
    public DocumentSignedObjectFingerprintRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "document_management_signed_object_fingerprints") { }

    public new Task<DocumentSignedObjectFingerprint> CreateAsync(
        DocumentSignedObjectFingerprint f, CancellationToken ct = default) => base.CreateAsync(f, ct);

    public async Task<IReadOnlyList<DocumentSignedObjectFingerprint>> GetBySubjectAsync(
        SignableSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Collection.Find(Builders<DocumentSignedObjectFingerprint>.Filter.And(
                ExecutionFilter,
                Builders<DocumentSignedObjectFingerprint>.Filter.Eq(x => x.SubjectType, subjectType),
                Builders<DocumentSignedObjectFingerprint>.Filter.Eq(x => x.SubjectId, subjectId)))
            .SortByDescending(x => x.GeneratedAt).ToListAsync(ct);
}
