using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;

public sealed record ListCorporateCollectionInstancesQuery(
    Guid? BaselineReleaseId,
    Guid? CorporateOwnerId,
    string CorrelationId) : IRequest<Response<IReadOnlyList<CorporateCollectionInstanceModel>>>;
