using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;

public sealed record GetTemplateMasterAdoptionImpactQuery(Guid TemplateMasterId, string CorrelationId)
    : IRequest<Response<TemplateMasterAdoptionImpactModel>>;
