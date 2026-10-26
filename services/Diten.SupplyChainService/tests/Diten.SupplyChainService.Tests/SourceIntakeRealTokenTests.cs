using System.Security.Claims;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Xunit;
namespace Diten.SupplyChainService.Tests;

/// <summary>
/// H2 finding, recorded as a test and NOT fixed here (Natig, 2026-10-21). <see cref="TrustedSourceContext"/>
/// (Application/Features/SourceIntake/Contracts/SourceClients.cs:16-29) still requires exactly one
/// <c>legal_entity_id</c> claim. AuthService's token has carried none since R-2 — the legal entity is the
/// X-Legal-Entity-Id header everywhere else — so a principal built from a real user's token is refused.
///
/// Today no HTTP endpoint calls <c>FromValidatedPrincipal</c> (measured on main dfb5de610: no production caller
/// outside SourceClients.cs), so no real user is refused yet; the day warehouse intake is wired to a request, every
/// real user would be — the same shape the Sandop/Capacity gates had until 2026-10-18. This test asserts today's
/// behaviour; when the context is aligned to the header (a separate, approved change) it must be inverted.
/// </summary>
public sealed class SourceIntakeRealTokenTests
{
    [Fact]
    public void A_principal_from_a_real_AuthService_token_is_refused_today_because_it_has_no_legal_entity_claim()
    {
        var scope = new ShipmentScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // Exactly the claims AuthService issues (see Common/AuthServiceToken.cs) — and no legal_entity_id.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", scope.ActorId.ToString()),
            new Claim("email", "test-user@example.test"),
            new Claim("actor_type", "tenant_user"),
            new Claim("tenant_id", scope.TenantId.ToString()),
            new Claim("pwd_change_required", "false"),
            new Claim("role", "Admin"),
            new Claim("permission", "supplychain.shipments.create")
        ], "AuthServiceShaped"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            TrustedSourceContext.FromValidatedPrincipal(principal, scope, "real-user-token"));
    }
}
