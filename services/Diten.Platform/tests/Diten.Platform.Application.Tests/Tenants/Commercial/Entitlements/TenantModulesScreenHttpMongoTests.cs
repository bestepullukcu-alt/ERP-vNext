using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

/// <summary>
/// BL-500 — the tenant's Modules tab, measured the way the screen uses it: a PLATFORM actor's signed token → the real
/// <c>TenantResolutionMiddleware</c> (which puts the request in the platform context) → the real
/// <see cref="TenantModuleEntitlementsController"/> → the production MediatR pipeline and handlers → the real
/// <see cref="TenantModuleEntitlementRepository"/>, transaction executor, version counter and audit outbox over a
/// test-owned MongoDB replica set.
///
/// <para><b>Why HTTP.</b> The suspend that failed on the screen passed every existing test: those build the
/// repository with <c>TenantContext.SetTenant(tenantId)</c>, the one context a platform administrator's request never
/// has. Only a request that goes through the middleware reproduces the context the failure lives in.</para>
///
/// <para>Doubled: the module catalogue, the subscription plan lookup, the quota service (always grants) and the
/// integration-event writer (records nothing). None of them is this file's subject.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TenantModulesScreenHttpMongoTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public TenantModulesScreenHttpMongoTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private static readonly Guid Tenant = Guid.Parse("50050050-0000-4000-8000-0000000000a1");
    private static readonly Guid OtherTenant = Guid.Parse("50050050-0000-4000-8000-0000000000b2");

    [Fact]
    public async Task A_platform_administrator_suspends_an_active_add_on_and_the_list_shows_it_suspended()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);

        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.True(row.IsEnabled);
        Assert.NotNull(row.RowVersion);

        // Exactly what the screen sends: the row's own version, as it arrived in the list.
        var response = await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = row.ModuleCode,
            physicalEntitlementId = row.PhysicalEntitlementId,
            reason = "Suspended from the Modules tab",
            rowVersion = row.RowVersion
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.False(after.IsEnabled);
        Assert.False((await host.StoredAsync(seeded.Id)).IsEnabled);
    }

    [Fact]
    public async Task A_suspended_add_on_is_enabled_again()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon, enabled: false);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);

        var response = await host.PostAsync(Tenant, $"{seeded.Id:D}/enable", row.RowVersion);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True((await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id).IsEnabled);
    }

    [Fact]
    public async Task An_expired_add_on_offers_a_new_date_first_and_never_enable_and_extending_it_makes_it_active()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon, expiry: DateTimeOffset.UtcNow.AddDays(-3));

        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Equal("Expired", row.EffectiveAccess);
        // The row is switched ON; it is expired. "Enable" would change nothing — it is not offered.
        Assert.Equal(["extendExpiry", "disable"], row.AllowedActions);

        var response = await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new
        {
            expiryDateUtc = DateTimeOffset.UtcNow.AddDays(30),
            reason = (string?)null,
            rowVersion = row.RowVersion
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Equal("Active", after.EffectiveAccess);
        Assert.True(after.IsEnabled);
        Assert.Equal(["disable", "extendExpiry"], after.AllowedActions);
    }

    [Fact]
    public async Task Suspending_by_module_code_where_the_list_offers_no_plan_line_action_is_refused_and_nothing_is_written()
    {
        // BL-500 — the path without a row id is the PLAN line's. It used to fall back to "the override row of that
        // module, if any" and write it — an action the list never offered (that row carries its own buttons).
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "CRM", EntitlementSource.ManualOverride);

        var refusal = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "CRM",
            physicalEntitlementId = (Guid?)null,
            reason = "Suspended by module",
            rowVersion = (string?)null
        }));

        Assert.Equal(HttpStatusCode.Conflict, refusal.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ActionNotOffered, refusal.Code);
        var stored = await host.StoredAsync(seeded.Id);
        Assert.True(stored.IsEnabled);
        Assert.Equal(seeded.RowVersion, stored.RowVersion);
        Assert.Equal(1, await host.CountAsync("tenant_module_entitlements"));
    }

    [Fact]
    public async Task Suspending_a_plan_module_blocks_it_and_the_override_row_carries_the_way_back()
    {
        await using var host = await Host.StartAsync();
        var plan = (await host.ListAsync(Tenant)).Single(r => r.IsProjectionRow);
        Assert.Equal(Host.PlanModule, plan.ModuleCode);
        Assert.Equal(["disable"], plan.AllowedActions);

        var response = await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = plan.ModuleCode,
            physicalEntitlementId = (Guid?)null,
            reason = "Not for this tenant",
            rowVersion = (string?)null
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rows = await host.ListAsync(Tenant);
        var planAfter = rows.Single(r => r.IsProjectionRow);
        Assert.Equal("BlockedByOverride", planAfter.EffectiveAccess);
        // A plan's module comes with the plan: once blocked there is nothing left to do on the plan's own line.
        Assert.Empty(planAfter.AllowedActions!);
        var overrideRow = rows.Single(r => !r.IsProjectionRow);
        Assert.Equal(["enable", "extendExpiry", "removeOverride"], overrideRow.AllowedActions);
    }

    [Fact]
    public async Task Removing_a_manual_override_takes_the_row_away()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "CRM", EntitlementSource.ManualOverride);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Equal(["disable", "extendExpiry", "removeOverride"], row.AllowedActions);

        var response = await host.DeleteAsync(Tenant, $"{seeded.Id:D}/manual-override", new { rowVersion = row.RowVersion });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await host.ListAsync(Tenant), r => r.PhysicalEntitlementId == seeded.Id);
    }

    [Fact]
    public async Task A_baseline_module_offers_no_action_and_the_server_still_refuses_its_removal_with_a_code()
    {
        await using var host = await Host.StartAsync();
        // A stray override row on a baseline module — what the owner met on the Task Center line.
        var seeded = await host.SeedAsync(Tenant, Host.BaselineModule, EntitlementSource.ManualOverride);

        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Empty(row.AllowedActions!);

        // The rule itself did not move: the server refuses, now with a code a screen can translate.
        var remove = await Host.ReadRefusalAsync(await host.DeleteAsync(Tenant, $"{seeded.Id:D}/manual-override", new { rowVersion = row.RowVersion }));
        Assert.Equal(HttpStatusCode.Conflict, remove.Status);
        Assert.Equal("ENTITLEMENT_MODULE_BASELINE", remove.Code);

        var disable = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = Host.BaselineModule, physicalEntitlementId = seeded.Id, reason = "x", rowVersion = row.RowVersion
        }));
        Assert.Equal(HttpStatusCode.Conflict, disable.Status);
        Assert.Equal("ENTITLEMENT_MODULE_BASELINE", disable.Code);
        Assert.True((await host.StoredAsync(seeded.Id)).IsEnabled);
    }

    [Fact]
    public async Task A_stale_screen_is_refused_with_its_own_code_and_the_row_is_left_alone()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);
        var staleVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var disable = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "GOLDENSLIM", physicalEntitlementId = seeded.Id, reason = "x", rowVersion = staleVersion
        }));
        var expiry = await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new
        {
            expiryDateUtc = DateTimeOffset.UtcNow.AddDays(5), reason = (string?)null, rowVersion = staleVersion
        }));

        Assert.Equal(HttpStatusCode.Conflict, disable.Status);
        Assert.Equal("ENTITLEMENT_STALE", disable.Code);
        Assert.Equal(HttpStatusCode.Conflict, expiry.Status);
        Assert.Equal("ENTITLEMENT_STALE", expiry.Code);
        var stored = await host.StoredAsync(seeded.Id);
        Assert.True(stored.IsEnabled);
        Assert.Null(stored.ExpiryDateUtc);
        Assert.Equal(seeded.RowVersion, stored.RowVersion);
    }

    [Fact]
    public async Task Another_tenants_row_id_under_this_tenants_route_is_not_found_and_stays_untouched()
    {
        await using var host = await Host.StartAsync();
        var theirs = await host.SeedAsync(OtherTenant, "CRM", EntitlementSource.ManualOverride);
        var version = Convert.ToBase64String(theirs.RowVersion);

        var refusals = new[]
        {
            await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new { moduleCode = "CRM", physicalEntitlementId = theirs.Id, reason = "x", rowVersion = version })),
            await Host.ReadRefusalAsync(await host.PostAsync(Tenant, $"{theirs.Id:D}/enable", version)),
            await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{theirs.Id:D}/expiry", new { expiryDateUtc = DateTimeOffset.UtcNow.AddDays(5), reason = (string?)null, rowVersion = version })),
            await Host.ReadRefusalAsync(await host.DeleteAsync(Tenant, $"{theirs.Id:D}/manual-override", new { rowVersion = version }))
        };

        Assert.All(refusals, refusal =>
        {
            Assert.Equal(HttpStatusCode.NotFound, refusal.Status);
            Assert.Equal("ENTITLEMENT_NOT_FOUND", refusal.Code);
        });
        var stored = await host.StoredAsync(theirs.Id);
        Assert.True(stored.IsEnabled);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.ExpiryDateUtc);
        Assert.Equal(theirs.RowVersion, stored.RowVersion);
        Assert.DoesNotContain(await host.ListAsync(Tenant), r => r.PhysicalEntitlementId == theirs.Id);
    }

    [Fact]
    public async Task Adding_a_module_twice_is_refused_with_a_code_and_a_baseline_module_cannot_be_added()
    {
        await using var host = await Host.StartAsync();
        object Add(string code) => new { moduleCode = code, source = "Addon", isEnabled = true, expiryDateUtc = (DateTimeOffset?)null, reason = (string?)null, rowVersion = (string?)null };

        var first = await host.PostAsync(Tenant, null, Add("CRM"));
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());

        var second = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, null, Add("CRM")));
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal("ENTITLEMENT_ALREADY_EXISTS", second.Code);

        var baseline = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, null, Add(Host.BaselineModule)));
        Assert.Equal(HttpStatusCode.Conflict, baseline.Status);
        Assert.Equal("ENTITLEMENT_MODULE_BASELINE", baseline.Code);
    }

    // ── WP-PLATFORM-TENANT-MODULES-01 FIX1 — the server refuses exactly what the list does not offer ─────────────

    /// <summary>One row state of the Modules tab and the answer every action must get on it.</summary>
    public sealed record RowState(
        string Name, string Module, EntitlementSource Source, bool Enabled, bool Expired,
        string[] Offered, string? Disable, string? Enable, string? ExtendExpiry, string? RemoveOverride)
    {
        public override string ToString() => Name;
    }

    // An action's expectation: null = offered, the server writes (204); a code = refused with that code, nothing written.
    public static TheoryData<RowState> RowStates() => new()
    {
        new("add-on, on", "GOLDENSLIM", EntitlementSource.Addon, true, false,
            ["disable", "extendExpiry"], null, "ENTITLEMENT_ACTION_NOT_OFFERED", null, "ENTITLEMENT_NOT_MANUAL_OVERRIDE"),
        new("add-on, off", "GOLDENSLIM", EntitlementSource.Addon, false, false,
            ["enable", "extendExpiry"], "ENTITLEMENT_ACTION_NOT_OFFERED", null, null, "ENTITLEMENT_NOT_MANUAL_OVERRIDE"),
        new("add-on, on but expired", "GOLDENSLIM", EntitlementSource.Addon, true, true,
            ["extendExpiry", "disable"], null, "ENTITLEMENT_ACTION_NOT_OFFERED", null, "ENTITLEMENT_NOT_MANUAL_OVERRIDE"),
        new("manual override, on", "CRM", EntitlementSource.ManualOverride, true, false,
            ["disable", "extendExpiry", "removeOverride"], null, "ENTITLEMENT_ACTION_NOT_OFFERED", null, null),
        new("system row", "GOLDENSLIM", EntitlementSource.System, true, false,
            [], "ENTITLEMENT_ACTION_NOT_OFFERED", "ENTITLEMENT_ACTION_NOT_OFFERED", "ENTITLEMENT_ACTION_NOT_OFFERED", "ENTITLEMENT_NOT_MANUAL_OVERRIDE"),
        new("baseline override", Host.BaselineModule, EntitlementSource.ManualOverride, true, false,
            [], "ENTITLEMENT_MODULE_BASELINE", "ENTITLEMENT_MODULE_BASELINE", "ENTITLEMENT_MODULE_BASELINE", "ENTITLEMENT_MODULE_BASELINE"),
        new("core add-on", Host.CoreModule, EntitlementSource.Addon, true, false,
            [], "ENTITLEMENT_MODULE_CORE", "ENTITLEMENT_MODULE_CORE", "ENTITLEMENT_MODULE_CORE", "ENTITLEMENT_MODULE_CORE"),
        new("add-on of a module that left the catalogue, on", Host.GoneModule, EntitlementSource.Addon, true, false,
            ["disable"], null, "ENTITLEMENT_MODULE_NOT_FOUND", "ENTITLEMENT_MODULE_NOT_FOUND", "ENTITLEMENT_NOT_MANUAL_OVERRIDE"),
        new("override of a module that left the catalogue, off", Host.GoneModule, EntitlementSource.ManualOverride, false, false,
            ["removeOverride"], "ENTITLEMENT_ACTION_NOT_OFFERED", "ENTITLEMENT_MODULE_NOT_FOUND", "ENTITLEMENT_MODULE_NOT_FOUND", null),
    };

    [Theory]
    [MemberData(nameof(RowStates))]
    public async Task Every_action_on_every_row_state_gets_the_answer_the_list_gives(RowState state)
    {
        await using var host = await Host.StartAsync();
        var expiry = state.Expired ? DateTimeOffset.UtcNow.AddDays(-2) : (DateTimeOffset?)null;
        var actions = new (string Name, string? Expected)[]
        {
            ("disable", state.Disable), ("enable", state.Enable), ("extendExpiry", state.ExtendExpiry), ("removeOverride", state.RemoveOverride)
        };

        // One tenant per action, each with the same row: an action that is carried out must not change the next one's row.
        for (var index = 0; index < actions.Length; index++)
        {
            var (action, expected) = actions[index];
            var tenant = Guid.Parse($"50050050-0000-4000-8000-00000000f{index:D3}");
            var seeded = await host.SeedAsync(tenant, state.Module, state.Source, state.Enabled, expiry);
            var row = (await host.ListAsync(tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
            Assert.Equal(state.Offered, row.AllowedActions);
            // The list and the server give ONE answer: offered ⇔ carried out.
            Assert.Equal(state.Offered.Contains(action), expected is null);

            var response = action switch
            {
                "disable" => await host.PostAsync(tenant, "disable", new
                {
                    moduleCode = row.ModuleCode, physicalEntitlementId = seeded.Id, reason = "matrix", rowVersion = row.RowVersion
                }),
                "enable" => await host.PostAsync(tenant, $"{seeded.Id:D}/enable", row.RowVersion),
                "extendExpiry" => await host.PatchAsync(tenant, $"{seeded.Id:D}/expiry", new
                {
                    expiryDateUtc = DateTimeOffset.UtcNow.AddDays(30), reason = (string?)null, rowVersion = row.RowVersion
                }),
                _ => await host.DeleteAsync(tenant, $"{seeded.Id:D}/manual-override", new { rowVersion = row.RowVersion })
            };

            var refusal = await Host.ReadRefusalAsync(response);
            if (expected is null)
            {
                Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{state.Name} / {action}: {refusal.Body}");
                Assert.NotEqual(seeded.RowVersion, (await host.StoredAsync(seeded.Id)).RowVersion);
            }
            else
            {
                Assert.True(expected == refusal.Code, $"{state.Name} / {action}: expected {expected}, got {(int)refusal.Status} {refusal.Body}");
                Assert.Equal(expected == TenantModuleEntitlementRefusalCodes.ModuleNotFound ? HttpStatusCode.NotFound : HttpStatusCode.Conflict, refusal.Status);
                var stored = await host.StoredAsync(seeded.Id);
                Assert.Equal(seeded.RowVersion, stored.RowVersion);
                Assert.False(stored.IsDeleted);
            }
        }
    }

    [Fact]
    public async Task A_plan_line_whose_access_an_enabled_override_gives_offers_nothing_and_its_suspension_is_refused()
    {
        // The plan is not what gives access here — the override row is, and it carries its own Disable.
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, Host.PlanModule, EntitlementSource.ManualOverride);

        var rows = await host.ListAsync(Tenant);
        var plan = rows.Single(r => r.IsProjectionRow);
        Assert.Equal("EnabledByOverride", plan.EffectiveAccess);
        Assert.Empty(plan.AllowedActions!);
        Assert.Equal(["disable", "extendExpiry", "removeOverride"], rows.Single(r => r.PhysicalEntitlementId == seeded.Id).AllowedActions);

        var refusal = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = Host.PlanModule, physicalEntitlementId = (Guid?)null, reason = "x", rowVersion = (string?)null
        }));
        Assert.Equal(HttpStatusCode.Conflict, refusal.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ActionNotOffered, refusal.Code);
        Assert.Equal(seeded.RowVersion, (await host.StoredAsync(seeded.Id)).RowVersion);
    }

    [Fact]
    public async Task A_row_id_is_the_module_the_body_code_only_has_to_agree_and_a_mismatch_is_refused_unwritten()
    {
        // FIX1 item 1 — the core/baseline check and the quota release used to read the BODY's module.
        await using var host = await Host.StartAsync();
        var baselineRow = await host.SeedAsync(Tenant, Host.BaselineModule, EntitlementSource.ManualOverride);
        var addOn = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);

        // A baseline row named as an ordinary module: refused, and NOT disabled under the ordinary module's rules.
        var disguised = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "GOLDENSLIM", physicalEntitlementId = baselineRow.Id, reason = "x", rowVersion = Convert.ToBase64String(baselineRow.RowVersion)
        }));
        // An ordinary row named as the baseline module: refused as a mismatch, not as "baseline".
        var misnamed = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = Host.BaselineModule, physicalEntitlementId = addOn.Id, reason = "x", rowVersion = Convert.ToBase64String(addOn.RowVersion)
        }));

        Assert.Equal(HttpStatusCode.BadRequest, disguised.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ModuleMismatch, disguised.Code);
        Assert.Equal(HttpStatusCode.BadRequest, misnamed.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ModuleMismatch, misnamed.Code);
        Assert.True((await host.StoredAsync(baselineRow.Id)).IsEnabled);
        Assert.True((await host.StoredAsync(addOn.Id)).IsEnabled);

        // Agreeing codes (in any letter case) are the ordinary path.
        var agreed = await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "goldenslim", physicalEntitlementId = addOn.Id, reason = "x", rowVersion = Convert.ToBase64String(addOn.RowVersion)
        });
        Assert.Equal(HttpStatusCode.NoContent, agreed.StatusCode);
    }

    [Fact]
    public async Task A_stored_row_is_written_only_against_a_version_and_a_request_without_one_is_refused_with_its_code()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "CRM", EntitlementSource.ManualOverride, enabled: false);

        var refusals = new[]
        {
            await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new { moduleCode = "CRM", physicalEntitlementId = seeded.Id, reason = "x", rowVersion = (string?)null })),
            await Host.ReadRefusalAsync(await host.PostAsync(Tenant, $"{seeded.Id:D}/enable", (string?)null)),
            await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new { expiryDateUtc = DateTimeOffset.UtcNow.AddDays(5), reason = (string?)null, rowVersion = (string?)null })),
            await Host.ReadRefusalAsync(await host.DeleteAsync(Tenant, $"{seeded.Id:D}/manual-override", new { rowVersion = (string?)null }))
        };

        Assert.All(refusals, refusal =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, refusal.Status);
            Assert.Equal(TenantModuleEntitlementRefusalCodes.RowVersionRequired, refusal.Code);
        });
        var stored = await host.StoredAsync(seeded.Id);
        Assert.Equal(seeded.RowVersion, stored.RowVersion);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Two_screens_suspending_the_same_plan_module_at_once_write_one_override_and_the_other_hears_why()
    {
        await using var host = await Host.StartAsync();
        object Suspend() => new { moduleCode = Host.PlanModule, physicalEntitlementId = (Guid?)null, reason = "at once", rowVersion = (string?)null };

        var responses = await Task.WhenAll(host.PostAsync(Tenant, "disable", Suspend()), host.PostAsync(Tenant, "disable", Suspend()));
        var answers = await Task.WhenAll(responses.Select(Host.ReadRefusalAsync));

        Assert.Equal(1, answers.Count(a => a.Status == HttpStatusCode.NoContent));
        var loser = answers.Single(a => a.Status != HttpStatusCode.NoContent);
        Assert.True(loser.Status == HttpStatusCode.Conflict, $"{(int)loser.Status}: {loser.Body} {string.Join(" || ", host.Failures)}");
        // Whichever way the race falls: the second write met the first one's row (stale), or read the list after it (no longer offered).
        Assert.Contains(loser.Code, new[] { TenantModuleEntitlementRefusalCodes.Stale, TenantModuleEntitlementRefusalCodes.ActionNotOffered });
        Assert.Equal(1, await host.CountAsync("tenant_module_entitlements"));
    }

    [Fact]
    public async Task Extending_expiry_requires_a_date_in_the_future_and_never_removes_the_expiry()
    {
        // FIX1 item 8 — an empty date box used to send null, which REMOVED the expiry while the screen said "saved".
        await using var host = await Host.StartAsync();
        var expiry = DateTimeOffset.UtcNow.AddDays(10);
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon, expiry: expiry);
        var version = Convert.ToBase64String(seeded.RowVersion);

        var none = await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new { expiryDateUtc = (DateTimeOffset?)null, reason = (string?)null, rowVersion = version }));
        var past = await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new { expiryDateUtc = DateTimeOffset.UtcNow.AddDays(-1), reason = (string?)null, rowVersion = version }));

        Assert.Equal(HttpStatusCode.BadRequest, none.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ExpiryRequired, none.Code);
        Assert.Equal(HttpStatusCode.BadRequest, past.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ExpiryInPast, past.Code);
        var stored = await host.StoredAsync(seeded.Id);
        Assert.NotNull(stored.ExpiryDateUtc);
        Assert.Equal(seeded.RowVersion, stored.RowVersion);
    }

    [Fact]
    public async Task A_suspension_without_a_reason_is_refused_with_a_code_the_screen_can_say()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);

        var refusal = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "GOLDENSLIM", physicalEntitlementId = seeded.Id, reason = "", rowVersion = Convert.ToBase64String(seeded.RowVersion)
        }));

        Assert.Equal(HttpStatusCode.BadRequest, refusal.Status);
        Assert.Equal(TenantModuleEntitlementRefusalCodes.ReasonRequired, refusal.Code);
        Assert.True((await host.StoredAsync(seeded.Id)).IsEnabled);
    }

    // ── WP-PLATFORM-AUDIT-INTX-01 (AUD-001 §10-K1): the in-transaction audit record is DELIVERED ───────────────
    // Real HTTP → real handler → real transaction → audit_outbox → the production AuditOutboxProcessor → audit_events.
    // Before the fix every such row went to dead letter (the payload had no TenantId / ActorType / Category /
    // SourceService) and not one reached audit_events.

    [Fact]
    public async Task K1_suspending_a_module_reaches_audit_events_once_with_the_administrator_and_the_state_before_and_after()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);

        var response = await host.PostAsync(Tenant, "disable", new { moduleCode = "GOLDENSLIM", physicalEntitlementId = seeded.Id, reason = "audit proof", rowVersion = row.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(1, await host.ProcessAuditOutboxAsync());
        var audit = Assert.Single(await host.AuditEventsAsync());
        Assert.Equal("DisableTenantModuleEntitlementCommand", audit.RequestType);
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);   // who: the kind of person …
        Assert.Equal(Host.Administrator, audit.ActorId);                         // … and which one
        Assert.Equal("p***@di10.test", audit.ActorEmailMasked);                 // masked as the central pipeline masks
        Assert.Equal(Tenant, audit.TenantId);                                    // owned by the tenant the change is about
        Assert.Equal(Tenant, audit.TargetTenantId);
        Assert.Equal(AuditCategory.SubscriptionBilling, audit.Category);
        Assert.Equal("TenantModuleEntitlement", audit.EntityType);
        Assert.Equal(seeded.Id, audit.EntityId);
        Assert.Equal(AuditOperation.Deactivate, audit.Operation);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("Diten.Platform", audit.SourceService);
        Assert.Equal("subscription-billing", audit.SourceModule);
        Assert.Equal(true, audit.BeforeState!["IsEnabled"]);
        Assert.Equal(false, audit.AfterState!["IsEnabled"]);
        Assert.Equal("Addon", audit.AfterState["Source"]);
        Assert.Equal("GOLDENSLIM", audit.Metadata["ModuleCode"]);
        Assert.DoesNotContain("audit proof", audit.AfterState.Values.OfType<string>());   // the free-text reason is not in the record
        Assert.Equal(0, await host.DeadLettersAsync());

        // Processing again finds nothing to do and writes no second row.
        Assert.Equal(0, await host.ProcessAuditOutboxAsync());
        Assert.Single(await host.AuditEventsAsync());
    }

    [Fact]
    public async Task K1_extending_a_module_records_the_expiry_date_before_and_after()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon, expiry: new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);

        var response = await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry",
            new { expiryDateUtc = new DateTimeOffset(2027, 6, 30, 0, 0, 0, TimeSpan.Zero), reason = (string?)null, rowVersion = row.RowVersion });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        await host.ProcessAuditOutboxAsync();
        var audit = Assert.Single(await host.AuditEventsAsync());
        Assert.Equal("UpdateTenantModuleEntitlementExpiryCommand", audit.RequestType);
        Assert.Equal(AuditOperation.Update, audit.Operation);
        Assert.Equal("2026-12-31T00:00:00.0000000+00:00", audit.BeforeState!["ExpiryDateUtc"]);
        Assert.Equal("2027-06-30T00:00:00.0000000+00:00", audit.AfterState!["ExpiryDateUtc"]);
    }

    [Fact]
    public async Task K1_every_entitlement_action_of_the_screen_is_delivered_none_dead_letters()
    {
        await using var host = await Host.StartAsync();

        var added = await host.PostAsync(Tenant, null, new { moduleCode = "CRM", source = "Addon", isEnabled = true, expiryDateUtc = (DateTimeOffset?)null, reason = (string?)null, rowVersion = (string?)null });
        Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync());
        var id = (await host.ListAsync(Tenant)).Single(r => r.ModuleCode == "CRM").PhysicalEntitlementId!.Value;
        async Task<string> Version() => (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == id).RowVersion!;

        var disabled = await host.PostAsync(Tenant, "disable", new { moduleCode = "CRM", physicalEntitlementId = id, reason = "audit proof", rowVersion = await Version() });
        Assert.True(disabled.IsSuccessStatusCode, await disabled.Content.ReadAsStringAsync());
        Assert.True((await host.PostAsync(Tenant, $"{id:D}/enable", await Version())).IsSuccessStatusCode);
        Assert.True((await host.PatchAsync(Tenant, $"{id:D}/expiry", new { expiryDateUtc = DateTimeOffset.UtcNow.AddDays(30), reason = (string?)null, rowVersion = await Version() })).IsSuccessStatusCode);

        Assert.Equal(4, await host.ProcessAuditOutboxAsync());
        var audits = await host.AuditEventsAsync();
        Assert.Equal(
            ["AddTenantModuleEntitlementCommand", "DisableTenantModuleEntitlementCommand", "EnableTenantModuleEntitlementCommand", "UpdateTenantModuleEntitlementExpiryCommand"],
            audits.Select(a => a.RequestType).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.All(audits, audit =>
        {
            Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
            Assert.Equal(Host.Administrator, audit.ActorId);
            Assert.Equal(id, audit.EntityId);
            Assert.NotNull(audit.AfterState);
        });
        Assert.Null(audits.Single(a => a.RequestType == "AddTenantModuleEntitlementCommand").BeforeState); // nothing existed before an add
        Assert.Equal(0, await host.DeadLettersAsync());
    }

    /// <summary>
    /// K2 — fail-closed. A signed-in administrator whose token carries no user id cannot be named, so the change is
    /// refused; and because the record and the business data share one transaction, the entitlement is unchanged.
    /// </summary>
    [Fact]
    public async Task K1_a_change_whose_actor_cannot_be_named_is_refused_and_the_entitlement_is_unchanged()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);

        // Refused BY THE AUDIT DOOR — not by request validation before the handler ran (that would prove nothing here).
        // The pipeline does not turn this refusal into a response, so the in-memory test server surfaces it as the
        // exception itself; through the real Api it is the Api's error response. Either way: refused, and why.
        string outcome;
        try
        {
            var response = await host.PostWithoutSubjectAsync(Tenant, "disable", new { moduleCode = "GOLDENSLIM", physicalEntitlementId = seeded.Id, reason = "audit proof", rowVersion = row.RowVersion });
            Assert.False(response.IsSuccessStatusCode, "a change with nobody to name was accepted");
            outcome = await response.Content.ReadAsStringAsync();
        }
        catch (Diten.Platform.Application.Features.Audit.TransactionOwnedAuditRefusedException refusal)
        {
            outcome = refusal.Message;
        }

        Assert.Contains("could not name who made it", outcome);
        var stored = await host.StoredAsync(seeded.Id);
        Assert.True(stored.IsEnabled);
        Assert.Equal(seeded.RowVersion, stored.RowVersion);
        Assert.Equal(0, await host.CountAsync("audit_outbox"));
        Assert.Equal(0, await host.ProcessAuditOutboxAsync());
        Assert.Empty(await host.AuditEventsAsync());
    }

    private sealed record Row(
        string ModuleCode,
        string DisplaySource,
        Guid? PhysicalEntitlementId,
        bool IsEnabled,
        DateTimeOffset? ExpiryDateUtc,
        string EffectiveAccess,
        bool IsProjectionRow,
        string? RowVersion,
        IReadOnlyList<string>? AllowedActions);

    private sealed record Envelope<T>(T? Data, bool IsSuccessful, IReadOnlyList<string>? Errors, string? Reason_Code);

    private sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-bl500-test";
        private const string Audience = "diten-platform-bl500-test";
        private const string Secret = "BL-500 tenant modules http round trip signing key, test only, 0123456789";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;

        public IMongoDatabase Database { get; }

        /// <summary>What the server logged as an error — a 500's cause, for a failure message.</summary>
        public System.Collections.Concurrent.ConcurrentQueue<string> Failures { get; } = new();
        public IMongoClient MongoClient => _mongo.Client;

        private Host(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            Database = database;
            var dbContext = new PlatformDbContext(mongo.Client, database);

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(new FailureLog(Failures));
                    // As production (Program.cs): a validation failure is a 400 problem carrying its reason_code.
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
                    // The production policy (Infrastructure DependencyInjection.AddInfrastructure).
                    services.AddAuthorization(options => options.AddPolicy("PlatformActor", policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireAssertion(context =>
                        {
                            var actorType = context.User.Claims
                                .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
                                ?.Value;
                            return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
                        });
                    }));
                    services.AddHttpContextAccessor();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();

                    // Real persistence, over the test-owned replica set.
                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<ITenantModuleEntitlementRepository, TenantModuleEntitlementRepository>();
                    services.AddScoped<IPlatformTransactionExecutor, PlatformTransactionExecutor>();
                    services.AddScoped<IEntitlementStateVersionRepository, EntitlementStateVersionRepository>();
                    services.AddScoped<AuditOutboxRepository>();
                    // As production wires it (WP-PLATFORM-AUDIT-INTX-01): the repository is only the STORE; the writer the
                    // handlers get is AddApplication's CanonicalTransactionalAuditOutboxWriter.
                    services.AddScoped<ITransactionalAuditOutboxStore>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());

                    // Not this file's subject.
                    services.AddSingleton<ITransactionalIntegrationEventWriter, SilentEvents>();
                    services.AddSingleton(GrantingQuota());
                    services.AddSingleton(Catalogue());
                    services.AddSingleton(CatalogueContract());
                    services.AddSingleton(Subscriptions());
                    services.AddSingleton(Plans());

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TenantModuleEntitlementsController).Assembly));
                        manager.FeatureProviders.Add(new OnlyController(typeof(TenantModuleEntitlementsController)));
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
            // The production indexes of the entitlement collection — the one-live-row-per-(tenant, module, source) rule
            // lives there, not in code.
            await PlatformSchemaManifest.For(SchemaProfile.Core)
                .Single(collection => collection.Name == "tenant_module_entitlements")
                .ApplyAsync(database, CancellationToken.None);
            return new Host(mongo, database);
        }

        public const string PlanModule = "PLANMOD";
        public const string BaselineModule = "TASKCENTER";
        public const string CoreModule = "COREMOD";
        public const string GoneModule = "GONEMOD";
        public static readonly Guid PlanId = Guid.Parse("50050050-0000-4000-8000-0000000000c3");

        public async Task<TenantModuleEntitlement> SeedAsync(
            Guid tenantId, string moduleCode, EntitlementSource source, bool enabled = true, DateTimeOffset? expiry = null)
        {
            var entitlement = new TenantModuleEntitlement
            {
                TenantId = tenantId,
                ModuleCode = moduleCode,
                Source = source,
                IsEnabled = enabled,
                ExpiryDateUtc = expiry,
                Reason = "seeded"
            };
            await Database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements").InsertOneAsync(entitlement);
            return entitlement;
        }

        public async Task<TenantModuleEntitlement> StoredAsync(Guid id) =>
            await Database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements").Find(x => x.Id == id).SingleAsync();

        /// <summary>One pass of the PRODUCTION outbox processor — the unit of work the hosted worker repeats.</summary>
        public Task<int> ProcessAuditOutboxAsync()
        {
            var context = new PlatformDbContext(MongoClient, Database);
            var tenantContext = new TenantContext();
            return new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxProcessor(
                new AuditOutboxRepository(context),
                new AuditEventRepository(Database, tenantContext),
                tenantContext,
                new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxPayloadMapper(),
                new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxWorkerOptions { BatchSize = 10, MaxAttempts = 5, InitialRetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(5) },
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Infrastructure.Services.Audit.AuditOutboxProcessor>.Instance).ProcessBatchAsync();
        }

        public async Task<IReadOnlyList<Diten.Platform.Domain.Entities.Audit.AuditEvent>> AuditEventsAsync() =>
            await Database.GetCollection<Diten.Platform.Domain.Entities.Audit.AuditEvent>("audit_events")
                .Find(FilterDefinition<Diten.Platform.Domain.Entities.Audit.AuditEvent>.Empty).ToListAsync();

        public Task<long> DeadLettersAsync() =>
            Database.GetCollection<BsonDocument>("audit_outbox").CountDocumentsAsync(new BsonDocument("Status", 5));

        /// <summary>A signed-in platform administrator whose token names no subject — a person nobody can name.</summary>
        public Task<HttpResponseMessage> PostWithoutSubjectAsync(Guid tenantId, string? suffix, object? body)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PlatformToken(withSubject: false));
            return client.PostAsync(Path(tenantId, suffix), JsonContent.Create(body, options: Json));
        }

        public Task<long> CountAsync(string collection) =>
            Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        public async Task<IReadOnlyList<Row>> ListAsync(Guid tenantId)
        {
            var response = await Client().GetAsync(Path(tenantId, null));
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
            return JsonSerializer.Deserialize<Envelope<List<Row>>>(body, Json)!.Data!;
        }

        public Task<HttpResponseMessage> PostAsync(Guid tenantId, string? suffix, object? body) =>
            Client().PostAsync(Path(tenantId, suffix), JsonContent.Create(body, options: Json));

        public Task<HttpResponseMessage> PatchAsync(Guid tenantId, string suffix, object body) =>
            Client().PatchAsync(Path(tenantId, suffix), JsonContent.Create(body, options: Json));

        public Task<HttpResponseMessage> DeleteAsync(Guid tenantId, string suffix, object body) =>
            Client().SendAsync(new HttpRequestMessage(HttpMethod.Delete, Path(tenantId, suffix))
            {
                Content = JsonContent.Create(body, options: Json)
            });

        public static async Task<(HttpStatusCode Status, string? Code, string Body)> ReadRefusalAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            string? code = null;
            if (body.Length > 0)
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("reason_code", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    code = value.GetString();
                }
            }

            return (response.StatusCode, code, body);
        }

        private static string Path(Guid tenantId, string? suffix) =>
            $"/api/platform/tenants/{tenantId:D}/commercial/module-entitlements" + (suffix is null ? string.Empty : "/" + suffix);

        private HttpClient Client()
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PlatformToken());
            return client;
        }

        public static readonly Guid Administrator = Guid.Parse("50050050-0000-4000-8000-0000000000d4");

        private static string PlatformToken(bool withSubject = true)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Email, "platform.admin@di10.test"),
                new("actor_type", "platform_admin")
            };
            if (withSubject)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, Administrator.ToString()));
            }

            var key = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience, claims, notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: key));
        }

        /// <summary>
        /// Every code is a catalogue module except <see cref="GoneModule"/> (it left the catalogue);
        /// <see cref="BaselineModule"/> is baseline and <see cref="CoreModule"/> is core.
        /// </summary>
        private static IModuleCatalogRepository Catalogue()
        {
            static ModuleCatalogItem? Record(string code) => string.Equals(code, GoneModule, StringComparison.OrdinalIgnoreCase)
                ? null
                : new ModuleCatalogItem
                {
                    ModuleCode = code,
                    ModuleName = code,
                    DisplayName = code,
                    IsBaseline = string.Equals(code, BaselineModule, StringComparison.OrdinalIgnoreCase),
                    IsCoreModule = string.Equals(code, CoreModule, StringComparison.OrdinalIgnoreCase)
                };

            var catalogue = new Mock<IModuleCatalogRepository>();
            catalogue.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string code, CancellationToken _) => Record(code));
            catalogue.Setup(x => x.GetByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> codes, CancellationToken _) =>
                    (IReadOnlyDictionary<string, ModuleCatalogItem>)codes
                        .Select(code => Record(code))
                        .OfType<ModuleCatalogItem>()
                        .ToDictionary(item => item.ModuleCode, StringComparer.OrdinalIgnoreCase));
            return catalogue.Object;
        }

        private static IPlatformCatalogContract CatalogueContract()
        {
            var contract = new Mock<IPlatformCatalogContract>();
            contract.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AssignableModuleInfo>());
            return contract.Object;
        }

        private static ITenantSubscriptionRepository Subscriptions()
        {
            var subscriptions = new Mock<ITenantSubscriptionRepository>();
            subscriptions.Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid tenantId, CancellationToken _) => new TenantSubscription { TenantId = tenantId, PlanId = PlanId });
            return subscriptions.Object;
        }

        private static ISubscriptionPlanRepository Plans()
        {
            var plans = new Mock<ISubscriptionPlanRepository>();
            plans.Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { IncludedModuleKeys = [PlanModule] });
            return plans.Object;
        }

        public async ValueTask DisposeAsync()
        {
            _server.Dispose();
            await _mongo.DisposeAsync();
        }
    }

    private sealed class FailureLog(System.Collections.Concurrent.ConcurrentQueue<string> sink) : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Logger(sink);
        public void Dispose() { }

        private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> sink) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Error;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel)) sink.Enqueue(formatter(state, exception) + " | " + exception);
            }
        }
    }

    private sealed class OnlyController(Type controller) : ControllerFeatureProvider
    {
        protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
    }

    private sealed class SilentEvents : ITransactionalIntegrationEventWriter
    {
        public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(
            IPlatformTransactionSession session, TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent =>
            Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(Guid.NewGuid(), @event.EventName, @event.EventVersion, Guid.NewGuid(), null, options.TenantId, "test", DateTimeOffset.UtcNow),
                @event));
    }

    /// <summary>The plan has room: every consume and release is granted. Limits have their own tests.</summary>
    private static IQuotaService GrantingQuota()
    {
        static Response<QuotaMutationDto> Granted(Guid tenantId) =>
            Response<QuotaMutationDto>.Success(new QuotaMutationDto(tenantId, QuotaKeys.ModulesMax, 1, 100, 1, true, null));

        var quota = new Mock<IQuotaService>();
        quota.Setup(x => x.TryConsumeEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TryConsumeQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPlatformTransactionSession _, TryConsumeQuotaRequest request, CancellationToken _) => Granted(request.TenantId));
        quota.Setup(x => x.ReleaseEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<ReleaseQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPlatformTransactionSession _, ReleaseQuotaRequest request, CancellationToken _) => Granted(request.TenantId));
        quota.Setup(x => x.RecalculateEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<RecalculateQuotaUsageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaStatusDto>.Fail("not measured here", 404));
        return quota.Object;
    }
}
