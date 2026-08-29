using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Diten.ManagementGovernanceService.Api.LocalTest;

public static class DwsLocalTestAuthenticationDefaults
{
    public const string Scheme = "DwsLocalTest";
    public const string TenantClaim = "diten_tenant_id";
    public const string EffectiveActorClaim = "diten_effective_actor_id";
    public const string DelegatedActorClaim = "diten_delegated_actor_id";
    public const string TestIdentityClaim = "diten_local_test_identity";
    public const string IdempotencyHeader = "X-Diten-Test-Idempotency-Key";
}

public sealed record DwsLocalTestIdentity(
    bool IsAuthenticated,
    Guid TenantId,
    Guid SecuritySubjectId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId = null);

public sealed class DwsLocalTestIdentityFixture
{
    private readonly object _gate = new();
    private DwsLocalTestIdentity _identity = new(
        true,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"));

    public DwsLocalTestIdentity Snapshot()
    {
        lock (_gate) return _identity;
    }

    public void Configure(DwsLocalTestIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        lock (_gate) _identity = identity;
    }
}

public interface IDwsLocalTestTrustedContextResolver
{
    DwsTrustedActorContext Resolve(ClaimsPrincipal principal, string? idempotencyKey);
}

public sealed class DwsLocalTestTrustedContextResolver : IDwsLocalTestTrustedContextResolver
{
    public DwsTrustedActorContext Resolve(ClaimsPrincipal principal, string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true || principal.FindFirstValue(DwsLocalTestAuthenticationDefaults.TestIdentityClaim) != "true")
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);

        var subject = ParseRequired(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier));
        var tenant = ParseRequired(principal.FindFirstValue(DwsLocalTestAuthenticationDefaults.TenantClaim));
        var actor = ParseRequired(principal.FindFirstValue(DwsLocalTestAuthenticationDefaults.EffectiveActorClaim));
        var delegatedValue = principal.FindFirstValue(DwsLocalTestAuthenticationDefaults.DelegatedActorClaim);
        Guid? delegated = delegatedValue is null ? null : ParseRequired(delegatedValue);
        return new(tenant, subject, actor, delegated, idempotencyKey);
    }

    private static Guid ParseRequired(string? value) => Guid.TryParseExact(value, "D", out var parsed) && parsed != Guid.Empty
        ? parsed
        : throw new DwsValidationException(DwsErrors.AuthenticationRequired);
}

public sealed class DwsLocalTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    DwsLocalTestIdentityFixture fixture)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = fixture.Snapshot();
        if (!identity.IsAuthenticated) return Task.FromResult(AuthenticateResult.NoResult());
        if (identity.TenantId == Guid.Empty || identity.SecuritySubjectId == Guid.Empty || identity.EffectiveActorId == Guid.Empty || identity.DelegatedActorId == Guid.Empty)
            return Task.FromResult(AuthenticateResult.Fail(DwsErrors.AuthenticationRequired));

        var claims = new List<Claim>
        {
            new("sub", identity.SecuritySubjectId.ToString("D")),
            new(ClaimTypes.NameIdentifier, identity.SecuritySubjectId.ToString("D")),
            new(DwsLocalTestAuthenticationDefaults.TenantClaim, identity.TenantId.ToString("D")),
            new(DwsLocalTestAuthenticationDefaults.EffectiveActorClaim, identity.EffectiveActorId.ToString("D")),
            new(DwsLocalTestAuthenticationDefaults.TestIdentityClaim, "true")
        };
        if (identity.DelegatedActorId is Guid delegated)
            claims.Add(new(DwsLocalTestAuthenticationDefaults.DelegatedActorClaim, delegated.ToString("D")));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
