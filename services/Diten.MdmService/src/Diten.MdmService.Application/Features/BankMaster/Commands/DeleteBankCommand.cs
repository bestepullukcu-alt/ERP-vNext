using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

/// <summary>Soft delete of a Draft that was never activated.</summary>
public sealed record DeleteBankCommand(Guid BankId, Guid Actor) : IRequest<Response<NoContent>>;
