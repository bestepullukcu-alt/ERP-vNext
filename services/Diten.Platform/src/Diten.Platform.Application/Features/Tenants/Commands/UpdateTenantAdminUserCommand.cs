using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantAdminUserCommand(
    Guid TenantId,
    Guid AdminUserId,
    TenantAdminUserUpsertRequest Request) : IRequest<Response<TenantAdminUserDto>>;
