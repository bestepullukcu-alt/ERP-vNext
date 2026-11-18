using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record DeactivateBankBranchCommand(Guid BranchId, DeactivateRequest Request, int ExpectedVersion, Guid Actor)
    : IRequest<Response<BankBranchDto>>;
