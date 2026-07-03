using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.QueryHandlers;

public sealed class GetTemplateMasterVariantsHandler(TemplateVariantService service)
    : IRequestHandler<GetTemplateMasterVariantsQuery, Response<IReadOnlyList<TemplateVariantListItemModel>>>
{
    public Task<Response<IReadOnlyList<TemplateVariantListItemModel>>> Handle(GetTemplateMasterVariantsQuery request, CancellationToken ct) =>
        service.GetByMasterAsync(request.TemplateMasterId, request.CorrelationId, ct);
}
