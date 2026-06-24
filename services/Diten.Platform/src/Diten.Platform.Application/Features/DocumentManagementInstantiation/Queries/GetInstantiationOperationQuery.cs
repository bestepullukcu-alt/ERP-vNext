using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;

public sealed record GetInstantiationOperationQuery(Guid OperationId, string CorrelationId)
    : IRequest<Response<InstantiationResultModel>>;
