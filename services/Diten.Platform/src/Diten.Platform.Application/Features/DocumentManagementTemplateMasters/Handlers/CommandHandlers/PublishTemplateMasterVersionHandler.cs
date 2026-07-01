using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.CommandHandlers;

public sealed class PublishTemplateMasterVersionHandler(TemplateMasterService service)
    : IRequestHandler<PublishTemplateMasterVersionCommand, Response<TemplateMasterVersionModel>>
{
    public Task<Response<TemplateMasterVersionModel>> Handle(PublishTemplateMasterVersionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.PublishVersionAsync(request.TemplateMasterId, request.File, request.ChangeSummary, request.AllowUnchanged, request.CorrelationId, ct);
    }
}
