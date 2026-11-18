using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

public sealed record ActivateBankCommand(Guid BankId, int ExpectedVersion, Guid Actor) : IRequest<Response<BankDetailDto>>;
