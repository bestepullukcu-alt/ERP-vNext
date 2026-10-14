using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
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
        var admitsPartners = context.ActionDescriptor.EndpointMetadata?
            .OfType<Diten.Platform.Application.Contracts.IPartnerAdminAllowed>().Any() == true;

        if (isPlatformActor)
        {
            var scopeService = ScopeOf(context.HttpContext);
            if (scopeService is not null)
            {
                var ct = context.HttpContext.RequestAborted;

                // BL-521 FIX1 F1 — WHO is a partner is decided by the administrator RECORD (PlatformActorTenantScope), not
                // by the token: a record that says PartnerAdmin gets the partner rule even behind a platform_admin token
                // (a demoted administrator keeps its old token until it expires). A partner — and any platform actor its
                // record does not confirm — passes ONLY an endpoint that admits partners (PartnerAdminScopeAttribute),
                // where its tenant scope is applied. Everywhere else it is refused before the key is looked at.
                var scope = await scopeService.GetAsync(ct);
                if (!scope.IsUnrestricted && !admitsPartners)
                {
                    context.Result = PermissionDenied(context);
                    return;
                }

                // The same record the scope was decided from (one read per request): missing, deleted or inactive →
                // refused. Unreadable never reaches here: the scope throws and the request is answered 503 (FIX2 item 3).
                var admin = await scopeService.GetRecordAsync(ct);
                if (admin == null || admin.IsDeleted || admin.Status != AdministratorStatus.Active)
                {
                    context.Result = PermissionDenied(context);
                    return;
                }

                if (admin.InvitationStatus != AdministratorInvitationStatus.Accepted || admin.LastLoginAtUtc is null)
                {
                    var repository = context.HttpContext.RequestServices.GetRequiredService<IPlatformAdministratorRepository>();
                    var expectedVersion = admin.Version;
                    admin.InvitationStatus = AdministratorInvitationStatus.Accepted;
                    admin.LastLoginAtUtc = DateTimeOffset.UtcNow;
                    admin.UpdatedAt = DateTimeOffset.UtcNow;
                    admin.UpdatedBy = "platform-api";
                    await repository.UpdateAsync(admin, expectedVersion, ct);
                }
            }
            else if (string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase) && !admitsPartners)
            {
                // A host with no administrator records to read at all: the token is all there is, and a partner token
                // still passes only an endpoint that admits partners.
                context.Result = PermissionDenied(context);
                return;
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

    /// <summary>
    /// The request's scope (registered by <see cref="PlatformActorTenantScopeRegistration"/>, one record read per
    /// request). A host that registers only the administrator repository still gets the record rule, over a scope built
    /// for this request; a host with neither has no record to read.
    /// </summary>
    private static PlatformActorTenantScope? ScopeOf(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        if (services.GetService<PlatformActorTenantScope>() is { } scoped)
        {
            return scoped;
        }

        return services.GetService<IPlatformAdministratorRepository>() is { } repository
            ? new PlatformActorTenantScope(new HttpContextAccessor { HttpContext = httpContext }, repository)
            : null;
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
