using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.QueryHandlers;

public sealed class GetTemplateMasterListHandler(TemplateMasterService service)
    : IRequestHandler<GetTemplateMasterListQuery, Response<IReadOnlyList<TemplateMasterListItemModel>>>
{
    public Task<Response<IReadOnlyList<TemplateMasterListItemModel>>> Handle(GetTemplateMasterListQuery request, CancellationToken ct) =>
        service.ListAsync(request.Filter, request.CorrelationId, ct);
}
