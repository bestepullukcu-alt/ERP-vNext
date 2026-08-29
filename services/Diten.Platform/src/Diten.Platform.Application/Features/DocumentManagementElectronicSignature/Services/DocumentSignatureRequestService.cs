using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;

/// <summary>
/// MOD-0029-FU23 — signature request lifecycle (GMG-QMS-SOP-0001 §11.2): who is being asked to sign what, with
/// which meaning, and by when.
///
/// TWO RULES CARRY THE REGULATORY WEIGHT:
/// • A DUE DATE CANNOT BE IN THE PAST. An already-overdue request is a fabricated deadline, and deadlines in this
///   domain are evidence about how the organisation manages its obligations.
/// • A SIGNED REQUEST IS TERMINAL. Cancelling or rejecting it afterwards would let the paperwork contradict the
///   act it produced — so both are refused once a signature exists.
///
/// Nothing is hard-deleted; cancellation and rejection are status changes, each requiring a reason.
/// </summary>
public sealed class DocumentSignatureRequestService
{
    private readonly IDocumentSignatureRequestRepository _requests;
    private readonly DocumentSignableSubjectResolver _resolver;
    private readonly DocumentSignaturePolicyService _policies;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentSignatureRequestService(
        IDocumentSignatureRequestRepository requests,
        DocumentSignableSubjectResolver resolver,
        DocumentSignaturePolicyService policies,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _requests = requests;
        _resolver = resolver;
        _policies = policies;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<SignatureRequestModel>> CreateAsync(
        CreateSignatureRequestInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;

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

        // An unaddressed request asks nobody, and nobody can later be shown to have failed to answer it.
        if (input.RequestedSignerUserId is null && string.IsNullOrWhiteSpace(input.RequestedSignerRole))
        {
            return Fail("A signature request must nominate either a signer user or a signer role.", 400,
                ElectronicSignatureReasonCodes.SignerRequired, correlationId);
        }

        if (input.DueDate is { } due && due < now)
        {
            return Fail("A signature request due date cannot be in the past.", 400,
                ElectronicSignatureReasonCodes.DueDateInPast, correlationId);
        }

        var subjectType = ElectronicSignatureWire.ParseSubjectType(input.SubjectType);

        if (!DocumentSignableSubjectResolver.IsResolvable(subjectType))
        {
            return Fail(
                $"Signature subject type '{subjectType}' has no resolver in MOD-0029-FU23 and cannot be requested.",
                400, ElectronicSignatureReasonCodes.SubjectNotResolvable, correlationId);
        }

        if (DocumentSignableSubjectResolver.RequiresRegisterEntryId(subjectType) && input.RegisterEntryId is null)
        {
            return Fail($"Signature subject type '{subjectType}' requires a register entry id.", 400,
                ElectronicSignatureReasonCodes.RegisterEntryRequiredForSubject, correlationId);
        }

        // Resolve now so a request can never be raised against a subject the tenant cannot see. This is also the
        // cross-tenant guard: another tenant's subject simply does not resolve.
        var snapshot = await _resolver.ResolveAsync(subjectType, input.SubjectId, input.RegisterEntryId, ct);
        if (snapshot is null)
        {
            return Fail("The signature subject was not found in this tenant.", 404,
                ElectronicSignatureReasonCodes.SubjectNotFound, correlationId);
        }

        var policy = await _policies.ResolveApplicableAsync(subjectType, meaning, ct);

        var request = new DocumentSignatureRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SignatureRequestNumber = $"SRQ-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            SubjectType = subjectType,
            SubjectId = input.SubjectId,
            RegisterEntryId = input.RegisterEntryId ?? snapshot.RegisterEntryId,
            ControlledDocumentId = input.ControlledDocumentId,
            RequestedSignerUserId = input.RequestedSignerUserId,
            RequestedSignerRole = Trim(input.RequestedSignerRole),
            SignatureMeaning = meaning,
            RequestStatus = SignatureRequestStatus.Pending,
            RequestedAt = now,
            RequestedBy = _currentUser.ActorName,
            DueDate = input.DueDate,
            RequestReason = Trim(input.RequestReason),
            RepositoryAssessmentId = input.RepositoryAssessmentId,
            PolicyId = policy?.Id == Guid.Empty ? null : policy?.Id,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _requests.CreateAsync(request, ct);
        return Response<SignatureRequestModel>.Success(
            ElectronicSignatureWire.ToRequest(request, now), 201, correlationId);
    }

    public async Task<Response<SignatureRequestModel>> CancelAsync(
        Guid id, CancelSignatureRequestInput input, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A cancellation reason is required.", 400,
                ElectronicSignatureReasonCodes.ReasonRequired, correlationId);
        }

