using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;

public sealed record GetCorporateCollectionInstanceQuery(
    Guid CollectionInstanceId,
    string CorrelationId) : IRequest<Response<CorporateCollectionInstanceModel>>;
