using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record RejectProductAbbreviationRetirementCommand(
    Guid RegisterEntryId,
    int ExpectedVersion,
    string RetirementRequestId,
    string IdempotencyKey,
    string Reason)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>;
