using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementRetention.Commands;
using Diten.Platform.Application.Features.DocumentManagementRetention.Queries;
using Diten.Platform.Application.Features.DocumentManagementRetention.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Handlers;

// MOD-0029-FU15 — thin MediatR handlers delegating to the retention policy / evaluator / legal hold /
// disposition services.

public sealed class CreateRetentionPolicyHandler(DocumentRetentionPolicyService s)
    : IRequestHandler<CreateRetentionPolicyCommand, Response<RetentionPolicyModel>>
{
    public Task<Response<RetentionPolicyModel>> Handle(CreateRetentionPolicyCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class UpdateRetentionPolicyHandler(DocumentRetentionPolicyService s)
    : IRequestHandler<UpdateRetentionPolicyCommand, Response<RetentionPolicyModel>>
{
    public Task<Response<RetentionPolicyModel>> Handle(UpdateRetentionPolicyCommand r, CancellationToken ct) =>
        s.UpdateAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class ActivateRetentionPolicyHandler(DocumentRetentionPolicyService s)
    : IRequestHandler<ActivateRetentionPolicyCommand, Response<RetentionPolicyModel>>
{
    public Task<Response<RetentionPolicyModel>> Handle(ActivateRetentionPolicyCommand r, CancellationToken ct) =>
        s.ActivateAsync(r.Id, r.CorrelationId, ct);
}

public sealed class RetireRetentionPolicyHandler(DocumentRetentionPolicyService s)
    : IRequestHandler<RetireRetentionPolicyCommand, Response<RetentionPolicyModel>>
{
    public Task<Response<RetentionPolicyModel>> Handle(RetireRetentionPolicyCommand r, CancellationToken ct) =>
        s.RetireAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetRetentionPoliciesHandler(DocumentRetentionPolicyService s)
    : IRequestHandler<GetRetentionPoliciesQuery, Response<IReadOnlyList<RetentionPolicyModel>>>
{
    public Task<Response<IReadOnlyList<RetentionPolicyModel>>> Handle(GetRetentionPoliciesQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetRetentionPolicyByIdHandler(DocumentRetentionPolicyService s)
    : IRequestHandler<GetRetentionPolicyByIdQuery, Response<RetentionPolicyModel>>
{
    public Task<Response<RetentionPolicyModel>> Handle(GetRetentionPolicyByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class EvaluateRetentionSubjectHandler(DocumentRetentionEvaluator s)
    : IRequestHandler<EvaluateRetentionSubjectCommand, Response<RetentionSubjectModel>>
{
    public Task<Response<RetentionSubjectModel>> Handle(EvaluateRetentionSubjectCommand r, CancellationToken ct) =>
        s.EvaluateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class GetRetentionSubjectHandler(DocumentRetentionEvaluator s)
    : IRequestHandler<GetRetentionSubjectQuery, Response<RetentionSubjectModel>>
{
    public Task<Response<RetentionSubjectModel>> Handle(GetRetentionSubjectQuery r, CancellationToken ct) =>
        s.GetSubjectAsync(r.SubjectType, r.SubjectId, r.CorrelationId, ct);
}

public sealed class GetEligibleRetentionSubjectsHandler(DocumentRetentionEvaluator s)
    : IRequestHandler<GetEligibleRetentionSubjectsQuery, Response<IReadOnlyList<RetentionSubjectModel>>>
{
    public Task<Response<IReadOnlyList<RetentionSubjectModel>>> Handle(GetEligibleRetentionSubjectsQuery r, CancellationToken ct) =>
        s.GetEligibleAsync(r.CorrelationId, ct);
}

public sealed class CreateLegalHoldHandler(DocumentLegalHoldService s)
    : IRequestHandler<CreateLegalHoldCommand, Response<LegalHoldModel>>
{
    public Task<Response<LegalHoldModel>> Handle(CreateLegalHoldCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class ActivateLegalHoldHandler(DocumentLegalHoldService s)
    : IRequestHandler<ActivateLegalHoldCommand, Response<LegalHoldModel>>
{
    public Task<Response<LegalHoldModel>> Handle(ActivateLegalHoldCommand r, CancellationToken ct) =>
        s.ActivateAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class ReleaseLegalHoldHandler(DocumentLegalHoldService s)
    : IRequestHandler<ReleaseLegalHoldCommand, Response<LegalHoldModel>>
{
    public Task<Response<LegalHoldModel>> Handle(ReleaseLegalHoldCommand r, CancellationToken ct) =>
        s.ReleaseAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class AddLegalHoldSubjectHandler(DocumentLegalHoldService s)
    : IRequestHandler<AddLegalHoldSubjectCommand, Response<LegalHoldSubjectModel>>
{
    public Task<Response<LegalHoldSubjectModel>> Handle(AddLegalHoldSubjectCommand r, CancellationToken ct) =>
        s.AddSubjectAsync(r.HoldId, r.SubjectType, r.SubjectId, r.RegisterEntryId, r.CorrelationId, ct);
}

public sealed class GetLegalHoldsHandler(DocumentLegalHoldService s)
    : IRequestHandler<GetLegalHoldsQuery, Response<IReadOnlyList<LegalHoldModel>>>
{
    public Task<Response<IReadOnlyList<LegalHoldModel>>> Handle(GetLegalHoldsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetLegalHoldByIdHandler(DocumentLegalHoldService s)
    : IRequestHandler<GetLegalHoldByIdQuery, Response<LegalHoldModel>>
{
    public Task<Response<LegalHoldModel>> Handle(GetLegalHoldByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}

public sealed class GetLegalHoldSubjectsHandler(DocumentLegalHoldService s)
    : IRequestHandler<GetLegalHoldSubjectsQuery, Response<IReadOnlyList<LegalHoldSubjectModel>>>
{
    public Task<Response<IReadOnlyList<LegalHoldSubjectModel>>> Handle(GetLegalHoldSubjectsQuery r, CancellationToken ct) =>
        s.GetSubjectsAsync(r.Id, r.CorrelationId, ct);
}

public sealed class CreateDispositionRequestHandler(DocumentDispositionService s)
    : IRequestHandler<CreateDispositionRequestCommand, Response<DispositionRequestModel>>
{
    public Task<Response<DispositionRequestModel>> Handle(CreateDispositionRequestCommand r, CancellationToken ct) =>
        s.CreateAsync(r.Input, r.CorrelationId, ct);
}

public sealed class SubmitDispositionRequestHandler(DocumentDispositionService s)
    : IRequestHandler<SubmitDispositionRequestCommand, Response<DispositionRequestModel>>
{
    public Task<Response<DispositionRequestModel>> Handle(SubmitDispositionRequestCommand r, CancellationToken ct) =>
        s.SubmitAsync(r.Id, r.CorrelationId, ct);
}

public sealed class ApproveDispositionRequestHandler(DocumentDispositionService s)
    : IRequestHandler<ApproveDispositionRequestCommand, Response<DispositionRequestModel>>
{
    public Task<Response<DispositionRequestModel>> Handle(ApproveDispositionRequestCommand r, CancellationToken ct) =>
        s.ApproveAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class RejectDispositionRequestHandler(DocumentDispositionService s)
    : IRequestHandler<RejectDispositionRequestCommand, Response<DispositionRequestModel>>
{
    public Task<Response<DispositionRequestModel>> Handle(RejectDispositionRequestCommand r, CancellationToken ct) =>
        s.RejectAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class ExecuteDispositionMarkerHandler(DocumentDispositionService s)
    : IRequestHandler<ExecuteDispositionMarkerCommand, Response<DispositionRequestModel>>
{
    public Task<Response<DispositionRequestModel>> Handle(ExecuteDispositionMarkerCommand r, CancellationToken ct) =>
        s.ExecuteMarkerAsync(r.Id, r.Input, r.CorrelationId, ct);
}

public sealed class GetDispositionRequestsHandler(DocumentDispositionService s)
    : IRequestHandler<GetDispositionRequestsQuery, Response<IReadOnlyList<DispositionRequestModel>>>
{
    public Task<Response<IReadOnlyList<DispositionRequestModel>>> Handle(GetDispositionRequestsQuery r, CancellationToken ct) =>
        s.ListAsync(r.CorrelationId, ct);
}

public sealed class GetDispositionRequestByIdHandler(DocumentDispositionService s)
    : IRequestHandler<GetDispositionRequestByIdQuery, Response<DispositionRequestModel>>
{
    public Task<Response<DispositionRequestModel>> Handle(GetDispositionRequestByIdQuery r, CancellationToken ct) =>
        s.GetAsync(r.Id, r.CorrelationId, ct);
}
