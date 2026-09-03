using System.IdentityModel.Tokens.Jwt;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public sealed class PlatformTenantWorkflowDefinitionRequestExecutor
    : IPlatformTenantWorkflowDefinitionRequestExecutor
{
    private readonly ITenantRegistryRepository _tenants;
    private readonly ITenantContext _tenantContext;

    public PlatformTenantWorkflowDefinitionRequestExecutor(
        ITenantRegistryRepository tenants,
        ITenantContext tenantContext)
    {
        _tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext context,
        Guid targetTenantId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(failure);

        var actorTypes = context.User.Claims
            .Where(claim => string.Equals(claim.Type, "actor_type", StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
        var subjects = context.User.Claims
            .Where(claim => string.Equals(claim.Type, JwtRegisteredClaimNames.Sub, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return failure(StatusCodes.Status401Unauthorized, "WORKFLOW_ADMIN_UNAUTHENTICATED");
        }

        if (actorTypes.Length != 1
            || !string.Equals(actorTypes[0], "platform_admin", StringComparison.Ordinal)
            || subjects.Length != 1
            || !Guid.TryParseExact(subjects[0], "D", out var subjectId)
            || subjectId == Guid.Empty)
        {
            return failure(StatusCodes.Status403Forbidden, "WORKFLOW_ADMIN_FORBIDDEN");
        }

        if (context.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return failure(StatusCodes.Status400BadRequest, "WORKFLOW_TENANT_HEADER_FORBIDDEN");
        }

        if (targetTenantId == Guid.Empty || SystemTenantRules.IsSystemTenantId(targetTenantId))
        {
            return failure(StatusCodes.Status400BadRequest, "WORKFLOW_TARGET_TENANT_INVALID");
        }

        var tenant = await _tenants.GetByIdAsync(targetTenantId, cancellationToken);
        if (tenant is null || tenant.IsDeleted)
        {
            return failure(StatusCodes.Status404NotFound, "WORKFLOW_TARGET_TENANT_NOT_FOUND");
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return failure(StatusCodes.Status409Conflict, "WORKFLOW_TARGET_TENANT_NOT_ACTIVE");
        }

        using (TenantScope.Begin(_tenantContext, targetTenantId))
        {
            return await action(cancellationToken);
        }
    }
}
