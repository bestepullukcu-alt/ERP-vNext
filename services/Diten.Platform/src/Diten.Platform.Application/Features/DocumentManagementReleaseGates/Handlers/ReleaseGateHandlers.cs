using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Commands;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Queries;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates.Handlers;

// MOD-0029-FU10 — thin MediatR handlers delegating to DocumentReleaseGateEvaluator.

public sealed class EvaluateReleaseGatesHandler(DocumentReleaseGateEvaluator evaluator)
    : IRequestHandler<EvaluateReleaseGatesCommand, Response<ReleaseGateEvaluationModel>>
{
    public Task<Response<ReleaseGateEvaluationModel>> Handle(EvaluateReleaseGatesCommand request, CancellationToken ct) =>
        evaluator.EvaluateAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class RecordReleaseGateEvidenceHandler(DocumentReleaseGateEvaluator evaluator)
    : IRequestHandler<RecordReleaseGateEvidenceCommand, Response<ReleaseGateEvaluationModel>>
{
    public Task<Response<ReleaseGateEvaluationModel>> Handle(RecordReleaseGateEvidenceCommand request, CancellationToken ct) =>
        evaluator.RecordEvidenceAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class GetLatestReleaseGateEvaluationHandler(DocumentReleaseGateEvaluator evaluator)
    : IRequestHandler<GetLatestReleaseGateEvaluationQuery, Response<ReleaseGateEvaluationModel>>
{
    public Task<Response<ReleaseGateEvaluationModel>> Handle(GetLatestReleaseGateEvaluationQuery request, CancellationToken ct) =>
        evaluator.GetLatestAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetReleaseGateHistoryHandler(DocumentReleaseGateEvaluator evaluator)
    : IRequestHandler<GetReleaseGateHistoryQuery, Response<IReadOnlyList<ReleaseGateEvaluationModel>>>
{
    public Task<Response<IReadOnlyList<ReleaseGateEvaluationModel>>> Handle(GetReleaseGateHistoryQuery request, CancellationToken ct) =>
        evaluator.GetHistoryAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetReleaseReadinessHandler(DocumentReleaseGateEvaluator evaluator)
    : IRequestHandler<GetReleaseReadinessQuery, Response<ReleaseGateEvaluationModel>>
{
    public Task<Response<ReleaseGateEvaluationModel>> Handle(GetReleaseReadinessQuery request, CancellationToken ct) =>
        evaluator.GetReadinessAsync(request.RegisterEntryId, request.CorrelationId, ct);
}
