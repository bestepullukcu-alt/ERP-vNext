using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record RequestProductAbbreviationAllocationCommand(
    Guid GlobalProductId,
    string Abbreviation,
    string IdempotencyKey)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>, global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation;
