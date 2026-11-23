using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record CreateBankBranchCommand(Guid BankId, BankBranchWriteRequest Request, Guid Actor)
    : IRequest<Response<BankBranchDto>>;
