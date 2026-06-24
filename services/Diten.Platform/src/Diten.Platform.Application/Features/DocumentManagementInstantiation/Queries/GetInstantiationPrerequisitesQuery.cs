using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;

public sealed record GetInstantiationPrerequisitesQuery(string CorrelationId)
    : IRequest<Response<InstantiationPrerequisitesModel>>;
