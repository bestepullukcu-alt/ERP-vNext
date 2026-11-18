using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record DeleteBankBranchCommand(Guid BranchId, Guid Actor) : IRequest<Response<NoContent>>;
