using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.QueryHandlers;

public sealed class GetTemplateVariantListHandler(TemplateVariantService service)
    : IRequestHandler<GetTemplateVariantListQuery, Response<IReadOnlyList<TemplateVariantListItemModel>>>
{
    public Task<Response<IReadOnlyList<TemplateVariantListItemModel>>> Handle(GetTemplateVariantListQuery request, CancellationToken ct) =>
        service.ListAsync(request.Filter, request.CorrelationId, ct);
}
