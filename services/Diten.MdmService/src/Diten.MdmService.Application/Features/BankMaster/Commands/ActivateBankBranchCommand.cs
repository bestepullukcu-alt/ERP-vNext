using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record ActivateBankBranchCommand(Guid BranchId, int ExpectedVersion, Guid Actor) : IRequest<Response<BankBranchDto>>;
