using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Lookups;

/*
 * WP-BRD-TENANT-CRM-SETS — GET api/lookups/reference-data/consumable-sets/{setCode}/published-values.
 *
 * WHAT WAS WRONG. Every CRM reference read went to the Platform consumer path with the CALLER's token, and that path needs
 * Platform.BusinessReferenceData.Consumer.Read, which only an administrator holds. A tenant user without it — every mobile
 * rep, every non-admin Web user — saw required reference fields refused, optional ones accepted unvalidated, and the
 * option / scope endpoints come back empty.
 *
 * THE RULE. Any signed-in TENANT user may read the allow-listed sets (BusinessReferenceData:ConsumableSets). A set is read
 * by its OWN BRD scope: tenant-scoped → the caller's tenant (scope_key = the tenant the server resolved from the token);
 * global → the global source (the reference tenant of BusinessReferenceData:CatalogLoad). Another tenant's tenant-scoped
 * set — including the reference tenant's — is never served. Nothing to serve → 404 reference_set_not_published, never 500.
 *
 * ONE HTTP ROUND TRIP PER CASE through the shipped controller (SignedTenantTokenHttpHost: signed token → JWT bearer → the
 * real TenantResolutionMiddleware → the controller → the production MediatR pipeline, consumer service and Mongo
 * repository) against a REAL MongoDB. Three tenants, each its own harness tenant: A and B (callers) and R (reference).
 * Each publishes DIFFERENT values for the same set code, so a read in the wrong tenant shows up as the wrong values.
 *
 * Mobile requirement §9 mapping (BACKEND-MOD-0048-CRM-REFERENCE-SETS-REQUIREMENTS): 1-5 the five mobile sets without the
 * Platform permission; 6-7 tenant / global scope; 8 isolation; 9 not listed → 404; 10 unauthenticated → 401; 11 no tenant →
 * 400; 14 ACCOUNT-TYPE upper case. Draft / submitted / deprecated, unpublished / retired, the unchanged sets/{setCode}
 * route and the 403 on the consumer path are pinned too. §9 12-13 (Web/mobile create through CRM) are CT E4.
 */
public sealed class ConsumableReferenceDataHttpMongoTests : IAsyncLifetime
{
    private const string ConsumerRead = "Platform.BusinessReferenceData.Consumer.Read";
    private static readonly Guid User = Guid.Parse("c0a5b1e0-0000-4000-8000-0000000000a1");
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly string[] MobileSets =
        ["account-type", "account-status", "account-category", "contact-type", "contact-status"];

    private MongoIntegrationHarness _tenantA = null!;
    private MongoIntegrationHarness _tenantB = null!;
    private MongoIntegrationHarness _reference = null!;

    private Guid TenantA => _tenantA.TenantId;
    private Guid TenantB => _tenantB.TenantId;
    private Guid ReferenceTenant => _reference.TenantId;

    public async Task InitializeAsync()
    {
        _tenantA = await MongoIntegrationHarness.CreateAsync(SchemaProfile.BusinessReferenceData);
        _tenantB = await MongoIntegrationHarness.CreateAsync(SchemaProfile.BusinessReferenceData);
        _reference = await MongoIntegrationHarness.CreateAsync(SchemaProfile.BusinessReferenceData);
    }

    public async Task DisposeAsync()
    {
        await _tenantA.DisposeAsync();
        await _tenantB.DisposeAsync();
        await _reference.DisposeAsync();
    }

    // ---- §9 1-5 + 6: the five mobile sets, read by a user WITHOUT the Platform permission, from the caller's tenant ----

    [Fact]
    public async Task The_five_mobile_sets_are_read_by_a_tenant_user_without_the_platform_permission()
    {
        foreach (var setCode in MobileSets)
        {
            await PublishAsync(TenantA, setCode, "tenant", Value($"{setCode}-a1", 10), Value($"{setCode}-a2", 20));
        }

        using var host = Host();
        foreach (var setCode in MobileSets)
        {
            var response = await host.GetAsync(ConsumablePath(setCode), Token(TenantA), TenantA.ToString());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await Body(response);
            Assert.Equal(setCode, body.GetProperty("data").GetProperty("setCode").GetString());
            Assert.Equal(new[] { $"{setCode}-a1", $"{setCode}-a2" }, Codes(body));
        }
    }

