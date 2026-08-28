using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Commands;

public sealed record LinkParentAccountCommand(Guid AccountId, Guid ParentAccountId) : IRequest<Response<bool>>;
