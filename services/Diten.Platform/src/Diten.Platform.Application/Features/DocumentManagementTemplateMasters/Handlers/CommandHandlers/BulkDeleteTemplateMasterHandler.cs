using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.CommandHandlers;

public sealed class BulkDeleteTemplateMasterHandler(TemplateMasterService service)
    : IRequestHandler<BulkDeleteTemplateMasterCommand, Response<int>>
{
    public Task<Response<int>> Handle(BulkDeleteTemplateMasterCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.BulkDeleteAsync(request.Ids, request.CorrelationId, ct);
    }
}
