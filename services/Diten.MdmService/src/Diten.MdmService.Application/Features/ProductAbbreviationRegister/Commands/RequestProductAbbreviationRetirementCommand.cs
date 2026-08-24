using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record RequestProductAbbreviationRetirementCommand(
    Guid RegisterEntryId,
    int ExpectedVersion,
    string IdempotencyKey,
    string Reason)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>;
