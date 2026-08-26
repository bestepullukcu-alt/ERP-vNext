using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record ApproveProductAbbreviationAllocationCommand(
    Guid RegisterEntryId,
    int ExpectedVersion,
    string IdempotencyKey,
    int? ExpectedFormerVersion = null,
    string? Reason = null)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>;