    // ---- §9 8: isolation — tenant sets come from the CALLER's tenant only ----------------------------------------

    [Fact]
    public async Task A_tenant_scoped_set_is_read_in_the_callers_tenant_never_in_another_or_the_reference_tenant()
    {
        await PublishAsync(TenantA, "account-type", "tenant", Value("hospital-a", 10));
        await PublishAsync(TenantB, "account-type", "tenant", Value("hospital-b", 10));
        await PublishAsync(ReferenceTenant, "account-type", "tenant", Value("hospital-r", 10));

        using var host = Host();
        var asA = await Body(await host.GetAsync(ConsumablePath("account-type"), Token(TenantA)));
        var asB = await Body(await host.GetAsync(ConsumablePath("account-type"), Token(TenantB)));

        Assert.Equal(new[] { "hospital-a" }, Codes(asA));
        Assert.Equal(new[] { "hospital-b" }, Codes(asB));
    }

    [Fact]
    public async Task A_tenant_scoped_set_only_the_reference_tenant_holds_is_not_published_for_the_caller()
    {
        await PublishAsync(ReferenceTenant, "contact-type", "tenant", Value("doctor-r", 10));

        using var host = Host();
        var response = await host.GetAsync(ConsumablePath("contact-type"), Token(TenantA));

        await AssertRefusalAsync(response, HttpStatusCode.NotFound, "reference_set_not_published");
    }

    [Fact]
    public async Task A_client_scope_key_naming_another_tenant_is_ignored()
    {
        await PublishAsync(TenantA, "account-status", "tenant", Value("active-a", 10));
        await PublishAsync(TenantB, "account-status", "tenant", Value("active-b", 10));

        using var host = Host();
        var response = await host.GetAsync(ConsumablePath("account-status") + $"?scope_key={TenantB}", Token(TenantA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "active-a" }, Codes(await Body(response)));
    }

    [Fact]
    public async Task An_x_tenant_id_header_naming_another_tenant_than_the_token_is_refused()
    {
        await PublishAsync(TenantA, "account-type", "tenant", Value("hospital-a", 10));

        using var host = Host();
        var response = await host.GetAsync(ConsumablePath("account-type"), Token(TenantA), TenantB.ToString());

        await AssertRefusalAsync(response, HttpStatusCode.BadRequest, "tenant_mismatch");
    }

    // ---- §9 7: a global set comes from the global source -----------------------------------------------------------

    [Fact]
    public async Task A_global_set_is_read_from_the_reference_tenant_when_the_caller_holds_none()
    {
        await PublishAsync(ReferenceTenant, "COUNTRY_CODES", "Global", Value("TR", 10), Value("AZ", 20));

        using var host = Host();
        var asA = await host.GetAsync(ConsumablePath("COUNTRY_CODES"), Token(TenantA));
        var asB = await host.GetAsync(ConsumablePath("COUNTRY_CODES"), Token(TenantB));

        Assert.Equal(HttpStatusCode.OK, asA.StatusCode);
        Assert.Equal(new[] { "TR", "AZ" }, Codes(await Body(asA)));
        Assert.Equal(new[] { "TR", "AZ" }, Codes(await Body(asB)));
    }

    // ---- KORU: same values the Platform consumer path gives an administrator ------------------------------------

