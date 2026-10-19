using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Contracts;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — FU-WEB-CONTRACT-FAKES: the binder of the seven OLDER fake gateways (<see cref="ContractRoutes"/>) catches what it must
/// (a missing required member, a wrong format / pattern / enum, a member the contract does not name, a path no contract owns), lets a valid
/// body and a non-2xx answer through, and an off-contract registration allows ONLY the violation it names. The last test pins that each of
/// the seven fakes still routes every registration through the binder.
/// </summary>
public sealed class ContractRoutesTests
{
    private const string Settings = """{ "waitTargetDays": 7, "fourEyes": false, "approverCandidates": [], "sampleLocations": {}, "lotReleaseRequiresApproval": false, "reconcileAfterSeconds": 300, "version": 1 }""";

    [Fact]
    public void A_valid_body_a_non_2xx_answer_and_an_unbound_service_pass()
    {
        ContractRoutes.Require("/api/quarantine/settings", 200, Settings);
        ContractRoutes.Require("/api/quarantine/settings", 409, "{}");
        ContractRoutes.Require("/api/users/aaaaaaaa-1111-4111-8111-000000000001/display-label", 200, "{}");
        Assert.Contains("/api/users", ContractRoutes.Unbound.Keys);
    }

    [Theory]
    [InlineData("/api/quarantine/settings", """{ "fourEyes": false }""", "required")]
    [InlineData("/api/quarantine/settings", """{ "waitTargetDays": 7, "fourEyes": false, "approverCandidates": [], "sampleLocations": {}, "lotReleaseRequiresApproval": false, "reconcileAfterSeconds": 300, "version": 1, "extra": 1 }""", "/extra")]
    [InlineData("/api/quarantine/settings", """{ "waitTargetDays": 99, "fourEyes": false, "approverCandidates": [], "sampleLocations": {}, "lotReleaseRequiresApproval": false, "reconcileAfterSeconds": 300, "version": 1 }""", "/waitTargetDays")]
    [InlineData("/api/product-master/products/", """{ "itemId": "x" }""", "/itemId: format")]
    [InlineData("/api/quarantine/no-such-route", "{}", "has no operation")]
    [InlineData("/api/nowhere", "{}", "no contract owns this path")]
    public void An_off_contract_body_is_refused(string prefix, string body, string expected)
    {
        var error = Assert.Throws<ContractViolationException>(() => ContractRoutes.Require(prefix, 200, body));
        Assert.Contains(error.Errors, e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void An_off_contract_registration_allows_only_the_violation_it_names()
    {
        const string flag = """{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","lotControlled":"yes","serialControlled":false}""";
        ContractRoutes.RequireOff("/api/product-master/skus/c3d4e5f6-0000-0000-0000-000000000002", 200, flag, "/lotControlled: type");
        // another mismatch beside the named one is still refused …
        Assert.Throws<ContractViolationException>(() => ContractRoutes.RequireOff("/api/product-master/skus/c3d4e5f6-0000-0000-0000-000000000002", 200,
            flag.Replace("\"skuLevel\":\"Lsku\"", "\"skuLevel\":\"Pallet\"", StringComparison.Ordinal), "/lotControlled: type"));
        // … and a body the contract accepts may not be registered as off-contract
        Assert.Throws<ContractViolationException>(() => ContractRoutes.RequireOff("/api/quarantine/settings", 200, Settings, "required"));
    }

    [Theory]
    [InlineData("Inventory/InventoryProxyControllerTests.cs")]
    [InlineData("Inventory/LocationProxyControllerTests.cs")]
    [InlineData("Inventory/MovementFormTests.cs")]
    [InlineData("Traceability/TraceProxyControllerTests.cs")]
    [InlineData("Traceability/LotMigrationWebTests.cs")]
    [InlineData("Quarantine/QuarantinePeopleTests.cs")]
    [InlineData("Expiry/ExpiryF5WebTests.cs")]
    public void Each_of_the_seven_fakes_binds_every_registration(string file)
    {
        var source = File.ReadAllText(Path.Combine(TraceViewRenderer.RepoRoot(), "frontend", "Diten.Web.Tests", file));
        var registrations = Regex.Matches(source, @"public void (On|OnOff)\(string \w+, int status, string body[^)]*\)\s*\{(?<body>[^}]*)\}");
        Assert.NotEmpty(registrations);
        Assert.All(registrations, m => Assert.Matches(@"Contracts\.ContractRoutes\.Require(Off)?\(", m.Groups["body"].Value));
        Assert.DoesNotContain("CONTRACT_ROUTES_LOG", File.ReadAllText(Path.Combine(TraceViewRenderer.RepoRoot(), "frontend", "Diten.Web.Tests", "Contracts", "ContractRoutes.cs")), StringComparison.Ordinal);
    }
}
