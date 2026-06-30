using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.CommandHandlers;

public sealed class CreateTemplateMasterHandler(TemplateMasterService service)
    : IRequestHandler<CreateTemplateMasterCommand, Response<TemplateMasterDetailModel>>
{
    public Task<Response<TemplateMasterDetailModel>> Handle(CreateTemplateMasterCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.CreateAsync(request.Input, request.CorrelationId, ct);
    }
}
