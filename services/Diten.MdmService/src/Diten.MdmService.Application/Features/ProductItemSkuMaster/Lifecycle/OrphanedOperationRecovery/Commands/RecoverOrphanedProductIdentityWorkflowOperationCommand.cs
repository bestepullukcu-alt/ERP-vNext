using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Commands;

public static class ProductIdentityWorkflowOperationRecoveryPermissions
{
    public const string Recover = "mdm.product-identity.lifecycle-operations.recover";
}

public sealed record RecoverOrphanedProductIdentityWorkflowOperationCommand(
    Guid OperationId,
    Guid CommandId,
    ProductIdentityWorkflowOperationRecoveryRequest Request)
    : IRequest<Response<ProductIdentityWorkflowOperationRecoveryResult>>;
