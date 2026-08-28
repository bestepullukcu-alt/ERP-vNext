using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Commands;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Queries;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Handlers;

// MOD-0029-FU23 — thin MediatR handlers delegating to the policy / request / signature / verification services.

// ── signature policies ───────────────────────────────────────────────────────

public sealed class CreateSignaturePolicyHandler(DocumentSignaturePolicyService s)
    : IRequestHandler<CreateSignaturePolicyCommand, Response<SignaturePolicyModel>>
{
    public Task<Response<SignaturePolicyModel>> Handle(CreateSignaturePolicyCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class ActivateSignaturePolicyHandler(DocumentSignaturePolicyService s)
    : IRequestHandler<ActivateSignaturePolicyCommand, Response<SignaturePolicyModel>>
{
    public Task<Response<SignaturePolicyModel>> Handle(ActivateSignaturePolicyCommand r, CancellationToken ct) =>
        s.ActivateAsync(r.Id, r.CorrelationId, ct);
}

public sealed class RetireSignaturePolicyHandler(DocumentSignaturePolicyService s)
    : IRequestHandler<RetireSignaturePolicyCommand, Response<SignaturePolicyModel>>
{
    public Task<Response<SignaturePolicyModel>> Handle(RetireSignaturePolicyCommand r, CancellationToken ct) =>
        s.RetireAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetSignaturePoliciesHandler(DocumentSignaturePolicyService s)
    : IRequestHandler<GetSignaturePoliciesQuery, Response<IReadOnlyList<SignaturePolicyModel>>>
{
    public Task<Response<IReadOnlyList<SignaturePolicyModel>>> Handle(GetSignaturePoliciesQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetSignaturePolicyByIdHandler(DocumentSignaturePolicyService s)
    : IRequestHandler<GetSignaturePolicyByIdQuery, Response<SignaturePolicyModel>>
{
    public Task<Response<SignaturePolicyModel>> Handle(GetSignaturePolicyByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

// ── signature requests ───────────────────────────────────────────────────────

public sealed class CreateSignatureRequestHandler(DocumentSignatureRequestService s)
    : IRequestHandler<CreateSignatureRequestCommand, Response<SignatureRequestModel>>
{
    public Task<Response<SignatureRequestModel>> Handle(CreateSignatureRequestCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class CancelSignatureRequestHandler(DocumentSignatureRequestService s)
    : IRequestHandler<CancelSignatureRequestCommand, Response<SignatureRequestModel>>
{
    public Task<Response<SignatureRequestModel>> Handle(CancelSignatureRequestCommand r, CancellationToken ct) =>
        s.CancelAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RejectSignatureRequestHandler(DocumentSignatureRequestService s)
    : IRequestHandler<RejectSignatureRequestCommand, Response<SignatureRequestModel>>
{
    public Task<Response<SignatureRequestModel>> Handle(RejectSignatureRequestCommand r, CancellationToken ct) =>
        s.RejectAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetSignatureRequestsHandler(DocumentSignatureRequestService s)
    : IRequestHandler<GetSignatureRequestsQuery, Response<IReadOnlyList<SignatureRequestModel>>>
{
    public Task<Response<IReadOnlyList<SignatureRequestModel>>> Handle(GetSignatureRequestsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetSignatureRequestByIdHandler(DocumentSignatureRequestService s)
    : IRequestHandler<GetSignatureRequestByIdQuery, Response<SignatureRequestModel>>
{
    public Task<Response<SignatureRequestModel>> Handle(GetSignatureRequestByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

// ── signing / verification ───────────────────────────────────────────────────

public sealed class SignDocumentSubjectHandler(DocumentSignatureService s)
    : IRequestHandler<SignDocumentSubjectCommand, Response<SignatureRecordModel>>
{
    public Task<Response<SignatureRecordModel>> Handle(SignDocumentSubjectCommand r, CancellationToken ct) =>
        s.SignAsync(r.Input, r.CorrelationId, ct);
}

public sealed class VerifySignatureHandler(DocumentSignatureVerificationService s)
    : IRequestHandler<VerifySignatureCommand, Response<SignatureVerificationModel>>
{
    public Task<Response<SignatureVerificationModel>> Handle(VerifySignatureCommand r, CancellationToken ct) =>
        s.VerifyAsync(r.Id, r.CorrelationId, ct);
}

public sealed class InvalidateSignatureHandler(DocumentSignatureVerificationService s)
    : IRequestHandler<InvalidateSignatureCommand, Response<SignatureRecordModel>>
{
    public Task<Response<SignatureRecordModel>> Handle(InvalidateSignatureCommand r, CancellationToken ct) =>
        s.InvalidateAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetSignaturesHandler(DocumentSignatureService s)
    : IRequestHandler<GetSignaturesQuery, Response<IReadOnlyList<SignatureRecordModel>>>
{
    public Task<Response<IReadOnlyList<SignatureRecordModel>>> Handle(GetSignaturesQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetSignatureByIdHandler(DocumentSignatureService s)
    : IRequestHandler<GetSignatureByIdQuery, Response<SignatureRecordModel>>
{
    public Task<Response<SignatureRecordModel>> Handle(GetSignatureByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetSignaturesBySubjectHandler(DocumentSignatureService s)
    : IRequestHandler<GetSignaturesBySubjectQuery, Response<IReadOnlyList<SignatureRecordModel>>>
{
    public Task<Response<IReadOnlyList<SignatureRecordModel>>> Handle(GetSignaturesBySubjectQuery r, CancellationToken ct) =>
        s.GetBySubjectAsync(r.SubjectType, r.SubjectId, r.CorrelationId, ct);
}

public sealed class GetSignedObjectFingerprintsHandler(DocumentSignatureVerificationService s)
    : IRequestHandler<GetSignedObjectFingerprintsQuery, Response<IReadOnlyList<SignedObjectFingerprintModel>>>
{
    public Task<Response<IReadOnlyList<SignedObjectFingerprintModel>>> Handle(GetSignedObjectFingerprintsQuery r, CancellationToken ct) =>
        s.GetFingerprintHistoryAsync(r.SubjectType, r.SubjectId, r.CorrelationId, ct);
}
