using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Domain.Enums;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementContract;

namespace Diten.Platform.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;
    private readonly string[] _alternatives;

    public HasPermissionAttribute(string permission)
    {
        _permission = permission;
        _alternatives = [];
    }

    /// <summary>
    /// WP-VW-W3-PLT — "<paramref name="permission"/> OR any of <paramref name="alternatives"/>". Used where a
    /// narrower, scope-bound key may reach the same endpoint as the general key (the document repository's
    /// <c>visit-evidence.*</c> keys beside <c>read</c>/<c>manage</c>). The attribute only admits the caller; the
    /// scope and owner decision for the narrower key belongs to the application service behind it (AD-5).
    /// Several attributes on one action still combine as AND.
    /// </summary>
    public HasPermissionAttribute(string permission, params string[] alternatives)
    {
        _permission = permission;
        _alternatives = alternatives ?? [];
    }

    /// <summary>The required permission key. Exposed so startup auto-registration (A1) can reflect it without
    /// reaching into the private field.</summary>
    public string Permission => _permission;

    /// <summary>WP-VW-W3-PLT — keys that satisfy this attribute instead of <see cref="Permission"/>. Reflected by A1
    /// auto-registration too, so an alternative key reaches the permission catalogue like the primary one.</summary>
    public IReadOnlyList<string> AlternativePermissions => _alternatives;

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
                    context.Result = PermissionDenied(context);
                    return;
                }

                var normalizedEmail = email.Trim().ToLowerInvariant();
                var admin = await repository.GetByNormalizedEmailAsync(normalizedEmail, context.HttpContext.RequestAborted);
                if (admin == null || admin.IsDeleted || admin.Status != AdministratorStatus.Active)
                {
                    context.Result = PermissionDenied(context);
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

        // AG-STEP-004B Slice 1B (dual-read) + AG-STEP-011 Group A: the canonical/alias claim-matching is delegated to
        // the side-effect-free PermissionClaimEvaluator so the enforcement filter and the future self-explain observer
        // share one implementation and cannot drift. Behavior is unchanged: a canonical requirement is satisfied by the
        // canonical key OR any mapped legacy alias; a legacy/unknown requirement accepts only itself (fail-closed).
        if (isPlatformActor
            || PermissionClaimEvaluator.Evaluate(user.Claims, _permission).IsSatisfied
            || _alternatives.Any(alternative => PermissionClaimEvaluator.Evaluate(user.Claims, alternative).IsSatisfied))
        {
            return;
        }

        context.Result = PermissionDenied(context);
    }

    private static ObjectResult PermissionDenied(AuthorizationFilterContext context)
    {
        var correlationContext = context.HttpContext.RequestServices.GetService<ICorrelationContext>();
        var correlationId = string.IsNullOrWhiteSpace(correlationContext?.CorrelationId)
            ? context.HttpContext.TraceIdentifier
            : correlationContext.CorrelationId;

        return new ObjectResult(Response<NoContent>.Fail(
            "Permission denied.",
            StatusCodes.Status403Forbidden,
            DocumentManagementReasonCodes.PermissionDenied,
            correlationId))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
