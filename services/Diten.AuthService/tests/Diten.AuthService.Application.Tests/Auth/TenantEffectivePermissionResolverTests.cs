using Diten.AuthService.Application.Common.Authorization;
using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Tests.Auth;

public sealed class TenantEffectivePermissionResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly string PpmRead = PpmPermissionCatalog.All[0];
    private static readonly string PpmCreate = PpmPermissionCatalog.All[1];
    private const string OtherPermission = "mdm.products.read";

    [Fact]
    public async Task Active_authoritative_entitlement_intersects_explicit_role_grants()
    {
        var client = FakeClient.Confirmed(new EntitledModulePermissionKeys("PPM", [PpmRead, PpmCreate]));
        var result = await Create(client).ResolveAsync(TenantId, [OtherPermission, PpmRead], CancellationToken.None);

        Assert.Equal([OtherPermission, PpmRead], result);
        Assert.Equal(TenantId, client.ObservedTenantId);
        Assert.Equal(1, client.ReadCount);
    }

    [Fact]
    public async Task Entitlement_catalog_cannot_create_a_permission_missing_from_the_role()
    {
        var client = FakeClient.Confirmed(new EntitledModulePermissionKeys("PPM", PpmPermissionCatalog.All));
        var result = await Create(client).ResolveAsync(TenantId, [PpmRead], CancellationToken.None);

        Assert.Equal([PpmRead], result);
    }

    [Fact]
    public async Task Unavailable_authority_cannot_smuggle_module_rows_into_claims()
    {
        var client = new FakeClient(new TenantEntitlementReadResult(
            false,
            [new EntitledModulePermissionKeys("PPM", [PpmRead])]));

        var result = await Create(client).ResolveAsync(TenantId, [OtherPermission, PpmRead], CancellationToken.None);

        Assert.Equal([OtherPermission], result);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "wrong-case")]
    [InlineData(true, "empty")]
    [InlineData(true, "duplicate")]
    public async Task Non_active_or_ambiguous_authority_emits_zero_ppm_and_preserves_other_modules(bool authoritative, string shape)
    {
        var modules = shape switch
        {
            "wrong-case" => new[] { new EntitledModulePermissionKeys("ppm", [PpmRead]) },
            "empty" => new[] { new EntitledModulePermissionKeys("PPM", Array.Empty<string>()) },
            "duplicate" => new[]
            {
                new EntitledModulePermissionKeys("PPM", [PpmRead]),
                new EntitledModulePermissionKeys("PPM", [PpmRead])
            },
            _ => Array.Empty<EntitledModulePermissionKeys>()
        };
        var client = authoritative ? FakeClient.Confirmed(modules) : FakeClient.Unavailable();

        var result = await Create(client).ResolveAsync(TenantId, [OtherPermission, PpmRead], CancellationToken.None);

        Assert.Equal([OtherPermission], result);
    }

    [Fact]
    public async Task Noncanonical_ppm_keys_and_duplicate_role_input_do_not_escape_the_exact_catalog()
    {
        var client = FakeClient.Confirmed(new EntitledModulePermissionKeys("PPM", [PpmRead, "ppm.*"]));
        var result = await Create(client).ResolveAsync(
            TenantId,
            [OtherPermission, OtherPermission.ToUpperInvariant(), PpmRead, PpmRead.ToUpperInvariant(), "ppm.*", " "],
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, value => string.Equals(value, OtherPermission, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, value => string.Equals(value, PpmRead, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task No_ppm_role_grant_avoids_authority_read()
    {
        var client = FakeClient.Unavailable();
        var result = await Create(client).ResolveAsync(TenantId, [OtherPermission], CancellationToken.None);

        Assert.Equal([OtherPermission], result);
        Assert.Equal(0, client.ReadCount);
    }

    [Fact]
    public async Task Caller_cancellation_is_forwarded_and_propagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var client = FakeClient.ThrowCancellation();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Create(client).ResolveAsync(TenantId, [PpmRead], source.Token));
        Assert.Equal(source.Token, client.ObservedCancellationToken);
    }

    private static TenantEffectivePermissionResolver Create(FakeClient client) =>
        new(client, new PpmEntitlementPermissionPolicy());

    private sealed class FakeClient(TenantEntitlementReadResult result, bool throwCancellation = false) : ITenantEntitlementClient
    {
        public int ReadCount { get; private set; }
        public Guid ObservedTenantId { get; private set; }
        public CancellationToken ObservedCancellationToken { get; private set; }

        public static FakeClient Confirmed(params EntitledModulePermissionKeys[] modules) =>
            new(TenantEntitlementReadResult.Confirmed(modules));

        public static FakeClient Confirmed(IReadOnlyList<EntitledModulePermissionKeys> modules) =>
            new(TenantEntitlementReadResult.Confirmed(modules));

        public static FakeClient Unavailable() => new(TenantEntitlementReadResult.Unavailable());
        public static FakeClient ThrowCancellation() => new(TenantEntitlementReadResult.Unavailable(), true);

        public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
        {
            ReadCount++;
            ObservedTenantId = tenantId;
            ObservedCancellationToken = ct;
            if (throwCancellation)
            {
                throw new OperationCanceledException(ct);
            }

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