    [Fact]
    public async Task A_tenant_user_gets_exactly_what_the_consumer_path_gives_an_administrator_of_the_same_tenant()
    {
        await PublishAsync(TenantA, "contact-status", "tenant",
            Value("active", 20), Value("inactive", 10), Value("legacy", 30, deprecated: true));
        await PublishAsync(TenantA, "COUNTRY_CODES", "global", Value("UZ", 10), Value("GE", 20));

        using var host = Host();
        var adminTenantSet = await Body(await host.GetAsync(
            $"/api/v1/reference-data/sets/contact-status/published-values?scope_key={TenantA}", Token(TenantA, ConsumerRead)));
        var adminGlobalSet = await Body(await host.GetAsync(
            "/api/v1/reference-data/sets/COUNTRY_CODES/published-values", Token(TenantA, ConsumerRead)));

        var userTenantSet = await Body(await host.GetAsync(ConsumablePath("contact-status"), Token(TenantA)));
        var userGlobalSet = await Body(await host.GetAsync(ConsumablePath("COUNTRY_CODES"), Token(TenantA)));

        Assert.Equal(Codes(adminTenantSet), Codes(userTenantSet));
        Assert.Equal(new[] { "inactive", "active" }, Codes(userTenantSet));
        Assert.Equal(Codes(adminGlobalSet), Codes(userGlobalSet));
        Assert.Equal(new[] { "UZ", "GE" }, Codes(userGlobalSet));
    }

    // ---- draft / submitted / deprecated; unpublished / retired / missing ------------------------------------------

