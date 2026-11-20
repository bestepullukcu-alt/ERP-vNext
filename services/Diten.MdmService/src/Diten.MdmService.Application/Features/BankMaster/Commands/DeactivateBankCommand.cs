using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record DeactivateBankCommand(Guid BankId, DeactivateRequest Request, int ExpectedVersion, Guid Actor)
    : IRequest<Response<BankDetailDto>>;
