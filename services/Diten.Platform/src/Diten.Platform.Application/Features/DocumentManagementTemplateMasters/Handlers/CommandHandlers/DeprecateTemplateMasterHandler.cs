using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.CommandHandlers;

public sealed class DeprecateTemplateMasterHandler(TemplateMasterService service)
    : IRequestHandler<DeprecateTemplateMasterCommand, Response<TemplateMasterDetailModel>>
{
    public Task<Response<TemplateMasterDetailModel>> Handle(DeprecateTemplateMasterCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.DeprecateAsync(request.TemplateMasterId, request.Input, request.CorrelationId, ct);
    }
}
