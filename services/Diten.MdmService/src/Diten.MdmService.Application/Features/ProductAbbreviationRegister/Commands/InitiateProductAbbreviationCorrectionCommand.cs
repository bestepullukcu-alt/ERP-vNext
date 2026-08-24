using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;

public sealed record InitiateProductAbbreviationCorrectionCommand(
    Guid ActiveRegisterEntryId,
    int ExpectedVersion,
    string ReplacementAbbreviation,
    string IdempotencyKey,
    string Reason)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>;
