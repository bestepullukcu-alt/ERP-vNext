using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Commands;

public sealed record UnlinkParentAccountCommand(Guid AccountId) : IRequest<Response<bool>>;
