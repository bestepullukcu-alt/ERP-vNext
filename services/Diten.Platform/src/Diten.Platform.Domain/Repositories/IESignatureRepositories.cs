using Diten.Platform.Domain.Entities.ESignature;

namespace Diten.Platform.Domain.Repositories;

// MOD-0022 — e-signature repositories.
//
// INSERT-ONLY (pack §4 "Insert-only enforcement", §16 E2): IESignatureRecordRepository, IESignatureInvalidationRepository,
// IESignatureExportRepository and IESignaturePolicyChangeRepository have InsertAsync plus reads and NOTHING else — no
// update, no replace, no delete. Their implementations do not derive from TenantRepository<T> (which would inherit a
// public soft delete). An architecture test reflects over every IESignature*Repository interface and implementation.
//
// Every read takes the tenant explicitly: a read without a tenant filter is a cross-tenant leak (CLAUDE.md). Every write
// joins the caller's Platform transaction (IPlatformTransactionSession), so a record, the request it fulfils, the meaning
// it locks and its audit intent commit together or not at all.
//
// ⚠ DUPLICATES INSIDE A TRANSACTION THROW. A duplicate-key error ABORTS a Mongo transaction server-side, so an insert
// cannot "return false and carry on": the commit would fail. A unique-index hit inside a session raises
// ESignatureDuplicateException (not a MongoException, so the executor aborts and rethrows it instead of answering 503);
// the handler decides outside the transaction (replay, 409). Measured 2026-10-08: commitTransaction "has been aborted".

public interface IESignatureRecordRepository
{
    /// <summary>Inserts the record. Duplicate (TenantId, SignerUserId, IdempotencyKey) ⇒ <see cref="ESignatureDuplicateException"/>;
    /// a second signature for one workflow task ⇒ <see cref="ESignatureTaskBindingException"/>.</summary>
    Task InsertAsync(IPlatformTransactionSession session, ESignatureRecord record, CancellationToken ct = default);

    Task<ESignatureRecord?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ESignatureRecord?> GetByIdempotencyKeyAsync(Guid tenantId, string signerUserId, string idempotencyKey, CancellationToken ct = default);

    Task<IReadOnlyList<ESignatureRecord>> ListBySubjectAsync(Guid tenantId, string module, string subjectType, string subjectId, CancellationToken ct = default);

    Task<(IReadOnlyList<ESignatureRecord> Items, long Total)> SearchAsync(Guid tenantId, ESignatureSearch search, int skip, int take, CancellationToken ct = default);

    /// <summary>E3 — how many signatures show each sample (a used sample can never be withdrawn).</summary>
    Task<IReadOnlyDictionary<Guid, long>> GetSampleUsageAsync(Guid tenantId, IReadOnlyCollection<Guid> sampleIds, CancellationToken ct = default);
}

public interface IESignatureInvalidationRepository
{
    /// <summary>Inserts the invalidation; a second one for the signature ⇒ <see cref="ESignatureDuplicateException"/>.</summary>
    Task InsertAsync(IPlatformTransactionSession session, ESignatureInvalidation invalidation, CancellationToken ct = default);

    Task<ESignatureInvalidation?> GetBySignatureAsync(Guid tenantId, Guid signatureId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, ESignatureInvalidation>> GetBySignaturesAsync(Guid tenantId, IReadOnlyCollection<Guid> signatureIds, CancellationToken ct = default);
}

public interface IESignatureExportRepository
{
    /// <summary>Inserts the export log row; a second one for (TenantId, ByUserId, IdempotencyKey) ⇒ <see cref="ESignatureDuplicateException"/>.</summary>
    Task InsertAsync(IPlatformTransactionSession session, ESignatureExport export, CancellationToken ct = default);

    Task<ESignatureExport?> GetByIdempotencyKeyAsync(Guid tenantId, string byUserId, string idempotencyKey, CancellationToken ct = default);

    Task<(IReadOnlyList<ESignatureExport> Items, long Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct = default);
}

public interface IESignaturePolicyChangeRepository
{
    Task InsertAsync(IPlatformTransactionSession session, ESignaturePolicyChange change, CancellationToken ct = default);

