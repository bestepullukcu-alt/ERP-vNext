using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantAdminUsersQueryHandler : IRequestHandler<GetTenantAdminUsersQuery, IReadOnlyList<TenantAdminUserDto>?>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantAdminUsersQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<TenantAdminUserDto>?> Handle(GetTenantAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var changed = TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        if (changed)
        {
            await _repository.UpdateAsync(tenant, cancellationToken);
        }

        return tenant.AdminUsers
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Email)
            .Select(TenantAdminUserSupport.ToDto)
            .ToList();
    }
}
