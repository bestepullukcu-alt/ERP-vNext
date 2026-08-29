using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Commands;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Queries;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Handlers;

// MOD-0029-FU12 — thin MediatR handlers delegating to DocumentPeriodicReviewService.

public sealed class InitiatePeriodicReviewHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<InitiatePeriodicReviewCommand, Response<PeriodicReviewModel>>
{
    public Task<Response<PeriodicReviewModel>> Handle(InitiatePeriodicReviewCommand request, CancellationToken ct) =>
        service.InitiateAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class CompletePeriodicReviewHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<CompletePeriodicReviewCommand, Response<PeriodicReviewModel>>
{
    public Task<Response<PeriodicReviewModel>> Handle(CompletePeriodicReviewCommand request, CancellationToken ct) =>
        service.CompleteAsync(request.RegisterEntryId, request.ReviewId, request.Input, request.CorrelationId, ct);
}

public sealed class RequestPeriodicReviewExtensionHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<RequestPeriodicReviewExtensionCommand, Response<PeriodicReviewExtensionModel>>
{
    public Task<Response<PeriodicReviewExtensionModel>> Handle(RequestPeriodicReviewExtensionCommand request, CancellationToken ct) =>
        service.RequestExtensionAsync(request.RegisterEntryId, request.ReviewId, request.Input, request.CorrelationId, ct);
}

public sealed class ApprovePeriodicReviewExtensionHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<ApprovePeriodicReviewExtensionCommand, Response<PeriodicReviewExtensionModel>>
{
    public Task<Response<PeriodicReviewExtensionModel>> Handle(ApprovePeriodicReviewExtensionCommand request, CancellationToken ct) =>
        service.ApproveExtensionAsync(request.RegisterEntryId, request.ReviewId, request.ExtensionId, request.Input, request.CorrelationId, ct);
}

public sealed class RejectPeriodicReviewExtensionHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<RejectPeriodicReviewExtensionCommand, Response<PeriodicReviewExtensionModel>>
{
    public Task<Response<PeriodicReviewExtensionModel>> Handle(RejectPeriodicReviewExtensionCommand request, CancellationToken ct) =>
        service.RejectExtensionAsync(request.RegisterEntryId, request.ReviewId, request.ExtensionId, request.Input, request.CorrelationId, ct);
}

public sealed class EvaluatePeriodicReviewOverdueHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<EvaluatePeriodicReviewOverdueCommand, Response<PeriodicReviewScheduleModel>>
{
    public Task<Response<PeriodicReviewScheduleModel>> Handle(EvaluatePeriodicReviewOverdueCommand request, CancellationToken ct) =>
        service.EvaluateOverdueAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetPeriodicReviewScheduleHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<GetPeriodicReviewScheduleQuery, Response<PeriodicReviewScheduleModel>>
{
    public Task<Response<PeriodicReviewScheduleModel>> Handle(GetPeriodicReviewScheduleQuery request, CancellationToken ct) =>
        service.GetScheduleAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetPeriodicReviewEscalationsHandler(DocumentPeriodicReviewService service)
    : IRequestHandler<GetPeriodicReviewEscalationsQuery, Response<IReadOnlyList<PeriodicReviewEscalationModel>>>
{
    public Task<Response<IReadOnlyList<PeriodicReviewEscalationModel>>> Handle(GetPeriodicReviewEscalationsQuery request, CancellationToken ct) =>
        service.GetEscalationsAsync(request.RegisterEntryId, request.CorrelationId, ct);
}
