using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Commands;

public sealed record BulkDeleteAccountCommand(IReadOnlyList<Guid> Ids) : IRequest<Response<int>>;
