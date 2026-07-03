using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.QueryHandlers;

public sealed class GetTemplateVariantOptionsHandler(TemplateVariantService service)
    : IRequestHandler<GetTemplateVariantOptionsQuery, Response<IReadOnlyList<TemplateVariantOptionModel>>>
{
    public Task<Response<IReadOnlyList<TemplateVariantOptionModel>>> Handle(GetTemplateVariantOptionsQuery request, CancellationToken ct) =>
        service.GetOptionsAsync(request.CorrelationId, ct);
}
