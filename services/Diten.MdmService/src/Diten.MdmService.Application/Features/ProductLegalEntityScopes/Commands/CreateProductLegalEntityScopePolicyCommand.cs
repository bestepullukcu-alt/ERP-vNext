using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;

public sealed record CreateProductLegalEntityScopePolicyCommand(
    Guid GlobalProductId,
    Guid CommandId,
    ProductLegalEntityScopeModels.CreatePolicyRequest Request)
    : IRequest<Response<ProductLegalEntityScopeModels.PolicyDto>>, IProductLegalEntityScopeInventoryMutation;
