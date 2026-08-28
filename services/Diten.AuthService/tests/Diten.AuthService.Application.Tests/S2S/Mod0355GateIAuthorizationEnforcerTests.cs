using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Infrastructure.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class Mod0355GateIAuthorizationEnforcerTests : IDisposable
{
    private static readonly Guid Tenant = Guid.Parse("35500000-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Issued = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly string RequestHash = B64(SHA256.HashData(Encoding.UTF8.GetBytes("mod-0355-b03b-request")));
    private readonly RSA _rsa = RSA.Create(3072);

    [Fact]
    public async Task Exact_twenty_five_operation_permission_pairs_require_current_entitlement_and_local_grant_snapshot()
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var enforcer = Enforcer(local: local);
        foreach (var entry in GateIPermissionCatalogManifests.Mod0355.Entries)
        {
            var result = await enforcer.EnforceAsync(Binding(entry.OperationId, entry.PermissionKey), Snapshot(), default);
            Assert.Equal(EntitlementAttestationOutcomeKind.Continue, result.Kind);
        }
        Assert.Equal(25, local.Calls);
    }

    [Fact]
    public async Task Cross_pairs_unknown_alias_case_trim_and_wildcard_are_terminal_before_local_authorization()
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var enforcer = Enforcer(local: local);
        var entries = GateIPermissionCatalogManifests.Mod0355.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var result = await enforcer.EnforceAsync(Binding(entries[i].OperationId, entries[(i + 1) % entries.Count].PermissionKey), Snapshot(), default);
            Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden, result.Kind);
        }
        foreach (var invalid in new[]
        {
            "management-governance.process-modeling.models.unknown",
            "Management-governance.process-modeling.models.read",
            " management-governance.process-modeling.models.read",
            "management-governance.process-modeling.models.read ",
            "management-governance.process-modeling.models.*"
        })
            Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden,
                (await enforcer.EnforceAsync(Binding(invalid, invalid), Snapshot(), default)).Kind);
        Assert.Equal(0, local.Calls);
    }

    [Theory]
    [InlineData(EntitlementAttestationDecisionV1.Disabled)]
    [InlineData(EntitlementAttestationDecisionV1.Expired)]
    [InlineData(EntitlementAttestationDecisionV1.Missing)]
    [InlineData(EntitlementAttestationDecisionV1.NotApplicable)]
    public async Task Non_current_mod0355_entitlement_is_403_and_never_consumes_local_grant(EntitlementAttestationDecisionV1 decision)
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var entry = GateIPermissionCatalogManifests.Mod0355.Entries[0];
        var result = await Enforcer(decision, local).EnforceAsync(Binding(entry.OperationId, entry.PermissionKey), Snapshot(), default);
        Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden, result.Kind);
        Assert.Equal(0, local.Calls);
    }

    [Theory]
    [InlineData(Fu16LocalAuthorizationResultKind.StaleOrConcurrent, EntitlementAttestationOutcomeKind.Conflict)]
    [InlineData(Fu16LocalAuthorizationResultKind.Unauthorized, EntitlementAttestationOutcomeKind.Unauthorized)]
    [InlineData(Fu16LocalAuthorizationResultKind.Forbidden, EntitlementAttestationOutcomeKind.Forbidden)]
    [InlineData(Fu16LocalAuthorizationResultKind.AuthorityUnavailable, EntitlementAttestationOutcomeKind.ServiceUnavailable)]
    public async Task Grant_membership_principal_credential_and_authorization_freshness_remain_fail_closed(
        Fu16LocalAuthorizationResultKind localResult, EntitlementAttestationOutcomeKind expected)
    {
        var entry = GateIPermissionCatalogManifests.Mod0355.Entries[^1];
        var result = await Enforcer(local: new(localResult)).EnforceAsync(Binding(entry.OperationId, entry.PermissionKey), Snapshot(), default);
        Assert.Equal(expected, result.Kind);
    }

    [Fact]
    public async Task Shared_workload_does_not_allow_mod0007_entitlement_or_permission_to_authorize_mod0355()
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var enforcer = Enforcer(local: local);
        var mod0355 = GateIPermissionCatalogManifests.Mod0355.Entries[0];
        var mod0007 = GateIPermissionCatalogManifests.Mod0007.Entries[0];
        Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden,
            (await enforcer.EnforceAsync(Binding(mod0355.OperationId, mod0355.PermissionKey) with
            { OwnerModuleId = "MOD-0007", ModuleEntitlementCode = "MOD-0007" }, Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.Forbidden,
            (await enforcer.EnforceAsync(Binding(mod0007.OperationId, mod0007.PermissionKey), Snapshot(), default)).Kind);
        Assert.Equal(0, local.Calls);
    }

    [Fact]
    public async Task Wrong_tenant_module_service_client_audience_scope_and_request_hash_are_fail_closed()
    {
        var local = new Local(Fu16LocalAuthorizationResultKind.Accepted);
        var enforcer = Enforcer(local: local);
        var entry = GateIPermissionCatalogManifests.Mod0355.Entries[0];
        var exact = Binding(entry.OperationId, entry.PermissionKey);
        var invalid = new[]
        {
            exact with { OwnerModuleId = "MOD-0007" },
            exact with { ModuleEntitlementCode = "MOD-0007" },
            exact with { ServiceIdentity = "Diten.OtherService" },
            exact with { ClientId = "diten.other" },
            exact with { Audience = "diten-other-service" },
            exact with { ProtocolScope = "diten.s2s.invoke" }
        };
        foreach (var binding in invalid)
            Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized,
                (await enforcer.EnforceAsync(binding, Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized,
            (await enforcer.EnforceAsync(exact with { TenantId = Guid.NewGuid() }, Snapshot(), default)).Kind);
        Assert.Equal(EntitlementAttestationOutcomeKind.Unauthorized,
            (await enforcer.EnforceAsync(exact with { RequestHash = B64(SHA256.HashData([1, 2, 3])) }, Snapshot(), default)).Kind);
        Assert.Equal(0, local.Calls);
    }

    private GateIModuleAuthorizationEnforcer Enforcer(
        EntitlementAttestationDecisionV1 decision = EntitlementAttestationDecisionV1.Active,
        Local? local = null) => new(new EntitlementAttestationConsumer(
            new Provider(Token(decision)), new Keys(new(EntitlementAttestationContractV1.Issuer, "key-2026-01", _rsa, true, false)),
            local ?? new(Fu16LocalAuthorizationResultKind.Accepted), new Fence(), new() { Enabled = true }, new Clock(Issued.AddSeconds(1))));

    private static GateIModuleAuthorizationBindingV1 Binding(string operation, string permission) => new(
        Tenant, "MOD-0355", "MOD-0355", "Diten.ManagementGovernanceService", "diten.management-governance",
        "diten-management-governance-service", "diten.s2s.delegated.invoke", operation, permission, RequestHash);

    private static Fu16LocalAuthorizationSnapshot Snapshot() => new(
        Guid.Parse("35500000-0000-4000-8000-000000000002"), 4, 3,
        Guid.Parse("35500000-0000-4000-8000-000000000003"), 8,
        Guid.Parse("35500000-0000-4000-8000-000000000004"), 9, 10, "mod0355-jti", "mod0355-nonce");

    private string Token(EntitlementAttestationDecisionV1 decision)
    {
        var header = Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"key-2026-01\",\"typ\":\"diten-entitlement-attestation+jwt\"}");
        var payload = Encoding.UTF8.GetBytes($"{{\"aud\":\"diten-auth-service\",\"contract_id\":\"platform.entitlement-attestation\",\"contract_version\":\"1.0\",\"decision\":\"{decision}\",\"iat\":\"2026-08-26T12:00:00.000Z\",\"iss\":\"diten-platform-service\",\"jti\":\"mod0355-fixture-jti\",\"module_applicability_version\":7,\"module_code\":\"MOD-0355\",\"physical_entitlement_version\":3,\"request_hash\":\"{RequestHash}\",\"resolved_at_utc\":\"2026-08-26T12:00:00.000Z\",\"subscription_version\":5,\"tenant_id\":\"{Tenant:D}\",\"valid_until_utc\":\"2026-08-26T12:00:15.000Z\"}}");
        var input = $"{B64(header)}.{B64(payload)}";
        return $"{input}.{B64(_rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }

    private static string B64(ReadOnlySpan<byte> value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public void Dispose() => _rsa.Dispose();
    private sealed class Provider(string token) : IPlatformEntitlementAttestationProvider { public Task<EntitlementAttestationProviderResultV1> GetAsync(EntitlementAttestationRequestV1 request, CancellationToken cancellationToken) => Task.FromResult<EntitlementAttestationProviderResultV1>(new EntitlementAttestationProviderResultV1.Attested(token)); }
    private sealed class Keys(EntitlementAttestationTrustedKey key) : IEntitlementAttestationTrustedKeyProvider { public Task<EntitlementAttestationKeyResolution> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken) => Task.FromResult(new EntitlementAttestationKeyResolution(kid == key.Kid ? EntitlementAttestationKeyResolutionKind.Resolved : EntitlementAttestationKeyResolutionKind.Unknown, kid == key.Kid ? key : null)); }
    private sealed class Fence : IEntitlementStateVersionFence { public Task<EntitlementVersionFenceResult> ObserveAsync(Guid tenantId, string moduleCode, EntitlementStateVersionV1 version, CancellationToken cancellationToken) => Task.FromResult(EntitlementVersionFenceResult.Accepted); }
    private sealed class Local(Fu16LocalAuthorizationResultKind result) : IFu16AuthorizationTransactionSession { public int Calls { get; private set; } public Task<Fu16LocalAuthorizationResult> ValidateAndConsumeAsync(Fu16LocalAuthorizationSnapshot snapshot, CancellationToken cancellationToken) { Calls++; return Task.FromResult(new Fu16LocalAuthorizationResult(result)); } }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
