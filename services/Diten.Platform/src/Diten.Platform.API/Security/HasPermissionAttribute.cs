using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var actorType = user.FindFirst("actor_type")?.Value;
        var isPlatformActor = string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);

        if (isPlatformActor)
        {
            var repository = context.HttpContext.RequestServices.GetService<IPlatformAdministratorRepository>();
            if (repository != null)
            {
                var email = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst("email")?.Value;

                if (string.IsNullOrWhiteSpace(email))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                var normalizedEmail = email.Trim().ToLowerInvariant();
                var admin = await repository.GetByNormalizedEmailAsync(normalizedEmail, context.HttpContext.RequestAborted);
                if (admin == null || admin.IsDeleted || admin.Status != AdministratorStatus.Active)
                {
                    context.Result = new ForbidResult();
                    return;
                }

                if (admin.InvitationStatus != AdministratorInvitationStatus.Accepted || admin.LastLoginAtUtc is null)
                {
                    var expectedVersion = admin.Version;
                    admin.InvitationStatus = AdministratorInvitationStatus.Accepted;
                    admin.LastLoginAtUtc = DateTimeOffset.UtcNow;
                    admin.UpdatedAt = DateTimeOffset.UtcNow;
                    admin.UpdatedBy = "platform-api";
                    await repository.UpdateAsync(admin, expectedVersion, context.HttpContext.RequestAborted);
                }
            }
        }

        if (isPlatformActor || HasPermissionClaim(user.Claims))
        {
            return;
        }

        context.Result = new ForbidResult();
    }

    private bool HasPermissionClaim(IEnumerable<Claim> claims) =>
        claims.Any(claim =>
            IsPermissionClaim(claim.Type)
            && claim.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(_permission, StringComparer.OrdinalIgnoreCase));

    private static bool IsPermissionClaim(string claimType) =>
        string.Equals(claimType, "permission", StringComparison.OrdinalIgnoreCase)
        || string.Equals(claimType, "permissions", StringComparison.OrdinalIgnoreCase)
        || claimType.EndsWith("/permission", StringComparison.OrdinalIgnoreCase)
        || claimType.EndsWith("/permissions", StringComparison.OrdinalIgnoreCase);
}
