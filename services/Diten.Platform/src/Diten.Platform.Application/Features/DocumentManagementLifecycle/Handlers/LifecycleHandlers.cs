using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Commands;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Queries;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle.Handlers;

// MOD-0029-FU08 — thin MediatR handlers delegating to DocumentLifecycleService.

public sealed class TransitionDocumentLifecycleHandler(DocumentLifecycleService service)
    : IRequestHandler<TransitionDocumentLifecycleCommand, Response<LifecycleStateModel>>
{
    public Task<Response<LifecycleStateModel>> Handle(TransitionDocumentLifecycleCommand request, CancellationToken ct) =>
        service.TransitionAsync(request.RegisterEntryId, request.Input, request.CorrelationId, ct);
}

public sealed class GetLifecycleStateHandler(DocumentLifecycleService service)
    : IRequestHandler<GetLifecycleStateQuery, Response<LifecycleStateModel>>
{
    public Task<Response<LifecycleStateModel>> Handle(GetLifecycleStateQuery request, CancellationToken ct) =>
        service.GetStateAsync(request.RegisterEntryId, request.CorrelationId, ct);
}

public sealed class GetLifecycleTransitionsHandler(DocumentLifecycleService service)
    : IRequestHandler<GetLifecycleTransitionsQuery, Response<IReadOnlyList<LifecycleTransitionRecordModel>>>
{
    public Task<Response<IReadOnlyList<LifecycleTransitionRecordModel>>> Handle(GetLifecycleTransitionsQuery request, CancellationToken ct) =>
        service.GetTransitionsAsync(request.RegisterEntryId, request.CorrelationId, ct);
}
