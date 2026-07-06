using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

// FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — flips the matching TenantAdminUser Invited → Active on the
// AuthService activation callback. Idempotent + fail-safe: unknown tenant / no matching admin / already-Active all
// return 204 (the caller is best-effort and non-admin tenant_users are not tracked here).
public sealed class ActivateTenantAdminUserCommandHandler : IRequestHandler<ActivateTenantAdminUserCommand, Response<NoContent>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ILogger<ActivateTenantAdminUserCommandHandler> _logger;

    public ActivateTenantAdminUserCommandHandler(
        ITenantRegistryRepository repository,
        ILogger<ActivateTenantAdminUserCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(ActivateTenantAdminUserCommand request, CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim();
        if (request.TenantId == Guid.Empty || email.Length == 0)
        {
            return Response<NoContent>.Success(204); // nothing to match — best-effort no-op
        }

        var tenant = await _repository.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
        {
            return Response<NoContent>.Success(204);
        }

        var admin = tenant.AdminUsers.FirstOrDefault(u =>
            string.Equals(u.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase));
        if (admin is null)
        {
            return Response<NoContent>.Success(204); // non-admin tenant_users are not tracked in Platform
        }

        if (admin.Status == TenantAdminUserStatus.Active)
        {
            return Response<NoContent>.Success(204); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        admin.Status = TenantAdminUserStatus.Active;
        admin.ActivatedAt = now;
        admin.UpdatedAt = now;
        await _repository.UpdateAsync(tenant, ct);

        _logger.LogInformation(
            "Tenant admin activated (Invited → Active). TenantId={TenantId} AdminUserId={AdminUserId}",
            request.TenantId,
            admin.Id);

        return Response<NoContent>.Success(204);
    }
}
