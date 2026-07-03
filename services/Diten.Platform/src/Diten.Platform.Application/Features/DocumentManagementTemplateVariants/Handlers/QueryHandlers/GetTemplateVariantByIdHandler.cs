using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.QueryHandlers;

public sealed class GetTemplateVariantByIdHandler(TemplateVariantService service)
    : IRequestHandler<GetTemplateVariantByIdQuery, Response<TemplateVariantDetailModel>>
{
    public Task<Response<TemplateVariantDetailModel>> Handle(GetTemplateVariantByIdQuery request, CancellationToken ct) =>
        service.GetDetailAsync(request.Id, request.CorrelationId, ct);
}