    Task<IReadOnlyList<ESignaturePolicyChange>> ListAsync(Guid tenantId, Guid? legalEntityId, CancellationToken ct = default);
}

// ── mutable (guarded). Named IESign*Repository on purpose: the IESignature*Repository name is reserved for the
// insert-only stores above, and the architecture test reflects over that name pattern. ─────────────────────────

public interface IESignRequestRepository
{
    /// <summary>Inserts the request; a second one for (TenantId, RequestedBy, IdempotencyKey) ⇒ <see cref="ESignatureDuplicateException"/>.</summary>
    Task InsertAsync(IPlatformTransactionSession session, ESignatureRequest request, CancellationToken ct = default);

    Task<ESignatureRequest?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ESignatureRequest?> GetByIdempotencyKeyAsync(Guid tenantId, string requestedBy, string idempotencyKey, CancellationToken ct = default);

    Task<(IReadOnlyList<ESignatureRequest> Items, long Total, long WaitingCount)> ListForSignerAsync(
        Guid tenantId, string signerUserId, ESignatureRequestQuery query, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Compare-and-set: writes the decision only while the stored row is still Waiting at <paramref name="expectedVersion"/>
    /// (and bumps the version); false when someone else decided first.
    /// </summary>
    Task<bool> SaveDecisionAsync(IPlatformTransactionSession session, ESignatureRequest request, int expectedVersion, CancellationToken ct = default);

    Task<IReadOnlyList<ESignatureRequest>> ListOverdueAsync(Guid tenantId, DateTime nowUtc, int take, CancellationToken ct = default);
}

public interface IESignPolicyRepository
{
    Task<ESignaturePolicy?> GetTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<ESignaturePolicy?> GetLegalEntityAsync(Guid tenantId, Guid legalEntityId, CancellationToken ct = default);

    Task<IReadOnlyList<ESignaturePolicy>> ListOverridesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Inserts a new policy document. Without a session (tenant seed on first read) a duplicate returns false; inside
    /// a session it raises <see cref="ESignatureDuplicateException"/>.</summary>
    Task<bool> InsertAsync(IPlatformTransactionSession? session, ESignaturePolicy policy, CancellationToken ct = default);

    /// <summary>Replaces when the stored Version equals <paramref name="expectedVersion"/>; the caller bumps Version. False on conflict.</summary>
    Task<bool> ReplaceAsync(IPlatformTransactionSession session, ESignaturePolicy policy, int expectedVersion, CancellationToken ct = default);

    Task<bool> DeleteOverrideAsync(IPlatformTransactionSession session, Guid tenantId, Guid legalEntityId, int expectedVersion, CancellationToken ct = default);

    Task<bool> AnyRowUsesMeaningAsync(Guid tenantId, string meaningCode, CancellationToken ct = default);
}

public interface IESignMeaningRepository
{
    Task<IReadOnlyList<ESignatureMeaning>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken ct = default);

    Task<ESignatureMeaning?> GetAsync(Guid tenantId, string code, CancellationToken ct = default);

    /// <summary>Inserts. Without a session (tenant seed) a duplicate (TenantId, Code) returns false; inside a session it raises
    /// <see cref="ESignatureDuplicateException"/>.</summary>
    Task<bool> InsertAsync(IPlatformTransactionSession? session, ESignatureMeaning meaning, CancellationToken ct = default);

    Task<bool> ReplaceAsync(IPlatformTransactionSession session, ESignatureMeaning meaning, int expectedVersion, CancellationToken ct = default);

    /// <summary>Marks the meaning used (first use locks the code) and counts the use — one atomic update.</summary>
    Task MarkUsedAsync(IPlatformTransactionSession session, Guid tenantId, string code, DateTime atUtc, CancellationToken ct = default);
}

/// <summary>The signable-action catalog is global (see <see cref="ESignableAction"/>): no tenant parameter by design.</summary>
public interface IESignActionRepository
{
    Task<IReadOnlyList<ESignableAction>> ListAsync(CancellationToken ct = default);

    Task<ESignableAction?> GetAsync(string actionCode, CancellationToken ct = default);

