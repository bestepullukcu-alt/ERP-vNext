using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class DeleteTenantAdminUserCommandHandler : IRequestHandler<DeleteTenantAdminUserCommand, Response<NoContent>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IQuotaService _quotaService;

    public DeleteTenantAdminUserCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IQuotaService quotaService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _quotaService = quotaService;
    }

    public async Task<Response<NoContent>> Handle(DeleteTenantAdminUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<NoContent>.Fail("Tenant not found.", 404);
        }

        var initialAdminAdded = TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        if (initialAdminAdded)
        {
            tenant.ActiveUserCount = TenantAdminUserSupport.CountUsersQuotaUsage(tenant);
        }

        var user = tenant.AdminUsers.FirstOrDefault(item => item.Id == request.AdminUserId);
        if (user == null)
        {
            return Response<NoContent>.Fail("Admin user not found.", 404);
        }

        var releasesUsersQuota = TenantAdminUserSupport.CountsTowardsUsersQuota(user);
        tenant.AdminUsers.Remove(user);
        tenant.ActiveUserCount = TenantAdminUserSupport.CountUsersQuotaUsage(tenant);
        TenantAdminUserSupport.AddActivity(tenant, "tenant.admin_user.deleted", $"Admin user '{user.Email}' deleted.", _currentUser.ActorName, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(tenant, cancellationToken);

        if (releasesUsersQuota)
        {
            await _quotaService.ReleaseAsync(new ReleaseQuotaRequest(
                tenant.Id,
                QuotaKeys.UsersMax,
                1,
                "TenantAdminUserDelete",
                $"tenant-admin-user:{user.Id}:delete",
                user.Id.ToString(),
                "Tenant admin user deleted.",
                _currentUser.ActorName,
                Guid.NewGuid().ToString()), cancellationToken);
        }

        return Response<NoContent>.Success(204);
    }
}
