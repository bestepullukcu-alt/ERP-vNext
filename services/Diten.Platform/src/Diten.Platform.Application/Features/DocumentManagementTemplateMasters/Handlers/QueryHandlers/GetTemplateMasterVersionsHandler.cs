using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.QueryHandlers;

public sealed class GetTemplateMasterVersionsHandler(TemplateMasterService service)
    : IRequestHandler<GetTemplateMasterVersionsQuery, Response<IReadOnlyList<TemplateMasterVersionModel>>>
{
    public Task<Response<IReadOnlyList<TemplateMasterVersionModel>>> Handle(GetTemplateMasterVersionsQuery request, CancellationToken ct) =>
        service.GetVersionsAsync(request.TemplateMasterId, request.CorrelationId, ct);
}
