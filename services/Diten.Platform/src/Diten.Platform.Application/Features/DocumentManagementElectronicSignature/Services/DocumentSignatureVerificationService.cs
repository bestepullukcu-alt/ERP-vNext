using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;

/// <summary>
/// MOD-0029-FU23 — verification and invalidation (GMG-QMS-SOP-0001 §11.2).
///
/// VERIFICATION HERE MEANS ONE SPECIFIC THING, AND NOT ANOTHER. It recomputes the subject's canonical metadata
/// fingerprint and compares it with the one captured at signing. It does NOT validate a certificate, contact a
/// provider, or attest that the signature is legally valid — FU23 has no capability to do any of those, and a
/// method named "verify" that quietly implied them would be the most dangerous thing in this feature.
///
/// WHAT HAPPENS WHEN THE OBJECT CHANGED: the signature moves to RequiresResign. It is NOT deleted and NOT silently
/// downgraded — the record, its original fingerprint and its snapshot summary all remain, because "this was signed,
/// then the object changed" is exactly the fact an auditor needs to see.
///
/// WHAT HAPPENS WHEN THE SUBJECT CANNOT BE RESOLVED (deleted, moved, cross-tenant): the signature moves to
/// RequiresResign as well, never to Valid. A signature we cannot check is not a signature we may trust — fail-closed.
/// </summary>
public sealed class DocumentSignatureVerificationService
{
    private readonly IDocumentSignatureRecordRepository _signatures;
    private readonly IDocumentSignedObjectFingerprintRepository _fingerprints;
    private readonly DocumentSignableSubjectResolver _resolver;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentSignatureVerificationService(
        IDocumentSignatureRecordRepository signatures,
        IDocumentSignedObjectFingerprintRepository fingerprints,
        DocumentSignableSubjectResolver resolver,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _signatures = signatures;
        _fingerprints = fingerprints;
        _resolver = resolver;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<SignatureVerificationModel>> VerifyAsync(
        Guid signatureId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var signature = await _signatures.GetByIdAsync(signatureId, ct);
        if (signature is null)
        {
            return Response<SignatureVerificationModel>.Fail(
                "Signature not found.", 404, ElectronicSignatureReasonCodes.SignatureNotFound, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var statusBefore = signature.SignatureStatus;

        // An already-invalidated or revoked signature is reported as-is. Re-verifying it must never resurrect it.
        if (statusBefore is SignatureStatus.Invalidated or SignatureStatus.Revoked or SignatureStatus.Rejected)
        {
            signature.LastVerifiedAt = now;
            await _signatures.UpdateAsync(signature, ct);
            return Ok(signature, statusBefore, SignatureVerificationOutcome.AlreadyInvalidated, null, false, now,
                null,
                $"The signature is {statusBefore} and was not re-evaluated. Its recorded state is preserved.",
                correlationId);
        }

        var snapshot = await _resolver.ResolveAsync(
            signature.SubjectType, signature.SubjectId, signature.RegisterEntryId, ct);

        // Fail-closed: unresolvable is never reported as valid.
        if (snapshot is null)
        {
            signature.SignatureStatus = SignatureStatus.RequiresResign;
            signature.LastVerifiedAt = now;
            Touch(signature);
            await _signatures.UpdateAsync(signature, ct);
            return Ok(signature, statusBefore, SignatureVerificationOutcome.SubjectUnresolvable, null, false, now,
                null,
                "The signed subject could not be resolved in this tenant, so the signature cannot be confirmed " +
                "against it. Marked as requiring re-signature; the original record is preserved unchanged.",
                correlationId);
        }

        var matches = string.Equals(snapshot.Fingerprint, signature.ObjectFingerprint, StringComparison.Ordinal);

        if (matches)
        {
            // A previously RequiresResign signature whose object was restored to the signed state legitimately
            // returns to Valid: it still describes the object exactly as signed.
            signature.SignatureStatus = SignatureStatus.Valid;
            signature.LastVerifiedAt = now;
            Touch(signature);
            await _signatures.UpdateAsync(signature, ct);
            return Ok(signature, statusBefore, SignatureVerificationOutcome.FingerprintMatches,
                snapshot.Fingerprint, true, now, snapshot.SnapshotSummary,
                "The subject's canonical metadata fingerprint matches the one captured at signing. NOTE: this " +
                "confirms object integrity only — MOD-0029-FU23 performs no certificate or provider validation.",
                correlationId);
        }

        signature.SignatureStatus = SignatureStatus.RequiresResign;
        signature.LastVerifiedAt = now;
        Touch(signature);
        await _signatures.UpdateAsync(signature, ct);

        // Record the CURRENT state as its own fingerprint row: verification should leave behind what it saw, so a
        // later reviewer can compare the two projections rather than re-deriving them.
        await _fingerprints.CreateAsync(new DocumentSignedObjectFingerprint
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            SubjectType = signature.SubjectType,
            SubjectId = signature.SubjectId,
            RegisterEntryId = signature.RegisterEntryId,
            FingerprintAlgorithm = snapshot.Algorithm,
            FingerprintValue = snapshot.Fingerprint,
            SnapshotSummary = snapshot.SnapshotSummary,
            GeneratedAt = now,
            GeneratedBy = _currentUser.ActorName,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Ok(signature, statusBefore, SignatureVerificationOutcome.ObjectChanged,
            snapshot.Fingerprint, false, now, snapshot.SnapshotSummary,
            "The signed object has changed since it was signed, so the signature no longer attests to its current " +
            "state. Marked as requiring re-signature. The original signature and its snapshot are preserved.",
            correlationId);
    }

    /// <summary>
    /// Manual invalidation. A reason is mandatory — an invalidated signature with no stated reason destroys the
    /// evidentiary value of the invalidation itself. Nothing is deleted; only status and reason are written.
    /// </summary>
    public async Task<Response<SignatureRecordModel>> InvalidateAsync(
        Guid signatureId, InvalidateSignatureInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var signature = await _signatures.GetByIdAsync(signatureId, ct);
        if (signature is null)
        {
            return Response<SignatureRecordModel>.Fail(
                "Signature not found.", 404, ElectronicSignatureReasonCodes.SignatureNotFound, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Response<SignatureRecordModel>.Fail(
                "An invalidation reason is required.", 400,
                ElectronicSignatureReasonCodes.InvalidationReasonRequired, correlationId);
        }

        if (signature.SignatureStatus == SignatureStatus.Invalidated)
        {
            return Response<SignatureRecordModel>.Fail(
                "The signature is already invalidated.", 409,
                ElectronicSignatureReasonCodes.SignatureAlreadyInvalidated, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        signature.SignatureStatus = SignatureStatus.Invalidated;
        signature.InvalidationReason = input.Reason.Trim();
        signature.InvalidatedAt = now;
        signature.InvalidatedBy = _currentUser.ActorName;
        Touch(signature);
        await _signatures.UpdateAsync(signature, ct);

        return Response<SignatureRecordModel>.Success(
            ElectronicSignatureWire.ToSignature(signature), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<SignedObjectFingerprintModel>>> GetFingerprintHistoryAsync(
        string subjectType, Guid subjectId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _fingerprints.GetBySubjectAsync(
            ElectronicSignatureWire.ParseSubjectType(subjectType), subjectId, ct);
        return Response<IReadOnlyList<SignedObjectFingerprintModel>>.Success(
            rows.Select(ElectronicSignatureWire.ToFingerprint).ToList(), correlationId: correlationId);
    }

    private static Response<SignatureVerificationModel> Ok(
        DocumentSignatureRecord signature,
        SignatureStatus statusBefore,
        SignatureVerificationOutcome outcome,
        string? currentFingerprint,
        bool matches,
        DateTimeOffset verifiedAt,
        string? currentSnapshotSummary,
        string note,
        string correlationId) =>
        Response<SignatureVerificationModel>.Success(new SignatureVerificationModel(
            signature.Id,
            statusBefore.ToString(),
            signature.SignatureStatus.ToString(),
            outcome.ToString(),
            signature.ObjectFingerprint,
            currentFingerprint,
            matches,
            verifiedAt,
            currentSnapshotSummary,
            note,
            ElectronicSignatureWire.BoundaryStatement), correlationId: correlationId);

    private void Touch(DocumentSignatureRecord s)
    {
        s.UpdatedAt = DateTimeOffset.UtcNow;
        s.UpdatedBy = _currentUser.ActorName;
    }
}
