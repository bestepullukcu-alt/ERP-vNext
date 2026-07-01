using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;

public sealed record DownloadTemplateMasterVersionQuery(Guid TemplateMasterId, Guid VersionId, string CorrelationId)
    : IRequest<Response<DocumentDownloadResult>>;
