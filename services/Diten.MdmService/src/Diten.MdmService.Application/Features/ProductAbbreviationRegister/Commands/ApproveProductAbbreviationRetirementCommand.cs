using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record ApproveProductAbbreviationRetirementCommand(
    Guid RegisterEntryId,
    int ExpectedVersion,
    string RetirementRequestId,
    string IdempotencyKey,
    string? Reason = null)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>;