    [Fact]
    public async Task Only_the_published_effective_version_and_its_non_deprecated_values_are_returned_in_sort_order()
    {
        var setId = await CreateSetAsync(TenantA, "account-category", "tenant");
        await AddVersionAsync(TenantA, setId, 1, BusinessReferenceDataVersionStatus.Deprecated, false, Value("old", 10));
        await AddVersionAsync(TenantA, setId, 2, BusinessReferenceDataVersionStatus.Published, false,
            Value("c", 30), Value("a", 10), Value("gone", 5, deprecated: true), Value("b", 20));
        await AddVersionAsync(TenantA, setId, 3, BusinessReferenceDataVersionStatus.Draft, false, Value("draft-only", 10));
        await AddVersionAsync(TenantA, setId, 4, BusinessReferenceDataVersionStatus.Draft, true, Value("submitted-only", 10));

        using var host = Host();
        var body = await Body(await host.GetAsync(ConsumablePath("account-category"), Token(TenantA)));

        Assert.Equal(2, body.GetProperty("data").GetProperty("versionNumber").GetInt32());
        Assert.Equal(new[] { "a", "b", "c" }, Codes(body));
        Assert.All(Items(body), item => Assert.True(item.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task A_listed_set_with_only_a_draft_or_submitted_version_is_not_published_never_500()
    {
        var setId = await CreateSetAsync(TenantA, "contact-role", "tenant");
        await AddVersionAsync(TenantA, setId, 1, BusinessReferenceDataVersionStatus.Draft, true, Value("x", 10));

        using var host = Host();
        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("contact-role"), Token(TenantA)), HttpStatusCode.NotFound, "reference_set_not_published");
    }

    [Fact]
    public async Task A_retired_set_is_not_published_never_500()
    {
        await PublishAsync(TenantA, "gender", "tenant", BusinessReferenceDataSetStatus.Retired, Value("f", 10));

        using var host = Host();
        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("gender"), Token(TenantA)), HttpStatusCode.NotFound, "reference_set_not_published");
    }

    [Fact]
    public async Task A_listed_set_nobody_holds_is_not_published()
    {
        using var host = Host();
        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("territory-level"), Token(TenantA)), HttpStatusCode.NotFound, "reference_set_not_published");
    }

    // ---- §9 9: allow-list ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_set_outside_the_allow_list_is_not_accessible_even_when_published_in_the_callers_tenant()
    {
        await PublishAsync(TenantA, "qms-document-class", "tenant", Value("sop", 10));

        using var host = Host();
        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("qms-document-class"), Token(TenantA)),
            HttpStatusCode.NotFound, "reference_set_not_tenant_accessible");
    }

    [Fact]
    public async Task The_configured_list_replaces_the_code_default()
    {
        await PublishAsync(TenantA, "qms-document-class", "tenant", Value("sop", 10));
        await PublishAsync(TenantA, "account-type", "tenant", Value("hospital-a", 10));

        using var host = Host(configured: ["qms-document-class"]);

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync(ConsumablePath("qms-document-class"), Token(TenantA))).StatusCode);
        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("account-type"), Token(TenantA)),
            HttpStatusCode.NotFound, "reference_set_not_tenant_accessible");
    }

    // ---- §9 14: set code case ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ACCOUNT-TYPE")]
    [InlineData("  Account-Type ")]
    public async Task The_set_code_is_matched_case_insensitively_and_read_under_its_listed_spelling(string requested)
    {
        await PublishAsync(TenantA, "account-type", "tenant", Value("hospital-a", 10));

        using var host = Host();
        var response = await host.GetAsync(ConsumablePath(requested), Token(TenantA));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("account-type", body.GetProperty("data").GetProperty("setCode").GetString());
        Assert.Equal(new[] { "hospital-a" }, Codes(body));
    }

    // ---- §9 10-11: who may call ------------------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_token_the_answer_is_401()
    {
        using var host = Host();
        var response = await host.GetAsync(ConsumablePath("account-type"), bearerToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_platform_actor_or_a_token_without_a_tenant_gets_tenant_context_required_and_reads_nothing()
    {
        await PublishAsync(ReferenceTenant, "COUNTRY_CODES", "global", Value("TR", 10));

        using var host = Host();
        var platformAdmin = SignedTenantTokenHttpHost.Token("platform_admin", PlatformTenant, User, ConsumerRead);
        var noTenant = SignedTenantTokenHttpHost.Token("tenant_user", tenant: null, User);

        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("COUNTRY_CODES"), platformAdmin), HttpStatusCode.BadRequest, "tenant_context_required");
        await AssertRefusalAsync(
            await host.GetAsync(ConsumablePath("COUNTRY_CODES"), noTenant), HttpStatusCode.BadRequest, "tenant_context_required");
    }

    [Fact]
    public async Task The_platform_consumer_path_still_refuses_a_user_without_the_permission()
    {
        await PublishAsync(TenantA, "account-type", "tenant", Value("hospital-a", 10));

        using var host = Host();
        var response = await host.GetAsync(
            $"/api/v1/reference-data/sets/account-type/published-values?scope_key={TenantA}", Token(TenantA));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- regression: sets/{setCode} and its three Global sets are unchanged ------------------------------------

    [Fact]
    public async Task The_existing_sets_route_still_reads_its_three_sets_in_the_reference_tenant_and_nothing_else()
    {
        await PublishAsync(ReferenceTenant, "legal-form", "global", Value("LLC", 10));
        await PublishAsync(TenantA, "legal-form", "global", Value("A-ONLY", 10));
        await PublishAsync(TenantA, "account-type", "tenant", Value("hospital-a", 10));

        using var host = Host();
        var legalForm = await host.GetAsync("/api/lookups/reference-data/sets/legal-form/published-values", Token(TenantA));
        var accountType = await host.GetAsync("/api/lookups/reference-data/sets/account-type/published-values", Token(TenantA));

        Assert.Equal(HttpStatusCode.OK, legalForm.StatusCode);
        Assert.Equal(new[] { "LLC" }, Codes(await Body(legalForm)));
        await AssertRefusalAsync(accountType, HttpStatusCode.NotFound, "reference_set_not_tenant_accessible");
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private static string ConsumablePath(string setCode)
        => $"/api/lookups/reference-data/consumable-sets/{Uri.EscapeDataString(setCode)}/published-values";

    private static string Token(Guid tenant, params string[] permissions)
        => SignedTenantTokenHttpHost.TenantUserToken(tenant, User, permissions);

    private SignedTenantTokenHttpHost Host(IReadOnlyList<string>? configured = null)
    {
        var dbContext = _tenantA.DbContext;
        var referenceTenant = ReferenceTenant;
        return new SignedTenantTokenHttpHost(services =>
        {
            services.AddSingleton(dbContext);
            services.AddScoped<IBusinessReferenceDataStewardshipRepository>(sp =>
                new BusinessReferenceDataStewardshipRepository(
                    sp.GetRequiredService<IPlatformDbContext>(), sp.GetRequiredService<ITenantContext>()));
            services.Configure<BusinessReferenceDataCatalogLoadOptions>(o => o.TenantId = referenceTenant.ToString());
            // BusinessReferenceDataController (the consumer path the comparison test calls) takes the Common one.
            services.AddScoped<Diten.Platform.Common.Observability.ICorrelationContext,
                Diten.Platform.Common.Observability.CorrelationContext>();
            if (configured is not null)
            {
                services.Configure<BusinessReferenceDataConsumableSetsOptions>(o => o.ConsumableSets = configured.ToList());
            }
        });
    }

    private static BusinessReferenceDataValue Value(string code, int sortOrder, bool deprecated = false) => new()
    {
        ValueCode = code,
        DisplayName = code.ToUpperInvariant(),
        SortOrder = sortOrder,
        IsDeprecated = deprecated
    };

    private Task PublishAsync(Guid tenant, string setCode, string scopeType, params BusinessReferenceDataValue[] values)
        => PublishAsync(tenant, setCode, scopeType, BusinessReferenceDataSetStatus.Active, values);

    private async Task PublishAsync(
        Guid tenant, string setCode, string scopeType, BusinessReferenceDataSetStatus status, params BusinessReferenceDataValue[] values)
    {
        var setId = await CreateSetAsync(tenant, setCode, scopeType, status);
        await AddVersionAsync(tenant, setId, 1, BusinessReferenceDataVersionStatus.Published, false, values);
    }

    private async Task<Guid> CreateSetAsync(
        Guid tenant, string setCode, string scopeType, BusinessReferenceDataSetStatus status = BusinessReferenceDataSetStatus.Active)
    {
        var (repository, context) = Repository(tenant);
        var set = new BusinessReferenceDataSet
        {
            TenantId = tenant,
            SetCode = setCode,
            Name = setCode,
            ScopeType = scopeType,
            Status = status
        };
        using (TenantScope.Begin(context, tenant))
        {
            await repository.CreateSetAsync(set);
        }

        return set.BusinessReferenceDataSetId;
    }

    private async Task AddVersionAsync(
        Guid tenant, Guid setId, int number, BusinessReferenceDataVersionStatus status, bool submitted,
        params BusinessReferenceDataValue[] values)
    {
        var (repository, _) = Repository(tenant);
        var published = status is BusinessReferenceDataVersionStatus.Published or BusinessReferenceDataVersionStatus.Deprecated;
        await repository.CreateVersionAsync(new BusinessReferenceDataVersion
        {
            TenantId = tenant,
            BusinessReferenceDataSetId = setId,
            VersionNumber = number,
            Status = status,
            IsImmutable = published,
            PublishedAt = published ? DateTimeOffset.UtcNow.AddHours(-number - 1) : null,
            SubmittedAt = submitted ? DateTimeOffset.UtcNow.AddMinutes(-5) : null,
            BusinessReferenceDataGovernanceState = submitted
                ? BusinessReferenceDataGovernanceState.Submitted
                : BusinessReferenceDataGovernanceState.Draft,
            Values = values.ToList()
        });
    }

    private (BusinessReferenceDataStewardshipRepository Repository, TenantContext Context) Repository(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return (new BusinessReferenceDataStewardshipRepository(_tenantA.DbContext, context), context);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(text), $"empty body, status {(int)response.StatusCode}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static IEnumerable<JsonElement> Items(JsonElement body)
        => body.GetProperty("data").GetProperty("items").EnumerateArray();

    private static string[] Codes(JsonElement body)
        => Items(body).Select(item => item.GetProperty("code").GetString()!).ToArray();

    private static async Task AssertRefusalAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(code, text, StringComparison.Ordinal);
    }
}