        if (request!.RequestStatus == SignatureRequestStatus.Signed)
        {
            return Fail("A signed signature request cannot be cancelled.", 409,
                ElectronicSignatureReasonCodes.RequestAlreadySigned, correlationId);
        }

        if (request.IsTerminal())
        {
            return Fail($"The signature request is already {request.RequestStatus}.", 409,
                ElectronicSignatureReasonCodes.RequestInvalidState, correlationId);
        }

        request.RequestStatus = SignatureRequestStatus.Cancelled;
        request.CancellationReason = input.Reason.Trim();
        Touch(request);
        await _requests.UpdateAsync(request, ct);
        return Response<SignatureRequestModel>.Success(
            ElectronicSignatureWire.ToRequest(request, DateTimeOffset.UtcNow), correlationId: correlationId);
    }

    public async Task<Response<SignatureRequestModel>> RejectAsync(
        Guid id, RejectSignatureRequestInput input, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400,
                ElectronicSignatureReasonCodes.ReasonRequired, correlationId);
        }

        // A refusal to sign is itself regulated evidence, so it needs a reference just as an approval does.
        if (string.IsNullOrWhiteSpace(input.RejectionEvidenceReference))
        {
            return Fail("A rejection evidence reference is required.", 400,
                ElectronicSignatureReasonCodes.RejectionEvidenceRequired, correlationId);
        }

        if (request!.RequestStatus == SignatureRequestStatus.Signed)
        {
            return Fail("A signed signature request cannot be rejected.", 409,
                ElectronicSignatureReasonCodes.RequestAlreadySigned, correlationId);
        }

        if (request.IsTerminal())
        {
            return Fail($"The signature request is already {request.RequestStatus}.", 409,
                ElectronicSignatureReasonCodes.RequestInvalidState, correlationId);
        }

        var rejectedBy = input.RejectedByUserId ?? _currentUser.UserId;

        // The person refusing must be the person who was asked — otherwise the refusal answers a different question.
        if (!request.IsSignerNominated(rejectedBy, _currentUser.DisplayName))
        {
            return Fail("Only the requested signer (or requested role) can reject this signature request.", 409,
                ElectronicSignatureReasonCodes.SignerNotNominated, correlationId);
        }

        request.RequestStatus = SignatureRequestStatus.Rejected;
        request.RejectionReason = input.Reason.Trim();
        request.RejectionEvidenceReference = input.RejectionEvidenceReference.Trim();
        request.RejectedByUserId = rejectedBy;
        request.RejectedAt = DateTimeOffset.UtcNow;
        Touch(request);
        await _requests.UpdateAsync(request, ct);
        return Response<SignatureRequestModel>.Success(
            ElectronicSignatureWire.ToRequest(request, DateTimeOffset.UtcNow), correlationId: correlationId);
    }

    public async Task<Response<SignatureRequestModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, request) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<SignatureRequestModel>.Success(
            ElectronicSignatureWire.ToRequest(request!, DateTimeOffset.UtcNow), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<SignatureRequestModel>>> ListAsync(
        string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var rows = await _requests.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<SignatureRequestModel>>.Success(
            rows.Select(r => ElectronicSignatureWire.ToRequest(r, now)).ToList(), correlationId: correlationId);
    }

    private async Task<(Response<SignatureRequestModel>? Fail, DocumentSignatureRequest? Request)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var request = await _requests.GetByIdAsync(id, ct);
        return request is null
            ? (Fail("Signature request not found.", 404,
                ElectronicSignatureReasonCodes.RequestNotFound, correlationId), null)
            : (null, request);
    }

    private void Touch(DocumentSignatureRequest r)
    {
        r.UpdatedAt = DateTimeOffset.UtcNow;
        r.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<SignatureRequestModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<SignatureRequestModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
