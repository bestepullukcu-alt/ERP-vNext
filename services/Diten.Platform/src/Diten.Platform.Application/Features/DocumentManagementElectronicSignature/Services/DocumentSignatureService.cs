using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;

/// <summary>
/// MOD-0029-FU23 — THE SIGN PATH (GMG-QMS-SOP-0001 §11.2).
///
/// WHAT THIS DOES, IN ORDER, AND WHY THE ORDER MATTERS: resolve the subject (so the signature binds to something
/// real and tenant-visible) → select the policy (most restrictive wins) → enforce the authentication controls the
/// policy demands → evaluate the repository boundary (fail-closed) → capture the fingerprint → THEN write the
/// signature. The fingerprint is taken from the resolved subject, never from caller input, so a caller cannot
/// declare what they signed.
///
/// THE FOUR THINGS THIS DELIBERATELY REFUSES TO DO:
/// • Accept a client SignedAt. The timestamp is always UtcNow — there is no backdating path, by construction.
/// • Fake a second factor. If the policy demands one, signing FAILS with SECOND_FACTOR_NOT_AVAILABLE, because a
///   fabricated 2FA flag is worse evidence than an honest gap.
/// • Mutate the subject. Signing an approval evidence record does not approve anything; FU09–FU22 behaviour is
///   untouched. The signature is a parallel attestation, not a control action.
/// • Claim validation. Every signature is written NotValidated, whatever method was used.
///
/// DUPLICATE HANDLING (product decision): an identical signature — same subject, same meaning, same fingerprint,
/// still valid — returns the EXISTING record rather than writing a second one. Idempotent retries are common on
/// flaky networks, and two identical valid signatures on one object state is not additional evidence, it is noise
/// that later invalidation would have to chase.
/// </summary>
public sealed class DocumentSignatureService
{
    private readonly IDocumentSignatureRecordRepository _signatures;
    private readonly IDocumentSignatureRequestRepository _requests;
    private readonly IDocumentSignedObjectFingerprintRepository _fingerprints;
    private readonly DocumentSignableSubjectResolver _resolver;
    private readonly DocumentSignaturePolicyService _policies;
    private readonly DocumentSignatureBoundaryEvaluator _boundary;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentSignatureService(
        IDocumentSignatureRecordRepository signatures,
        IDocumentSignatureRequestRepository requests,
        IDocumentSignedObjectFingerprintRepository fingerprints,
        DocumentSignableSubjectResolver resolver,
        DocumentSignaturePolicyService policies,
        DocumentSignatureBoundaryEvaluator boundary,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _signatures = signatures;
        _requests = requests;
        _fingerprints = fingerprints;
        _resolver = resolver;
        _policies = policies;
        _boundary = boundary;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<SignatureRecordModel>> SignAsync(
        SignDocumentSubjectInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        // ── 1. what is being signed, and what does it mean ────────────────────
        if (input.SubjectId == Guid.Empty)
        {
            return Fail("A signature subject is required.", 400,
                ElectronicSignatureReasonCodes.SubjectRequired, correlationId);
        }

        if (ElectronicSignatureWire.ParseMeaning(input.SignatureMeaning) is not { } meaning)
        {
            return Fail("A valid signature meaning is required.", 400,
                ElectronicSignatureReasonCodes.MeaningRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.MeaningStatement))
        {
            return Fail(
                "A meaning statement is required: a signature whose meaning is not manifest is not a regulated signature.",
                400, ElectronicSignatureReasonCodes.MeaningStatementRequired, correlationId);
        }

        // ── 2. who is signing ─────────────────────────────────────────────────
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return Fail("An authenticated signer identity is required to sign.", 401,
                ElectronicSignatureReasonCodes.SignerIdentityRequired, correlationId);
        }

        var signerUserId = _currentUser.UserId;
        var signerRole = Trim(input.SignerRole);
        var subjectType = ElectronicSignatureWire.ParseSubjectType(input.SubjectType);

        if (!DocumentSignableSubjectResolver.IsResolvable(subjectType))
        {
            return Fail(
                $"Signature subject type '{subjectType}' has no resolver in MOD-0029-FU23 and cannot be signed.",
                400, ElectronicSignatureReasonCodes.SubjectNotResolvable, correlationId);
        }

        if (DocumentSignableSubjectResolver.RequiresRegisterEntryId(subjectType) && input.RegisterEntryId is null)
        {
            return Fail($"Signature subject type '{subjectType}' requires a register entry id.", 400,
                ElectronicSignatureReasonCodes.RegisterEntryRequiredForSubject, correlationId);
        }

        // ── 3. the request, if this signature answers one ─────────────────────
        DocumentSignatureRequest? request = null;
        if (input.SignatureRequestId is { } requestId)
        {
            request = await _requests.GetByIdAsync(requestId, ct);
            if (request is null)
            {
                return Fail("Signature request not found.", 404,
                    ElectronicSignatureReasonCodes.RequestNotFound, correlationId);
            }

            if (request.IsTerminal())
            {
                return Fail($"The signature request is already {request.RequestStatus}.", 409,
                    ElectronicSignatureReasonCodes.RequestInvalidState, correlationId);
            }

            // A signature from someone who was never asked is not the signature that was requested.
            if (!request.IsSignerNominated(signerUserId, signerRole))
            {
                return Fail(
                    "The signer does not match the requested signer user or role on this signature request.", 409,
                    ElectronicSignatureReasonCodes.SignerNotNominated, correlationId);
            }
        }

        // ── 4. resolve the subject — the cross-tenant guard and the binding ───
        var snapshot = await _resolver.ResolveAsync(subjectType, input.SubjectId, input.RegisterEntryId, ct);
        if (snapshot is null)
        {
            return Fail("The signature subject was not found in this tenant.", 404,
                ElectronicSignatureReasonCodes.SubjectNotFound, correlationId);
        }

        // ── 5. policy, and the authentication controls it demands ─────────────
        var policy = await _policies.ResolveApplicableAsync(subjectType, meaning, ct)
                     ?? DocumentSignaturePolicyService.SafeDefault(subjectType, meaning);

        var authContext = Trim(input.AuthenticationContextReference);

        // NOT IMPLEMENTED, AND SAID SO. There is no second-factor authentication context in the platform. The only
        // alternatives were to block or to accept a client-asserted boolean — and a fabricated 2FA claim on a
        // regulated signature is materially worse than a feature gap.
        if (policy.RequiresSecondFactor)
        {
            return Fail(
                $"The signature policy '{policy.PolicyKey}' requires a second authentication factor. MOD-0029-FU23 " +
                "has no second-factor authentication context and will not record an unverified second-factor claim.",
                501, ElectronicSignatureReasonCodes.SecondFactorNotAvailable, correlationId);
        }

        // Re-authentication is only ever evidenced by an external authentication context reference — never by a
        // client-asserted flag, for the same reason.
        if (policy.RequiresReAuthentication && authContext is null)
        {
            return Fail(
                $"The signature policy '{policy.PolicyKey}' requires re-authentication; an authentication context " +
                "reference must be supplied.",
                400, ElectronicSignatureReasonCodes.ReAuthenticationRequired, correlationId);
        }

        // ── 6. method-specific evidence requirements ──────────────────────────
        var method = ElectronicSignatureWire.ParseMethod(input.SignatureMethod);
        var evidenceReference = Trim(input.SignatureEvidenceReference);
        var providerReference = Trim(input.ExternalProviderReference);

        if (method == SignatureMethod.WetSignatureEvidence && evidenceReference is null)
        {
            return Fail(
                "A wet signature record requires an evidence reference pointing at the signed physical document.",
                400, ElectronicSignatureReasonCodes.WetSignatureEvidenceRequired, correlationId);
        }

        // The reference is stored; no provider API is called, here or anywhere in FU23.
        if (method is SignatureMethod.ExternalProviderReference or SignatureMethod.QualifiedElectronicSignatureReference
            && providerReference is null)
        {
            return Fail(
                $"Signature method '{method}' requires an external provider reference. MOD-0029-FU23 stores the " +
                "reference only and performs no provider call or certificate validation.",
                400, ElectronicSignatureReasonCodes.ExternalProviderReferenceRequired, correlationId);
        }

        // ── 7. repository boundary — fail-closed ──────────────────────────────
        var assessmentId = input.RepositoryAssessmentId ?? request?.RepositoryAssessmentId;
        var boundary = await _boundary.EvaluateAsync(assessmentId, policy, ct);
        if (boundary.Blocked)
        {
            return Fail(boundary.BlockMessage!, 409, boundary.BlockReasonCode!, correlationId);
        }

        // ── 8. duplicate check against the CURRENT object state ───────────────
        var existing = (await _signatures.GetBySubjectAsync(subjectType, input.SubjectId, ct))
            .FirstOrDefault(s => s.SignatureMeaning == meaning
                                 && s.ObjectFingerprint == snapshot.Fingerprint
                                 && s.SignerUserId == signerUserId
                                 && s.IsCurrentlyValid());
        if (existing is not null)
        {
            return Response<SignatureRecordModel>.Success(
                ElectronicSignatureWire.ToSignature(existing), correlationId: correlationId);
        }

        // ── 9. persist the object state, then the signature ───────────────────
        var now = DateTimeOffset.UtcNow;

        var fingerprint = new DocumentSignedObjectFingerprint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectType = subjectType,
            SubjectId = input.SubjectId,
            RegisterEntryId = input.RegisterEntryId ?? snapshot.RegisterEntryId,
            FingerprintAlgorithm = snapshot.Algorithm,
            FingerprintValue = snapshot.Fingerprint,
            SnapshotSummary = snapshot.SnapshotSummary,
            GeneratedAt = now,
            GeneratedBy = _currentUser.ActorName,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _fingerprints.CreateAsync(fingerprint, ct);

        var signature = new DocumentSignatureRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SignatureNumber = $"SIG-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            SignatureRequestId = request?.Id,
            SubjectType = subjectType,
            SubjectId = input.SubjectId,
            RegisterEntryId = input.RegisterEntryId ?? snapshot.RegisterEntryId,
            ControlledDocumentId = input.ControlledDocumentId ?? request?.ControlledDocumentId,
            SignatureMeaning = meaning,
            SignerUserId = signerUserId,
            SignerDisplayName = _currentUser.DisplayName ?? _currentUser.ActorName,
            SignerRole = signerRole ?? request?.RequestedSignerRole,
            MeaningStatement = input.MeaningStatement.Trim(),

            // SERVER-STAMPED. There is no code path that accepts a caller-supplied signing time.
            SignedAt = now,

            SignatureMethod = method,
            SignatureStatus = SignatureStatus.Valid,
            ObjectFingerprint = snapshot.Fingerprint,
            FingerprintAlgorithm = snapshot.Algorithm,
            ObjectSnapshotReferenceId = fingerprint.Id,
            ObjectSnapshotSummary = snapshot.SnapshotSummary,
            RepositoryAssessmentId = boundary.RepositoryAssessmentId,
            RepositoryTypeAtSigning = boundary.RepositoryTypeAtSigning,
            RepositoryBoundaryStatement = boundary.BoundaryStatement,
            AuthenticationContextReference = authContext,

            // Derived from the presence of an authentication context — never from client input.
            ReAuthenticationPerformed = authContext is not null,

            // Always false in FU23. See the RequiresSecondFactor block above.
            SecondFactorPerformed = false,

            SignatureEvidenceReference = evidenceReference,
            ExternalProviderReference = providerReference,

            // No provider is called and no certificate is validated, so nothing here was ever validated.
            ValidationResult = SignatureValidationResult.NotValidated,
            ValidationDetails = MethodValidationNote(method),

            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _signatures.CreateAsync(signature, ct);

        // ── 10. settle the request. The SUBJECT is deliberately untouched. ────
        if (request is not null)
        {
            request.RequestStatus = SignatureRequestStatus.Signed;
            request.SignatureRecordId = signature.Id;
            request.SignedAt = now;
            request.UpdatedAt = now;
            request.UpdatedBy = _currentUser.ActorName;
            await _requests.UpdateAsync(request, ct);
        }

        return Response<SignatureRecordModel>.Success(
            ElectronicSignatureWire.ToSignature(signature), 201, correlationId);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<Response<SignatureRecordModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var signature = await _signatures.GetByIdAsync(id, ct);
        return signature is null
            ? Fail("Signature not found.", 404, ElectronicSignatureReasonCodes.SignatureNotFound, correlationId)
            : Response<SignatureRecordModel>.Success(
                ElectronicSignatureWire.ToSignature(signature), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<SignatureRecordModel>>> ListAsync(
        string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _signatures.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<SignatureRecordModel>>.Success(
            rows.Select(ElectronicSignatureWire.ToSignature).ToList(), correlationId: correlationId);
    }

    /// <summary>The full attestation history for one subject — invalidated records included, never filtered out.</summary>
    public async Task<Response<IReadOnlyList<SignatureRecordModel>>> GetBySubjectAsync(
        string subjectType, Guid subjectId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _signatures.GetBySubjectAsync(
            ElectronicSignatureWire.ParseSubjectType(subjectType), subjectId, ct);
        return Response<IReadOnlyList<SignatureRecordModel>>.Success(
            rows.Select(ElectronicSignatureWire.ToSignature).ToList(), correlationId: correlationId);
    }

    private static string MethodValidationNote(SignatureMethod method) => method switch
    {
        SignatureMethod.InternalAttestation =>
            "Internal attestation recorded by MOD-0029-FU23. This is NOT a qualified electronic signature.",
        SignatureMethod.WetSignatureEvidence =>
            "Wet signature evidence reference recorded. The physical signature is held outside this platform and is " +
            "not an electronic signature.",
        SignatureMethod.SeparateApprovalMechanism =>
            "Approval performed in a separate assessed mechanism; this record references that mechanism's evidence.",
        SignatureMethod.ExternalProviderReference =>
            "External provider reference stored. No provider API was called and no validation was performed.",
        SignatureMethod.QualifiedElectronicSignatureReference =>
            "Qualified electronic signature REFERENCE stored. MOD-0029-FU23 performed no certificate chain " +
            "validation and makes no qualified signature claim; validation status remains NotValidated.",
        _ => "Recorded by MOD-0029-FU23. Not validated."
    };

    private static Response<SignatureRecordModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<SignatureRecordModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
