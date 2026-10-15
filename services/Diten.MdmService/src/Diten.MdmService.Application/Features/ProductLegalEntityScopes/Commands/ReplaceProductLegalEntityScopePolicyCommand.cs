using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;

public sealed record ReplaceProductLegalEntityScopePolicyCommand(
    Guid GlobalProductId,
    Guid CommandId,
    ProductLegalEntityScopeModels.ReplacePolicyRequest Request)
    : IRequest<Response<ProductLegalEntityScopeModels.PolicyDto>>, IProductLegalEntityScopeInventoryMutation;
