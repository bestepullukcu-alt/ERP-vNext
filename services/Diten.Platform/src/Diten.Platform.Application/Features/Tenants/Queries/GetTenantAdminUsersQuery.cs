using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Queries;

public sealed record GetTenantAdminUsersQuery(Guid TenantId) : IRequest<IReadOnlyList<TenantAdminUserDto>?>;
