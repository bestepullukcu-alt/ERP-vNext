using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.QueryHandlers;

public sealed class GetTemplateVariantCompareHandler(TemplateVariantService service)
    : IRequestHandler<GetTemplateVariantCompareQuery, Response<TemplateVariantCompareModel>>
{
    public Task<Response<TemplateVariantCompareModel>> Handle(GetTemplateVariantCompareQuery request, CancellationToken ct) =>
        service.CompareAsync(request.Id, request.CorrelationId, ct);
}
