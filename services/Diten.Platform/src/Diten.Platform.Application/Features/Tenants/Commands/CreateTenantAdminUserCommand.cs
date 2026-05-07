using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record CreateTenantAdminUserCommand(
    Guid TenantId,
    TenantAdminUserUpsertRequest Request) : IRequest<Response<TenantAdminUserDto>>;
