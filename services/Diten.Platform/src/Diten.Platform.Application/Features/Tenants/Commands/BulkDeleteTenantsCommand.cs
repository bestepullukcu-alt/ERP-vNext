using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record BulkDeleteTenantsCommand(IReadOnlyList<Guid> Ids) : IRequest<Response<NoContent>>;
