using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record RejectProductAbbreviationAllocationCommand(
    Guid RegisterEntryId,
    int ExpectedVersion,
    string IdempotencyKey,
    string Reason)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>, global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation;
