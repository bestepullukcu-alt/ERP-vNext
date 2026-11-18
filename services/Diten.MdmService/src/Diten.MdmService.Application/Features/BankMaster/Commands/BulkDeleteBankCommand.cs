using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BankMaster.Commands;

/// <summary>All or none: one id that cannot be deleted makes the whole request 409 and nothing is deleted.</summary>
public sealed record BulkDeleteBankCommand(BulkDeleteBanksRequest Request, Guid Actor) : IRequest<Response<NoContent>>;
