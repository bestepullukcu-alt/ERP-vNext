using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record InviteTenantAdminUserCommand(
    Guid TenantId,
    Guid AdminUserId) : IRequest<Response<TenantAdminUserDto>>;
