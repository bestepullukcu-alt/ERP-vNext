using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.QueryHandlers;

public sealed class GetTemplateMasterAdoptionImpactHandler(TemplateMasterService service)
    : IRequestHandler<GetTemplateMasterAdoptionImpactQuery, Response<TemplateMasterAdoptionImpactModel>>
{
    public Task<Response<TemplateMasterAdoptionImpactModel>> Handle(GetTemplateMasterAdoptionImpactQuery request, CancellationToken ct) =>
        service.GetAdoptionImpactAsync(request.TemplateMasterId, request.CorrelationId, ct);
}
