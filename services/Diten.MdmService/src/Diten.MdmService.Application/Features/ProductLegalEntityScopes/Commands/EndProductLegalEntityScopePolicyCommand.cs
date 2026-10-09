using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;

public sealed record EndProductLegalEntityScopePolicyCommand(
    Guid GlobalProductId,
    Guid CommandId,
    ProductLegalEntityScopeModels.EndPolicyRequest Request)
    : IRequest<Response<ProductLegalEntityScopeModels.PolicyDto>>, IProductLegalEntityScopeInventoryMutation;
