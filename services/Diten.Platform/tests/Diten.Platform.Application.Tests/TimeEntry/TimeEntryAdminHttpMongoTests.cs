using System.Net;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1a — tenant isolation, the category catalogue, settings and the per-legal-entity timer switch
/// (pack §17 T-18, T-19, T-20; §13 "Unauthorized").
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimeEntryAdminHttpMongoTests : TimeEntryScenario
{
    public TimeEntryAdminHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    // ── Tenant isolation (the same user id in two tenants must still see two different worlds) ──────────────────

    [Fact]
    public async Task The_same_user_id_in_another_tenant_sees_none_of_this_tenants_week()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 120, TaskA))).Status);

        var other = await GetWeekAsync(CurrentWeek, PersonToken(OtherTenant));

        Assert.Equal(HttpStatusCode.OK, other.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, other.Data.GetProperty("weekId").ValueKind);
        Assert.Equal(0, other.Data.GetProperty("totalMinutes").GetInt32());
        Assert.Empty(other.Data.GetProperty("entries").EnumerateArray());
        Assert.True(other.Data.GetProperty("editable").GetBoolean());
    }

    [Fact]
    public async Task Another_tenants_approver_and_time_admin_get_404_with_no_metadata()
    {
        var weekId = await SubmittedWeekAsync();

        // Same approver and admin user ids, other tenant — the tenant boundary, not the person, decides.
        var approval = await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(Manager, OtherTenant));
        // The person holds no seat in the other tenant: the reopen there cannot even name them.
        var reopen = await ReopenAsync(Person, "2026-W30", 0, "cross-tenant probe", AdminToken(OtherTenant));

        Assert.Equal(HttpStatusCode.NotFound, approval.Status);
        Assert.DoesNotContain(weekId.ToString(), approval.Body);
        Assert.Equal(HttpStatusCode.NotFound, reopen.Status);
        Assert.Empty(await StoredWeeksAsync(OtherTenant));
        Assert.Equal(0, (await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(Manager, OtherTenant)))
            .Data.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Without_the_permission_every_route_is_403()
    {
        var nobody = Host.Token(Person, Tenant);

        Assert.Equal(HttpStatusCode.Forbidden, (await GetWeekAsync(CurrentWeek, nobody)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SaveAsync(0, CurrentWeek, nobody, Row(Monday, 60, TaskA))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Host.GetAsync("/api/v1/time-entry/approvals", nobody)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Host.GetAsync("/api/v1/time-entry/settings", nobody)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Host.PostAsync("/api/v1/time-entry/categories/install-recommended", nobody)).Status);
        Assert.Empty(await StoredWeeksAsync());
    }

    // ── T-18 — categories ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Categories_are_created_deactivated_not_deleted_and_a_duplicate_code_is_409()
    {
        var created = await Host.PostAsync("/api/v1/time-entry/categories", AdminToken(),
            new { code = "QUALITY_REVIEW", labelText = "Kalite gözden geçirme", description = (string?)null, countsAsWork = true, sortOrder = 5 });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = created.Data.GetProperty("id").GetGuid();

        var duplicate = await Host.PostAsync("/api/v1/time-entry/categories", AdminToken(),
            new { code = "QUALITY_REVIEW", labelText = "again", countsAsWork = true, sortOrder = 6 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.Status);
        Assert.Equal(TimeEntryReasonCodes.CategoryCodeDuplicate, duplicate.ReasonCode);

        var renamedCode = await Host.PutAsync($"/api/v1/time-entry/categories/{id}", AdminToken(),
            new { expectedVersion = 1, code = "OTHER_CODE", labelText = "x", countsAsWork = true, sortOrder = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, renamedCode.Status);
        Assert.Equal(TimeEntryReasonCodes.CategoryCodeImmutable, renamedCode.ReasonCode);

        // A row using it, then retire it: the row keeps resolving, a NEW row is refused, nothing is deleted.
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, category: "QUALITY_REVIEW"))).Status);
        var deactivated = await Host.PostAsync($"/api/v1/time-entry/categories/{id}/deactivate", AdminToken(), new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, deactivated.Status);
        Assert.False(deactivated.Data.GetProperty("isActive").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 90, category: "QUALITY_REVIEW"))).Status);
        var newUse = await SaveFreshAsync(CurrentWeek, Row(Monday, 90, category: "QUALITY_REVIEW"), Row(Monday.AddDays(1), 30, category: "QUALITY_REVIEW"));
        Assert.Equal(HttpStatusCode.BadRequest, newUse.Status); // a retired category does not spread to a new day
        Assert.Equal(TimeEntryReasonCodes.CategoryInactive, newUse.ReasonCode);

        var stored = await Collection<WorkCategory>(PlatformCollections.TimeEntryWorkCategories)
            .Find(c => c.Id == id).SingleAsync();
        Assert.False(stored.IsDeleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Leave_and_absence_cannot_be_categories()
    {
        var leave = await Host.PostAsync("/api/v1/time-entry/categories", AdminToken(),
            new { code = "LEAVE", labelText = "İzin", countsAsWork = false, sortOrder = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, leave.Status);
        Assert.Equal(TimeEntryReasonCodes.CategoryCodeReserved, leave.ReasonCode);
    }

    [Fact]
    public async Task The_recommended_set_installs_by_resource_key_and_a_second_install_changes_nothing()
    {
        var first = await Host.PostAsync("/api/v1/time-entry/categories/install-recommended", AdminToken());
        var second = await Host.PostAsync("/api/v1/time-entry/categories/install-recommended", AdminToken());

        Assert.Equal(HttpStatusCode.OK, first.Status);
        // Five recommended codes; ADMINISTRATION is already the tenant's own (seeded), so four are installed.
        Assert.Equal(4, first.Data.GetProperty("installed").GetArrayLength());
        Assert.Equal(["ADMINISTRATION"], first.Data.GetProperty("alreadyPresent").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(0, second.Data.GetProperty("installed").GetArrayLength());
        var list = await Host.GetAsync("/api/v1/time-entry/categories", AdminToken());
        var training = list.Data.EnumerateArray().Single(c => c.GetProperty("code").GetString() == "TRAINING");
        Assert.Equal("TimeEntry.Category.TRAINING", training.GetProperty("labelResourceKey").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, training.GetProperty("labelText").ValueKind);
        // ADMINISTRATION was already the tenant's own (seeded): left exactly as it was.
        var administration = list.Data.EnumerateArray().Single(c => c.GetProperty("code").GetString() == "ADMINISTRATION");
        Assert.Equal("ADMINISTRATION", administration.GetProperty("labelText").GetString());
    }

    [Fact]
    public async Task People_pick_from_active_categories_only_and_the_manager_sees_the_retired_ones_too()
    {
        await SeedCategoryAsync(Tenant, "RETIRED", active: false);

        var picker = await Host.GetAsync("/api/v1/time-entry/categories", PersonToken());
        var manager = await Host.GetAsync("/api/v1/time-entry/categories/manage", AdminToken());
        var notManager = await Host.GetAsync("/api/v1/time-entry/categories/manage", PersonToken());

        Assert.DoesNotContain(picker.Data.EnumerateArray(), c => c.GetProperty("code").GetString() == "RETIRED");
        Assert.Contains(picker.Data.EnumerateArray(), c => c.GetProperty("code").GetString() == Category);
        Assert.Contains(manager.Data.EnumerateArray(), c => c.GetProperty("code").GetString() == "RETIRED");
        Assert.Equal(HttpStatusCode.Forbidden, notManager.Status);
    }

    // ── F5 — a task the person cannot read is exactly as absent as one that does not exist ─────────────────────

    [Fact]
    public async Task A_task_the_person_cannot_read_is_refused_with_the_same_answer_as_a_task_that_does_not_exist()
    {
        var hidden = Guid.NewGuid();
        await SeedTaskAsync(Tenant, hidden, assignee: Stranger, creator: Stranger);

        var unreadable = await SaveAsync(0, CurrentWeek, null, Row(Monday, 60, hidden));
        var missing = await SaveAsync(0, CurrentWeek, null, Row(Monday, 60, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, unreadable.Status);
        Assert.Equal(missing.Status, unreadable.Status);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, unreadable.ReasonCode);
        Assert.Equal(missing.ReasonCode, unreadable.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());

        // Non-vacuity: the same task becomes usable the moment the person may read it (a watcher is a data leg).
        await Collection<Domain.Entities.Tasks.TaskWatcher>(PlatformCollections.TaskWatchers).InsertOneAsync(
            new Domain.Entities.Tasks.TaskWatcher { TenantId = Tenant, TaskItemId = hidden, UserId = Person });
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(0, CurrentWeek, null, Row(Monday, 60, hidden))).Status);
    }

    // ── T-20 / D12 — the timer switch: no row = off, on needs a reason ──────────────────────────────────────────

    [Fact]
    public async Task No_switch_row_means_off_and_switching_on_requires_a_reason()
    {
        var none = await Host.GetAsync("/api/v1/time-entry/settings/legal-entities", AdminToken());
        Assert.Equal(0, none.Data.GetArrayLength());

        var bare = await Host.PutAsync($"/api/v1/time-entry/settings/legal-entities/{LegalEntity}", AdminToken(),
            new { expectedVersion = 0, timerEnabled = true, reason = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, bare.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerSwitchReasonRequired, bare.ReasonCode);
        Assert.Equal(0, await Collection<LegalEntityTimeSetting>(PlatformCollections.TimeEntryLegalEntitySettings)
            .CountDocumentsAsync(s => s.TenantId == Tenant));

        var on = await Host.PutAsync($"/api/v1/time-entry/settings/legal-entities/{LegalEntity}", AdminToken(),
            new { expectedVersion = 0, timerEnabled = true, reason = "TR entity: legal basis recorded in the DPIA" });
        Assert.Equal(HttpStatusCode.OK, on.Status);
        Assert.True(on.Data.GetProperty("timerEnabled").GetBoolean());
        var version = on.Data.GetProperty("version").GetInt32();

        // F11 — a switch written from a stale read is refused, not silently applied.
        var stale = await Host.PutAsync($"/api/v1/time-entry/settings/legal-entities/{LegalEntity}", AdminToken(),
            new { expectedVersion = 0, timerEnabled = false, reason = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerSwitchConcurrencyConflict, stale.ReasonCode);

        var off = await Host.PutAsync($"/api/v1/time-entry/settings/legal-entities/{LegalEntity}", AdminToken(),
            new { expectedVersion = version, timerEnabled = false, reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, off.Status);
        var row = await Collection<LegalEntityTimeSetting>(PlatformCollections.TimeEntryLegalEntitySettings)
            .Find(s => s.TenantId == Tenant).SingleAsync();
        Assert.False(row.TimerEnabled);
        Assert.Equal(PoolAdmin, row.ChangedByUserId);

        // Manual entry works whatever the switch says.
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
    }

    [Fact]
    public async Task The_time_admin_pool_must_be_a_position_of_this_tenant()
    {
        var foreign = await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = 0, timeAdminPoolPositionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.Status);
        Assert.Equal(TimeEntryReasonCodes.SettingsPositionNotFound, foreign.ReasonCode);

        await SetPoolAsync(PoolSeat);
        var read = await Host.GetAsync("/api/v1/time-entry/settings", AdminToken());
        Assert.Equal(PoolSeat, read.Data.GetProperty("timeAdminPoolPositionId").GetGuid());
    }
}
