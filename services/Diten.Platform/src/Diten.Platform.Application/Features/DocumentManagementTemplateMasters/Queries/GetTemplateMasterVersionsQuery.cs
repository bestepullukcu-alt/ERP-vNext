using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;

public sealed record GetTemplateMasterVersionsQuery(Guid TemplateMasterId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TemplateMasterVersionModel>>>;
