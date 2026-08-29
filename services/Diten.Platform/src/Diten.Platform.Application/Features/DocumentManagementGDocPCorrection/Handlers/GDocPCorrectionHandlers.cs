using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Commands;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Queries;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Handlers;

// MOD-0029-FU21 — thin MediatR handlers delegating to the correction and policy services.

public sealed class CreateGDocPCorrectionPolicyHandler(DocumentGDocPCorrectionPolicyService s)
    : IRequestHandler<CreateGDocPCorrectionPolicyCommand, Response<GDocPCorrectionPolicyModel>>
{
    public Task<Response<GDocPCorrectionPolicyModel>> Handle(CreateGDocPCorrectionPolicyCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class ActivateGDocPCorrectionPolicyHandler(DocumentGDocPCorrectionPolicyService s)
    : IRequestHandler<ActivateGDocPCorrectionPolicyCommand, Response<GDocPCorrectionPolicyModel>>
{
    public Task<Response<GDocPCorrectionPolicyModel>> Handle(ActivateGDocPCorrectionPolicyCommand r, CancellationToken ct) =>
        s.ActivateAsync(r.Id, r.CorrelationId, ct);
}

public sealed class RetireGDocPCorrectionPolicyHandler(DocumentGDocPCorrectionPolicyService s)
    : IRequestHandler<RetireGDocPCorrectionPolicyCommand, Response<GDocPCorrectionPolicyModel>>
{
    public Task<Response<GDocPCorrectionPolicyModel>> Handle(RetireGDocPCorrectionPolicyCommand r, CancellationToken ct) =>
        s.RetireAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetGDocPCorrectionPoliciesHandler(DocumentGDocPCorrectionPolicyService s)
    : IRequestHandler<GetGDocPCorrectionPoliciesQuery, Response<IReadOnlyList<GDocPCorrectionPolicyModel>>>
{
    public Task<Response<IReadOnlyList<GDocPCorrectionPolicyModel>>> Handle(GetGDocPCorrectionPoliciesQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetGDocPCorrectionPolicyByIdHandler(DocumentGDocPCorrectionPolicyService s)
    : IRequestHandler<GetGDocPCorrectionPolicyByIdQuery, Response<GDocPCorrectionPolicyModel>>
{
    public Task<Response<GDocPCorrectionPolicyModel>> Handle(GetGDocPCorrectionPolicyByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class RecordGDocPCorrectionHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<RecordGDocPCorrectionCommand, Response<GDocPCorrectionRecordModel>>
{
    public Task<Response<GDocPCorrectionRecordModel>> Handle(RecordGDocPCorrectionCommand r, CancellationToken ct) =>
        s.RecordCorrectionAsync(r.Input, r.CorrelationId, ct);
}

public sealed class ReviewGDocPCorrectionHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<ReviewGDocPCorrectionCommand, Response<GDocPCorrectionRecordModel>>
{
    public Task<Response<GDocPCorrectionRecordModel>> Handle(ReviewGDocPCorrectionCommand r, CancellationToken ct) =>
        s.ReviewAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RejectGDocPCorrectionHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<RejectGDocPCorrectionCommand, Response<GDocPCorrectionRecordModel>>
{
    public Task<Response<GDocPCorrectionRecordModel>> Handle(RejectGDocPCorrectionCommand r, CancellationToken ct) =>
        s.RejectAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetGDocPCorrectionsHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<GetGDocPCorrectionsQuery, Response<IReadOnlyList<GDocPCorrectionRecordModel>>>
{
    public Task<Response<IReadOnlyList<GDocPCorrectionRecordModel>>> Handle(GetGDocPCorrectionsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetGDocPCorrectionByIdHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<GetGDocPCorrectionByIdQuery, Response<GDocPCorrectionRecordModel>>
{
    public Task<Response<GDocPCorrectionRecordModel>> Handle(GetGDocPCorrectionByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetGDocPCorrectionsBySubjectHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<GetGDocPCorrectionsBySubjectQuery, Response<IReadOnlyList<GDocPCorrectionRecordModel>>>
{
    public Task<Response<IReadOnlyList<GDocPCorrectionRecordModel>>> Handle(GetGDocPCorrectionsBySubjectQuery r, CancellationToken ct) =>
        s.GetBySubjectAsync(r.SubjectType, r.SubjectId, r.CorrelationId, ct);
}

public sealed class GetGDocPCorrectionReviewsHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<GetGDocPCorrectionReviewsQuery, Response<IReadOnlyList<GDocPCorrectionReviewModel>>>
{
    public Task<Response<IReadOnlyList<GDocPCorrectionReviewModel>>> Handle(GetGDocPCorrectionReviewsQuery r, CancellationToken ct) =>
        s.GetReviewsAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetPendingGDocPCorrectionReviewsHandler(DocumentGDocPCorrectionService s)
    : IRequestHandler<GetPendingGDocPCorrectionReviewsQuery, Response<IReadOnlyList<GDocPCorrectionRecordModel>>>
{
    public Task<Response<IReadOnlyList<GDocPCorrectionRecordModel>>> Handle(GetPendingGDocPCorrectionReviewsQuery r, CancellationToken ct) =>
        s.GetPendingReviewAsync(r.CorrelationId, ct);
}