    /// <summary>Upsert by ActionCode; true when inserted or changed.</summary>
    Task<bool> UpsertAsync(IPlatformTransactionSession session, ESignableAction action, CancellationToken ct = default);
}

/// <summary>Atomic per-tenant per-year counter (ASSUMPTION-0022-07). Outside the business transaction: a gap is allowed.</summary>
public interface IESignNumberGenerator
{
    Task<long> NextAsync(Guid tenantId, string series, int year, CancellationToken ct = default);
}

public sealed record ESignatureSearch(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Module,
    string? SignerUserId,
    string? MeaningCode,
    Guid? LegalEntityId,
    IReadOnlyCollection<Guid> VisibleLegalEntityIds,
    string ViewerUserId);

public sealed record ESignatureRequestQuery(
    string? Module,
    ESignatureRequestStatus? Status,
    DateTime? FromUtc,
    DateTime? ToUtc);

/// <summary>A unique index refused an insert inside a transaction (the transaction is aborted; decide outside it).</summary>
public sealed class ESignatureDuplicateException(Exception? inner = null)
    : Exception("An e-signature unique index refused the insert.", inner);

/// <summary>
/// A record insert hit the one-signature-per-workflow-task unique index (pack §4; E4 binds a signature to one task). Raised
/// by the record repository, answered 409 ESIGN_SIGNATURE_MISMATCH by the sign handler.
/// </summary>
public sealed class ESignatureTaskBindingException(Exception? inner = null)
    : Exception("A signature is already bound to this workflow task.", inner);

// ── E3 — signature samples ("İmzam"). The two sample stores are INSERT-ONLY (IESignature*Repository naming: the
// architecture test covers them); the owner pointer is a guarded mutable document. ──────────────────────────────

public interface IESignatureSampleRepository
{
    /// <summary>Inserts the sample. Duplicate (TenantId, OwnerUserId, IdempotencyKey) ⇒ <see cref="ESignatureDuplicateException"/>.</summary>
    Task InsertAsync(IPlatformTransactionSession session, ESignatureSample sample, CancellationToken ct = default);

    Task<ESignatureSample?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ESignatureSample?> GetByIdempotencyKeyAsync(Guid tenantId, string ownerUserId, string idempotencyKey, CancellationToken ct = default);

    /// <summary>The owner's samples, newest first.</summary>
    Task<IReadOnlyList<ESignatureSample>> ListByOwnerAsync(Guid tenantId, string ownerUserId, CancellationToken ct = default);

    Task<IReadOnlyList<ESignatureSample>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

public interface IESignatureSampleEventRepository
{
    /// <summary>Appends one event. A second event with the same (TenantId, SampleId, Sequence) ⇒ <see cref="ESignatureDuplicateException"/>.</summary>
    Task InsertAsync(IPlatformTransactionSession session, ESignatureSampleEvent sampleEvent, CancellationToken ct = default);

    /// <summary>One sample's events in sequence order; with a session the read joins the transaction.</summary>
    Task<IReadOnlyList<ESignatureSampleEvent>> ListBySampleAsync(Guid tenantId, Guid sampleId, IPlatformTransactionSession? session = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ESignatureSampleEvent>> ListBySamplesAsync(Guid tenantId, IReadOnlyCollection<Guid> sampleIds, CancellationToken ct = default);

    /// <summary>Ids of the tenant's samples that have a Submitted event (pending candidates — the caller projects their status).</summary>
    Task<IReadOnlyList<Guid>> ListSubmittedSampleIdsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>The tenant's approval decisions (Approved / Rejected), newest first.</summary>
    Task<IReadOnlyList<ESignatureSampleEvent>> ListDecisionsAsync(Guid tenantId, int take, CancellationToken ct = default);
}

public interface IESignSampleOwnerStateRepository
{
    Task<ESignatureSampleOwnerState?> GetAsync(IPlatformTransactionSession? session, Guid tenantId, string ownerUserId, CancellationToken ct = default);

    /// <summary>
    /// Compare-and-set of the owner's active sample inside the transaction: moves the pointer from
    /// <paramref name="expectedActiveSampleId"/> to <paramref name="newActiveSampleId"/>; false when someone moved it first.
    /// The first activation of an owner creates the document (a parallel first creation hits the unique index ⇒
    /// <see cref="ESignatureDuplicateException"/>).
    /// </summary>
    Task<bool> MoveActiveAsync(IPlatformTransactionSession session, Guid tenantId, string ownerUserId, Guid? expectedActiveSampleId,
        Guid? newActiveSampleId, CancellationToken ct = default);
}
