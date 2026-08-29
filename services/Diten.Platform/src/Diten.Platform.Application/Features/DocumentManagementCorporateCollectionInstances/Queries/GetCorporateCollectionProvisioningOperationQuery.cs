using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;

public sealed record GetCorporateCollectionProvisioningOperationQuery(
    Guid OperationId,
    string CorrelationId) : IRequest<Response<CorporateCollectionProvisioningOperationModel>>;
