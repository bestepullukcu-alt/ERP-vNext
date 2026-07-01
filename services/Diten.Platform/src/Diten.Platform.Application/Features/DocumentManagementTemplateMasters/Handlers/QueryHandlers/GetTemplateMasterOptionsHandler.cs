using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.QueryHandlers;

public sealed class GetTemplateMasterOptionsHandler(TemplateMasterService service)
    : IRequestHandler<GetTemplateMasterOptionsQuery, Response<IReadOnlyList<TemplateMasterOptionModel>>>
{
    public Task<Response<IReadOnlyList<TemplateMasterOptionModel>>> Handle(GetTemplateMasterOptionsQuery request, CancellationToken ct) =>
        service.GetOptionsAsync(request.CorrelationId, ct);
}
