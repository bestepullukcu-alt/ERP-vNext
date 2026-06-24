using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;

public sealed record GetCollectionInstancesQuery(Guid? CompanyId, Guid? BaselineReleaseId, string? InstanceToken, string CorrelationId)
    : IRequest<Response<IReadOnlyList<CollectionInstanceListItemModel>>>;
