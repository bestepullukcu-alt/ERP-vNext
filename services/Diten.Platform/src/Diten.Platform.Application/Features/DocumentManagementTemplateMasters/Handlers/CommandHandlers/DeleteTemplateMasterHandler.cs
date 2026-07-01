using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.CommandHandlers;

public sealed class DeleteTemplateMasterHandler(TemplateMasterService service)
    : IRequestHandler<DeleteTemplateMasterCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(DeleteTemplateMasterCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.DeleteAsync(request.TemplateMasterId, request.CorrelationId, ct);
    }
}
