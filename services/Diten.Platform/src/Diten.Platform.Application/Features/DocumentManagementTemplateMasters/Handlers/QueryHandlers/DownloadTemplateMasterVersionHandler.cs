using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Handlers.QueryHandlers;

public sealed class DownloadTemplateMasterVersionHandler(TemplateMasterService service)
    : IRequestHandler<DownloadTemplateMasterVersionQuery, Response<DocumentDownloadResult>>
{
    public Task<Response<DocumentDownloadResult>> Handle(DownloadTemplateMasterVersionQuery request, CancellationToken ct) =>
        service.DownloadVersionAsync(request.TemplateMasterId, request.VersionId, request.CorrelationId, ct);
}
