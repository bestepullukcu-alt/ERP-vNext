using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Commands;

public sealed record RetryCorporateCollectionProvisioningCommand(
    Guid OperationId,
    string CorrelationId) : IRequest<Response<CorporateCollectionProvisioningResult>>;
