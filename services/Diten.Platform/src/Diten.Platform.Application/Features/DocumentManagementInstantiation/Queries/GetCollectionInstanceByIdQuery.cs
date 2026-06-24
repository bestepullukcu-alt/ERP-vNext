using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;

public sealed record GetCollectionInstanceByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<CollectionInstanceDetailModel>>;
