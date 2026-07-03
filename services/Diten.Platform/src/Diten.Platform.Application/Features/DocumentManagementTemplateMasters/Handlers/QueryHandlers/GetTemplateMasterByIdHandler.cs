using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.QueryHandlers;

public sealed class GetTemplateMasterByIdHandler(TemplateMasterService service)
    : IRequestHandler<GetTemplateMasterByIdQuery, Response<TemplateMasterDetailModel>>
{
    public Task<Response<TemplateMasterDetailModel>> Handle(GetTemplateMasterByIdQuery request, CancellationToken ct) =>
        service.GetDetailAsync(request.TemplateMasterId, request.CorrelationId, ct);
}
