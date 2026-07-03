using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.CommandHandlers;

public sealed class CreateTemplateVariantHandler(TemplateVariantService service)
    : IRequestHandler<CreateTemplateVariantCommand, Response<TemplateVariantDetailModel>>
{
    public Task<Response<TemplateVariantDetailModel>> Handle(CreateTemplateVariantCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateAsync(request.Input, request.CorrelationId, ct);
    }
}
