using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record UpdateBankBranchCommand(Guid BranchId, BankBranchWriteRequest Request, int ExpectedVersion, Guid Actor)
    : IRequest<Response<BankBranchDto>>;
