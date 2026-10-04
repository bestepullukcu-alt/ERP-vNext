using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Controllers.Admin;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

/// <summary>
/// BL-521 — a partner administrator reaches only the tenants its administrator record allows. Measured over HTTP on the
/// REAL tenant, quota, lookup and account controllers, the production platform-actor policies
/// (DependencyInjection.AddPlatformActorPolicies), the production scope registration
/// (PlatformActorTenantScopeRegistration.AddPlatformActorTenantScope), the production tenant-resolution middleware and
/// real Mongo (a test-owned replica set — never the shared database). Two tenants: A (allowed) and B (not).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PartnerAdminScopeHttpMongoTests
{
    // ── family 1: the tenant list and its counts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_partner_lists_and_counts_only_its_allowed_tenants()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        var list = await host.ReadAsync(await host.Client(partner).GetAsync("/api/admin/tenants"));
        var stats = await host.ReadAsync(await host.Client(partner).GetAsync("/api/admin/tenants/stats"));

        var ids = list.GetProperty("data").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([Host.TenantA], ids);
        Assert.Equal(1, list.GetProperty("data").GetProperty("totalCount").GetInt64());
        Assert.Equal(1, stats.GetProperty("data").GetProperty("total").GetInt64());
    }

    // ── family 2: a tenant's own endpoints ──────────────────────────────────────────────────────────────

    public static TheoryData<string, string, string?> TenantEndpoints() => new()
    {
        { "GET", "", null },
        { "PUT", "", """{"name":"Changed","displayName":"Changed","domain":"changed.local"}""" },
        { "PUT", "/branding", """{"logoUrl":"https://x.test/l.png"}""" },
        { "POST", "/suspend", """{"reason":"x"}""" },
        { "GET", "/modules", null },
        { "GET", "/users/summary", null },
        { "GET", "/admin-users", null },
        { "POST", "/admin-users", """{"email":"a@b.test","displayName":"X"}""" },
        { "GET", "/settings", null },
        { "PUT", "/settings", """{"language":"tr","timezone":"Europe/Istanbul","currency":"TRY","environment":"Production"}""" },
        { "GET", "/login-settings", null },
        { "PUT", "/login-settings", "{}" },
    };

    [Theory]
    [MemberData(nameof(TenantEndpoints))]
    public async Task A_tenant_outside_the_partners_scope_is_not_found_on_every_endpoint_and_nothing_is_written(string method, string suffix, string? body)
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        var before = await host.TenantDocumentAsync(Host.TenantB);

        var response = await host.SendAsync(partner, method, $"/api/admin/tenants/{Host.TenantB:D}{suffix}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await host.TenantDocumentAsync(Host.TenantB));
        // the attempt is recorded as a platform security event naming the tenant it aimed at
        var record = Assert.Single(await host.SecurityEventsAsync());
        Assert.Contains(Host.TenantB.ToString("D"), record, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_allowed_tenant_answers_the_partner()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantA:D}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantA:D}/settings", null)).StatusCode);
        Assert.Empty(await host.SecurityEventsAsync());
    }

    // ── FIX1 F8-4/5/6 — what the security event says ────────────────────────────────────────────────────

    [Fact]
    public async Task The_out_of_scope_attempt_is_filed_under_the_platform_naming_the_partner_and_the_target_tenant()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantB:D}", null)).StatusCode);

        var record = Assert.Single(await host.SecurityEventDocumentsAsync());
        var payload = record["Payload"].AsBsonDocument;
        // F8-4: a platform security event — filed under the platform, not in the probed tenant's own trail
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, Host.AsGuid(record["TenantId"]));
        // F8-5: by a PARTNER administrator
        Assert.Equal(nameof(AuditActorType.PartnerAdministrator), payload["ActorType"].AsString);
        // F8-6: aimed at tenant B
        Assert.Equal(Host.TenantB, Host.AsGuid(payload["TargetTenantId"]));
    }

    // ── FIX1 F8-10 — the refusal is indistinguishable from a missing tenant ─────────────────────────────

    [Fact]
    public async Task The_out_of_scope_404_is_byte_for_byte_the_404_of_a_tenant_that_does_not_exist()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        var admin = await host.PlatformAdminAsync();

        var refused = await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantB:D}", null);
        var missing = await host.SendAsync(admin, "GET", $"/api/admin/tenants/{Guid.NewGuid():D}", null);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await refused.Content.ReadAsStringAsync());
    }

    // ── fail closed ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_partner_allowed_no_tenant_sees_none()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([]);

        await AssertSeesNothingAsync(host, partner);
    }

    /// <summary>
    /// FIX2 item 3 — a record that cannot be READ is no answer: 503 with its reason code on the list, the counts, an allowed
    /// and a foreign tenant alike; the scope does not fall open, and no security event is filed (an outage is not a
    /// partner probing a tenant). Before: a silent empty list / 404 and a false PartnerTenantScopeViolation per tenant.
    /// </summary>
    [Fact]
    public async Task A_partner_whose_record_cannot_be_read_is_answered_503_and_no_security_event_is_filed()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        // the record no longer deserializes — the scope must not fall open
        await host.Database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators).UpdateOneAsync(
            new BsonDocument("NormalizedEmail", partner.Email), new BsonDocument("$set", new BsonDocument("AllowedTenantIds", "not a list")));

        foreach (var path in new[]
                 {
                     "/api/admin/tenants", "/api/admin/tenants/stats",
                     $"/api/admin/tenants/{Host.TenantA:D}", $"/api/admin/tenants/{Host.TenantB:D}"
                 })
        {
            await AssertRecordUnavailableAsync(await host.SendAsync(partner, "GET", path, null), path);
        }

        Assert.Empty(await host.SecurityEventsAsync());
    }

    /// <summary>FIX2 item 3 — the same for a platform_admin token whose record cannot be read, on a platform-only door
    /// (the PlatformAdminOnly policy reads the record): 503, nothing deleted.</summary>
    [Fact]
    public async Task A_platform_administrator_whose_record_cannot_be_read_is_answered_503_on_a_platform_only_door()
    {
        await using var host = await Host.StartAsync();
        var admin = await host.PlatformAdminAsync();
        await host.Database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators).UpdateOneAsync(
            new BsonDocument("NormalizedEmail", admin.Email), new BsonDocument("$set", new BsonDocument("Roles", "not a list")));

        await AssertRecordUnavailableAsync(
            await host.SendAsync(admin, "DELETE", "/api/admin/tenants/bulk", $$"""{"ids":["{{Host.TenantA:D}}"]}"""), "bulk delete");

        Assert.NotNull(await host.TenantDocumentAsync(Host.TenantA));
        Assert.Empty(await host.SecurityEventsAsync());
    }

    private static async Task AssertRecordUnavailableAsync(HttpResponseMessage response, string what)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, $"{what}: {(int)response.StatusCode} {body}");
        Assert.Equal(PlatformAdministratorRecordUnavailableException.ReasonCode,
            JsonDocument.Parse(body).RootElement.GetProperty("reason_code").GetString());
    }

    /// <summary>FIX1 F8-2 — a deleted or a suspended partner record names no tenant (each half of the live-record rule).</summary>
    [Theory]
    [InlineData("deleted")]
    [InlineData("suspended")]
    public async Task A_partner_whose_record_is_deleted_or_suspended_sees_none(string state)
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA], state);

        await AssertSeesNothingAsync(host, partner);
    }

    /// <summary>
    /// FIX1 F7 — a platform_admin token is unrestricted only with its own live record: missing, deleted or suspended →
    /// it sees no tenant (it used to see all). Deliberate: on dev, a platform administrator without a record sees an empty
    /// tenant list.
    /// </summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("suspended")]
    public async Task A_platform_administrator_without_a_live_record_sees_no_tenant(string state)
    {
        await using var host = await Host.StartAsync();
        var admin = state == "missing" ? host.Unrecorded("platform_admin") : await host.PlatformAdminAsync(state);

        await AssertSeesNothingAsync(host, admin);
        // and the global, platform-only doors stay shut for it
        Assert.Equal(HttpStatusCode.Forbidden, (await host.SendAsync(admin, "DELETE", "/api/admin/tenants/bulk",
            $$"""{"ids":["{{Host.TenantA:D}}"]}""")).StatusCode);
        Assert.NotNull(await host.TenantDocumentAsync(Host.TenantA));
    }

    private static async Task AssertSeesNothingAsync(Host host, Host.Actor actor)
    {
        var list = await host.ReadAsync(await host.Client(actor).GetAsync("/api/admin/tenants"));
        var stats = await host.ReadAsync(await host.Client(actor).GetAsync("/api/admin/tenants/stats"));
        Assert.Empty(list.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(0, list.GetProperty("data").GetProperty("totalCount").GetInt64());
        Assert.Equal(0, stats.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(actor, "GET", $"/api/admin/tenants/{Host.TenantA:D}", null)).StatusCode);
    }

    // ── FIX1 F1 — the RECORD decides who is a partner, not the token ───────────────────────────────────

    [Fact]
    public async Task A_demoted_administrator_with_a_platform_admin_token_lists_and_reaches_only_its_partner_tenants()
    {
        await using var host = await Host.StartAsync();
        var demoted = await host.DemotedAsync([Host.TenantA]);

        var list = await host.ReadAsync(await host.Client(demoted).GetAsync("/api/admin/tenants"));
        var ids = list.GetProperty("data").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        Assert.Equal([Host.TenantA], ids);
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(demoted, "GET", $"/api/admin/tenants/{Host.TenantB:D}", null)).StatusCode);
    }

    [Fact]
    public async Task A_demoted_administrator_with_a_platform_admin_token_cannot_delete_tenants()
    {
        await using var host = await Host.StartAsync();
        var demoted = await host.DemotedAsync([Host.TenantA]);

        // PlatformAdminOnly, no permission filter on these: only the policy stands between the token and the delete.
        var bulk = await host.SendAsync(demoted, "DELETE", "/api/admin/tenants/bulk", $$"""{"ids":["{{Host.TenantA:D}}","{{Host.TenantB:D}}"]}""");
        var single = await host.SendAsync(demoted, "DELETE", $"/api/admin/tenants/{Host.TenantA:D}", null);

        Assert.Equal(HttpStatusCode.Forbidden, bulk.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, single.StatusCode);
        Assert.NotNull(await host.TenantDocumentAsync(Host.TenantA));
        Assert.NotNull(await host.TenantDocumentAsync(Host.TenantB));
    }

    [Fact]
    public async Task A_demoted_administrator_with_a_platform_admin_token_cannot_open_or_invite_platform_administrators()
    {
        await using var host = await Host.StartAsync();
        var demoted = await host.DemotedAsync([Host.TenantA]);

        // PlatformActor policy + permission filter, unmarked endpoints (global objects).
        var list = await host.SendAsync(demoted, "GET", "/api/platform/administrators", null);
        var invite = await host.SendAsync(demoted, "POST", "/api/platform/administrators",
            """{"email":"new.admin@x.test","userName":"new.admin","displayName":"X","actorType":"PlatformAdmin","roles":["SuperAdmin"],"allowedTenantIds":[]}""");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);
        Assert.Equal(1, await host.AdministratorCountAsync());
    }

    // ── FIX1 F2 — a declared key the route does not carry closes the endpoint to partners ──────────────

    [Fact]
    public async Task An_action_added_without_the_tenant_key_under_a_controller_key_is_closed_to_partners_and_recorded()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        var admin = await host.PlatformAdminAsync();

        var refused = await host.SendAsync(partner, "GET", "/api/admin/bl521-probe/added-later", null);
        var allowed = await host.SendAsync(admin, "GET", "/api/admin/bl521-probe/added-later", null);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); // a platform administrator is not affected
        var record = Assert.Single(await host.SecurityEventDocumentsAsync());
        Assert.Contains("added-later", record.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_action_that_declares_it_filters_in_its_query_is_open_and_an_undecided_one_is_closed()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", "/api/admin/bl521-probe/declared-list", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", $"/api/admin/bl521-probe/{Host.TenantA:D}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(partner, "GET", $"/api/admin/bl521-probe/{Host.TenantB:D}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(partner, "GET", "/api/admin/bl521-probe/undecided", null)).StatusCode);
    }

    // ── FIX1 F3 — the tenant type is a platform administrator's ─────────────────────────────────────────

    [Fact]
    public async Task A_partner_cannot_change_the_tenant_type_of_its_own_tenant_but_may_resend_the_same_type()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        var before = await host.TenantDocumentAsync(Host.TenantA);

        var internalType = await host.SendAsync(partner, "PUT", $"/api/admin/tenants/{Host.TenantA:D}",
            $$"""{"name":"Renamed","displayName":"Renamed","domain":"renamed-a.local","tenantType":{{(int)TenantType.Internal}}}""");
        Assert.Equal(HttpStatusCode.Forbidden, internalType.StatusCode);
        Assert.Equal(before, await host.TenantDocumentAsync(Host.TenantA)); // nothing written

        var sameType = await host.SendAsync(partner, "PUT", $"/api/admin/tenants/{Host.TenantA:D}",
            $$"""{"name":"Renamed","displayName":"Renamed","domain":"renamed-a.local","tenantType":{{(int)TenantType.Customer}}}""");
        Assert.Equal(HttpStatusCode.OK, sameType.StatusCode);
        Assert.Equal(TenantType.Customer, (await host.TenantAsync(Host.TenantA)).TenantType);
    }

    [Fact]
    public async Task A_platform_administrator_changes_the_tenant_type()
    {
        await using var host = await Host.StartAsync();
        var admin = await host.PlatformAdminAsync();

        var response = await host.SendAsync(admin, "PUT", $"/api/admin/tenants/{Host.TenantA:D}",
            $$"""{"name":"Renamed","displayName":"Renamed","domain":"renamed-a.local","tenantType":{{(int)TenantType.Internal}}}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TenantType.Internal, (await host.TenantAsync(Host.TenantA)).TenantType);
    }

    // ── FIX1 F6 — the platform system tenant is never in a partner's scope ──────────────────────────────

    [Fact]
    public async Task A_partner_record_naming_the_platform_system_tenant_does_not_open_it()
    {
        await using var host = await Host.StartAsync();
        await host.SeedTenantAsync(SystemTenantRules.PlatformSystemTenantId, "PLATFORM");
        var partner = await host.PartnerAsync([SystemTenantRules.PlatformSystemTenantId, Host.TenantA]);

        Assert.Equal(HttpStatusCode.NotFound,
            (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{SystemTenantRules.PlatformSystemTenantId:D}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantA:D}", null)).StatusCode);
    }

    [Fact]
    public void The_partner_scope_never_holds_the_platform_system_tenant()
    {
        var scope = PlatformTenantScope.Only([SystemTenantRules.PlatformSystemTenantId, Host.TenantA, Guid.Empty]);

        Assert.False(scope.Allows(SystemTenantRules.PlatformSystemTenantId));
        Assert.Equal([Host.TenantA], scope.TenantIds);
        Assert.True(scope.IsPartner);
        Assert.False(scope.IsUnrestricted);
    }

    // ── FIX1 F8-1 — the quota family addresses its tenant by route ──────────────────────────────────────

    [Fact]
    public async Task The_quota_endpoints_answer_the_partner_only_for_its_allowed_tenant()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        var mine = await host.SendAsync(partner, "GET", $"/api/platform/tenants/{Host.TenantA:D}/quotas", null);
        var theirs = await host.SendAsync(partner, "GET", $"/api/platform/tenants/{Host.TenantB:D}/quotas", null);

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        var record = Assert.Single(await host.SecurityEventsAsync());
        Assert.Contains(Host.TenantB.ToString("D"), record, StringComparison.OrdinalIgnoreCase);
    }

    // ── FIX1 F8-8/9 — what partners keep that names no tenant ───────────────────────────────────────────

    [Fact]
    public async Task A_partner_keeps_the_reference_lookups()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", "/api/lookups/timezones", null)).StatusCode);
    }

    [Fact]
    public async Task A_partner_keeps_its_own_account()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        var me = await host.ReadAsync(await host.Client(partner).GetAsync("/api/platform/account/me"));

        Assert.Equal(partner.Email, me.GetProperty("data").GetProperty("email").GetString());
    }

    // ── what stays with platform administrators ─────────────────────────────────────────────────────────

    public static TheoryData<string, string, string?> PlatformOnlyEndpoints() => new()
    {
        { "POST", "/api/admin/tenants", """{"code":"NEW","name":"New"}""" },
        { "DELETE", $"/api/admin/tenants/{Host.TenantA:D}", null },
        { "DELETE", "/api/admin/tenants/bulk", $$"""{"ids":["{{Host.TenantA:D}}"]}""" },
        // FIX1 F4 — lifting a suspension and writing quotas: platform administrators only, even for the partner's own tenant
        { "POST", $"/api/admin/tenants/{Host.TenantA:D}/reactivate", """{"reason":"x"}""" },
        { "POST", $"/api/platform/tenants/{Host.TenantA:D}/quotas/initialize", """{"reason":"x"}""" },
        { "POST", $"/api/platform/tenants/{Host.TenantA:D}/quotas/sync-limits", """{"reason":"x"}""" },
        { "GET", "/api/platform/module-catalog", null },
        { "GET", "/api/platform/subscription-plans", null },
        { "GET", "/api/platform/administrators", null },
        { "POST", "/api/platform/administrators", """{"email":"new.admin@x.test","displayName":"X","actorType":"PlatformAdmin"}""" },
    };

    [Theory]
    [MemberData(nameof(PlatformOnlyEndpoints))]
    public async Task Creating_or_deleting_tenants_and_every_global_object_refuse_a_partner(string method, string path, string? body)
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]); // even for its OWN tenant
        var before = await host.TenantDocumentAsync(Host.TenantA);

        var response = await host.SendAsync(partner, method, path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await host.TenantDocumentAsync(Host.TenantA));
        host.QuotaWrites.Verify(q => q.InitializeTenantQuotasAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        host.QuotaWrites.Verify(q => q.SyncTenantQuotaLimitsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── no regression for platform administrators ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_platform_administrator_still_sees_and_reaches_every_tenant()
    {
        await using var host = await Host.StartAsync();
        var admin = await host.PlatformAdminAsync();

        var list = await host.ReadAsync(await host.Client(admin).GetAsync("/api/admin/tenants"));
        var stats = await host.ReadAsync(await host.Client(admin).GetAsync("/api/admin/tenants/stats"));

        Assert.Equal(2, list.GetProperty("data").GetProperty("totalCount").GetInt64());
        Assert.Equal(2, stats.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(admin, "GET", $"/api/admin/tenants/{Host.TenantB:D}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(admin, "GET", $"/api/platform/tenants/{Host.TenantB:D}/quotas", null)).StatusCode);
        Assert.Empty(await host.SecurityEventsAsync());
    }

    // ── host ────────────────────────────────────────────────────────────────────────────────────────────

    internal sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-bl521-test";
        private const string Audience = "diten-platform-bl521-test";
        private const string Secret = "BL-521 partner scope http round trip signing key, test only, 0123456789";

        public static readonly Guid TenantA = Guid.Parse("52152152-0000-4000-8000-0000000000a1");
        public static readonly Guid TenantB = Guid.Parse("52152152-0000-4000-8000-0000000000b2");

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;

        public IMongoDatabase Database { get; }

        /// <summary>The quota service behind the quota controller: reads answer, writes are only counted.</summary>
        public Mock<IQuotaService> QuotaWrites { get; } = new();

        public sealed record Actor(string ActorType, Guid UserId, string Email);

        private Host(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            Database = database;
            var dbContext = new PlatformDbContext(mongo.Client, database);
            QuotaWrites.Setup(q => q.GetStatusesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response<IReadOnlyList<QuotaStatusDto>>.Success([]));
            var lookups = new Mock<IPlatformLookupProvider>();
            lookups.Setup(l => l.GetTimezonesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddProblemDetails();
                    services.AddExceptionHandler<Diten.Platform.API.Middleware.GlobalExceptionHandler>();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = Issuer,
                                ValidAudience = Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                                ClockSkew = TimeSpan.Zero
                            };
                        });
                    // THE production policies — not a copy.
                    services.AddAuthorization(Diten.Platform.Infrastructure.DependencyInjection.AddPlatformActorPolicies);
                    // THE production scope registration (Program.cs calls the same method) — not a copy (FIX1 F8-7).
                    services.AddPlatformActorTenantScope();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();

                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
                    services.AddScoped<IPlatformAdministratorRepository, PlatformAdministratorRepository>();
                    services.AddScoped<AuditOutboxRepository>();
                    services.AddScoped<ITransactionalAuditOutboxStore>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddSingleton(QuotaWrites.Object);
                    services.AddSingleton(lookups.Object);

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TenantsController).Assembly));
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(Bl521ProbeController).Assembly));
                        manager.FeatureProviders.Add(new OnlyControllers(
                            typeof(TenantsController), typeof(ModuleCatalogController), typeof(SubscriptionPlansController),
                            typeof(AdministratorsController), typeof(QuotasController), typeof(LookupsController),
                            typeof(PlatformAccountController), typeof(Bl521ProbeController)));
                    });
                })
                .Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public static async Task<Host> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                         .Where(c => c.Name is PlatformCollections.Tenants or PlatformCollections.PlatformAdministrators))
            {
                await collection.ApplyAsync(database, CancellationToken.None);
            }

            var host = new Host(mongo, database);
            foreach (var tenant in new[] { TenantA, TenantB })
            {
                var tail = tenant.ToString("N")[^8..];
                await host.SeedTenantAsync(tenant, "T" + tail);
            }

            return host;
        }

        public Task SeedTenantAsync(Guid id, string code)
        {
            var tail = id.ToString("N")[^8..];
            return Database.GetCollection<Tenant>(PlatformCollections.Tenants).InsertOneAsync(new Tenant
            {
                Id = id, Code = code, Slug = "t" + tail, Name = "Tenant " + tail, DisplayName = "Tenant " + tail,
                Domain = tail + ".local", Status = TenantStatus.Active
            });
        }

        public Task<Actor> PartnerAsync(IReadOnlyList<Guid> allowed, string state = "active") =>
            SeedAsync("partner_admin", ActorType.PartnerAdmin, allowed, state);

        public Task<Actor> PlatformAdminAsync(string state = "active") => SeedAsync("platform_admin", ActorType.PlatformAdmin, [], state);

        /// <summary>FIX1 F1 — a platform administrator demoted to partner: its record says PartnerAdmin, its (still
        /// valid) token still says platform_admin.</summary>
        public Task<Actor> DemotedAsync(IReadOnlyList<Guid> allowed) => SeedAsync("platform_admin", ActorType.PartnerAdmin, allowed, "active");

        /// <summary>A valid token whose holder has no administrator record at all.</summary>
        public Actor Unrecorded(string claim) => new(claim, Guid.NewGuid(), $"{claim}.{Guid.NewGuid():N}@bl521.test");

        private async Task<Actor> SeedAsync(string claim, ActorType type, IReadOnlyList<Guid> allowed, string state)
        {
            var actor = new Actor(claim, Guid.NewGuid(), $"{claim}.{Guid.NewGuid():N}@bl521.test");
            await Database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(new PlatformAdministrator
            {
                Email = actor.Email, NormalizedEmail = actor.Email, UserName = actor.Email, NormalizedUserName = actor.Email,
                DisplayName = claim, ActorType = type, PartnerId = type == ActorType.PartnerAdmin ? Guid.NewGuid() : null,
                AllowedTenantIds = allowed.ToList(),
                Status = state == "suspended" ? AdministratorStatus.Suspended : AdministratorStatus.Active,
                IsDeleted = state == "deleted",
                InvitationStatus = AdministratorInvitationStatus.Accepted, LastLoginAtUtc = DateTimeOffset.UtcNow
            });
            return actor;
        }

        public HttpClient Client(Actor actor)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(actor));
            return client;
        }

        public Task<HttpResponseMessage> SendAsync(Actor actor, string method, string path, string? json)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Client(actor).SendAsync(request);
        }

        public async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
            return JsonDocument.Parse(body).RootElement;
        }

        public async Task<string?> TenantDocumentAsync(Guid id) =>
            (await Database.GetCollection<BsonDocument>(PlatformCollections.Tenants)
                .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).FirstOrDefaultAsync())?.ToJson();

        public async Task<Tenant> TenantAsync(Guid id) =>
            await Database.GetCollection<Tenant>(PlatformCollections.Tenants).Find(t => t.Id == id).SingleAsync();

        public async Task<long> AdministratorCountAsync() =>
            await Database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        /// <summary>The out-of-scope attempts waiting in the audit outbox (the central door's store).</summary>
        public async Task<IReadOnlyList<string>> SecurityEventsAsync() =>
            (await SecurityEventDocumentsAsync()).Select(d => d.ToJson()).ToList();

        public async Task<IReadOnlyList<BsonDocument>> SecurityEventDocumentsAsync() =>
            (await Database.GetCollection<BsonDocument>("audit_outbox").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Where(d => d.ToJson().Contains(PartnerAdminScopeAttribute.OutOfScopeRequestType, StringComparison.Ordinal))
                .ToList();

        public static Guid AsGuid(BsonValue value) => value switch
        {
            BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary => binary.ToGuid(GuidRepresentation.Standard),
            BsonBinaryData { SubType: BsonBinarySubType.UuidLegacy } binary => binary.ToGuid(GuidRepresentation.CSharpLegacy),
            BsonString text => Guid.Parse(text.Value),
            _ => throw new InvalidOperationException($"Not a GUID: {value}")
        };

        private static string Token(Actor actor) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience,
                [
                    new Claim(JwtRegisteredClaimNames.Sub, actor.UserId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, actor.Email),
                    new Claim("actor_type", actor.ActorType)
                ],
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            return _mongo.DisposeAsync();
        }

        private sealed class OnlyControllers(params Type[] controllers) : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) => controllers.Contains(typeInfo.AsType());
        }
    }
}

/// <summary>
/// BL-521 FIX1 F2 — a test-only controller declared the way a tenant controller is (controller-level tenant key), with
/// the three cases the key rule must decide: the key carried, the key missing (an action "added later"), and actions
/// that declare their own decision. Hosted only by <see cref="PartnerAdminScopeHttpMongoTests"/>.
/// </summary>
[ApiController]
[Route("api/admin/bl521-probe")]
[Authorize(Policy = "PlatformActor")]
[PartnerAdminScope("id")]
public sealed class Bl521ProbeController : ControllerBase
{
    [HttpGet("{id:guid}")]
    public IActionResult ById(Guid id) => Ok(id);

    [HttpGet("added-later")]
    public IActionResult AddedLater() => Ok("ran");

    [HttpGet("declared-list")]
    [PartnerAdminScope(PartnerAdminScopeAttribute.FiltersInQuery)]
    public IActionResult DeclaredList() => Ok("ran");

    [HttpGet("undecided")]
    [PartnerAdminScope]
    public IActionResult Undecided() => Ok("ran");
}
