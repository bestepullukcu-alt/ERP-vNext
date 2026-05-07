using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record DeleteTenantAdminUserCommand(
    Guid TenantId,
    Guid AdminUserId) : IRequest<Response<NoContent>>;
