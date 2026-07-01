using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Handlers.CommandHandlers;

public sealed class RebaseTemplateVariantHandler(TemplateVariantService service)
    : IRequestHandler<RebaseTemplateVariantCommand, Response<TemplateVariantDetailModel>>
{
    public Task<Response<TemplateVariantDetailModel>> Handle(RebaseTemplateVariantCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.RebaseAsync(request.Id, request.Input, request.CorrelationId, ct);
    }
}
