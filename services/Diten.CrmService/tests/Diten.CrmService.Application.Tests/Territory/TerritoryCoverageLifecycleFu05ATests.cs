using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.Territory.AccountAssignments.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>
/// MOD-0151 FU05A — CoverageSummary model lifecycle guard (pack §22.2a).
///
/// <para>The FU05 live smoke found that an assignment whose territory model had been deactivated kept reporting as
/// <c>current</c> coverage. These tests pin the two-gate rule (active model AND open assignment), the history/current
/// split, the <c>effectiveAt</c> semantics, and the boundaries the guard must not cross (no account mutation, no
/// hard delete, no auto-end).</para>
/// </summary>
public sealed class TerritoryCoverageLifecycleFu05ATests
{
    private static readonly Guid TenantA = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    // ---------------------------------------------------------------- current coverage

    [Fact]
    public async Task Active_model_with_active_assignment_is_current_coverage()
    {
        var fx = Fixture();

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.HasCurrentCoverage);
        Assert.Single(response.Data.CurrentAssignments);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("archived")]
    [InlineData("superseded")]
    [InlineData("draft")]
    [InlineData("review")]
    [InlineData("approved")]
    public async Task Non_active_model_with_active_assignment_is_not_current_coverage(string modelStatus)
    {
        var fx = Fixture();
        fx.Model.Status = modelStatus;

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.HasCurrentCoverage);
        Assert.Empty(response.Data.CurrentAssignments);
    }

    [Fact]
    public async Task Expired_model_window_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Model.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-1);

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Future_model_window_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Model.EffectiveFrom = DateTimeOffset.UtcNow.AddDays(30);
        fx.Model.EffectiveTo = DateTimeOffset.UtcNow.AddDays(400);

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Soft_deleted_model_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Model.IsDeleted = true;

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Ended_assignment_under_active_model_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Assignment.AssignmentStatus = "ended";
        fx.Assignment.EndedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Future_assignment_under_active_model_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Assignment.EffectiveFrom = DateTimeOffset.UtcNow.AddDays(10);

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Expired_assignment_under_active_model_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Assignment.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-1);

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Soft_deleted_assignment_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Assignment.IsDeleted = true;

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Only_the_active_model_projects_when_the_account_has_two_models()
    {
        var fx = Fixture();
        var archived = NewModel("archived");
        fx.Models.Items.Add(archived);
        fx.Assignments.Items.Add(NewAssignment(fx.AccountId, archived.Id, "ZONE-OLD", "Zone Old"));

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.True(response.Data!.HasCurrentCoverage);
        Assert.Single(response.Data.CurrentAssignments);
        Assert.Equal("ZONE-1", response.Data.CurrentAssignments[0].TerritoryNodeCode);
    }

    [Fact]
    public async Task Assignment_pointing_at_a_missing_model_is_not_current_coverage()
    {
        var fx = Fixture();
        fx.Models.Items.Clear();

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    // ---------------------------------------------------------------- history

    [Theory]
    [InlineData("inactive")]
    [InlineData("archived")]
    [InlineData("superseded")]
    public async Task Assignment_of_a_non_active_model_stays_visible_in_history(string modelStatus)
    {
        var fx = Fixture();
        fx.Model.Status = modelStatus;

        var coverage = await fx.Coverage.Handle(new(fx.AccountId), default);
        var history = await fx.History.Handle(new(fx.AccountId), default);

        Assert.False(coverage.Data!.HasCurrentCoverage);
        Assert.Single(history.Data!.Items);
        Assert.Equal(fx.Assignment.Id, history.Data.Items[0].Id);
    }

    [Fact]
    public async Task Ended_assignment_stays_visible_in_history()
    {
        var fx = Fixture();
        fx.Assignment.AssignmentStatus = "ended";
        fx.Assignment.EndedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var history = await fx.History.Handle(new(fx.AccountId), default);

        Assert.Single(history.Data!.Items);
        Assert.Equal("ended", history.Data.Items[0].AssignmentStatus);
    }

    [Fact]
    public async Task Deactivating_the_model_never_deletes_or_ends_the_assignment()
    {
        var fx = Fixture();
        var before = Snapshot(fx.Assignment);

        fx.Model.Status = "inactive";
        var coverage = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(coverage.Data!.HasCurrentCoverage);
        Assert.Single(fx.Assignments.Items);                       // no hard delete
        Assert.Equal(before, Snapshot(fx.Assignments.Items[0]));   // no auto-end, no status/date rewrite
        Assert.False(fx.Assignments.Items[0].IsDeleted);
    }

    // ---------------------------------------------------------------- effectiveAt

    [Fact]
    public async Task Past_effectiveAt_returns_current_when_model_and_assignment_were_both_effective_then()
    {
        var fx = Fixture();

        var response = await fx.Coverage.Handle(new(fx.AccountId, DateTimeOffset.UtcNow.AddDays(-5)), default);

        Assert.True(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Past_effectiveAt_returns_nothing_when_the_model_window_did_not_cover_it()
    {
        var fx = Fixture();
        fx.Model.EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-2);

        var response = await fx.Coverage.Handle(new(fx.AccountId, DateTimeOffset.UtcNow.AddDays(-5)), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Past_effectiveAt_returns_nothing_when_the_assignment_had_not_started_yet()
    {
        var fx = Fixture();

        var response = await fx.Coverage.Handle(new(fx.AccountId, DateTimeOffset.UtcNow.AddDays(-30)), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Model_status_is_evaluated_as_stored_so_a_deactivated_model_is_not_current_for_a_past_date_either()
    {
        // Status is not versioned in FU05A: deactivation removes the model from current coverage at every
        // effectiveAt. History remains the record of what happened.
        var fx = Fixture();
        fx.Model.Status = "inactive";

        var response = await fx.Coverage.Handle(new(fx.AccountId, DateTimeOffset.UtcNow.AddDays(-5)), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    [Fact]
    public async Task Omitted_effectiveAt_defaults_to_now()
    {
        var fx = Fixture();
        var before = DateTimeOffset.UtcNow;

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.InRange(response.Data!.EffectiveAt, before, DateTimeOffset.UtcNow);
    }

    // ---------------------------------------------------------------- account grid parity + boundaries

    [Fact]
    public async Task Account_list_current_territory_column_applies_the_same_guard()
    {
        var fx = Fixture();
        var accounts = new FakeAccountRepo();
        accounts.Items.Add(NewAccount(fx.AccountId));
        var handler = new GetAccountListHandler(
            TenantFactory.Tenant(TenantA), accounts, fx.Assignments, fx.Models);

        var withActiveModel = await handler.Handle(new GetAccountListQuery(null, 1, 25), default);
        Assert.Equal("ZONE-1", withActiveModel.Data!.Items[0].TerritoryNodeCode);

        fx.Model.Status = "archived";
        var withArchivedModel = await handler.Handle(new GetAccountListQuery(null, 1, 25), default);
        Assert.Null(withArchivedModel.Data!.Items[0].TerritoryNodeCode);
        Assert.Null(withArchivedModel.Data.Items[0].TerritoryNodeName);
    }

    [Fact]
    public async Task Coverage_query_never_mutates_the_account_or_creates_contact_coverage()
    {
        var fx = Fixture();
        var accountSnapshot = fx.Accounts.Accounts.Single();

        fx.Model.Status = "inactive";
        await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.Equal(accountSnapshot, fx.Accounts.Accounts.Single());
        // ITerritoryAccountReader has no write member and there is no ContactTerritoryAssignment aggregate —
        // the seam makes an account/contact mutation impossible to express here.
    }

    [Fact]
    public async Task Cross_tenant_model_does_not_satisfy_the_guard()
    {
        var fx = Fixture();
        fx.Model.TenantId = Guid.NewGuid();

        var response = await fx.Coverage.Handle(new(fx.AccountId), default);

        Assert.False(response.Data!.HasCurrentCoverage);
    }

    // ---------------------------------------------------------------- fixture

    private static (string Status, DateTimeOffset From, DateTimeOffset? To, DateTimeOffset? EndedAt, bool Deleted)
        Snapshot(AccountTerritoryAssignment a) => (a.AssignmentStatus, a.EffectiveFrom, a.EffectiveTo, a.EndedAt, a.IsDeleted);

    private static TerritoryModel NewModel(string status) => new()
    {
        TenantId = TenantA, ModelCode = $"TM-{status}", Name = $"Model {status}", Status = status,
        EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1), EffectiveTo = DateTimeOffset.UtcNow.AddYears(1)
    };

    private static AccountTerritoryAssignment NewAssignment(Guid accountId, Guid modelId, string code, string name) => new()
    {
        TenantId = TenantA, TerritoryModelId = modelId, AccountId = accountId, AccountCode = "ACC-1",
        AccountDisplayName = "Account One", TerritoryNodeId = Guid.NewGuid(), TerritoryNodeCode = code,
        TerritoryNodeName = name, AssignmentSource = "rule", AssignmentStatus = "active", ConflictPolicy = "block",
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10)
    };

    private static Domain.Entities.Account NewAccount(Guid id) => new()
    {
        Id = id, TenantId = TenantA, AccountCode = "ACC-1", AccountName = "Account One",
        AccountType = "hospital", Status = "active"
    };

    /// <summary>Minimal account store for the list-grid parity test (the other suites keep their own private copies).</summary>
    private sealed class FakeAccountRepo : Domain.Repositories.IAccountRepository
    {
        public List<Domain.Entities.Account> Items { get; } = [];

        public Task<Domain.Entities.Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted));

        public Task<Domain.Entities.Account?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.AccountCode == code && !a.IsDeleted));

        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId, CancellationToken ct)
            => Task.FromResult(Items.Any(a => a.TenantId == tenantId && !a.IsDeleted && a.AccountCode == code && a.Id != excludeId));

        public Task<(IReadOnlyList<Domain.Entities.Account> Items, long Total)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, CancellationToken ct)
        {
            var q = Items.Where(a => a.TenantId == tenantId && !a.IsDeleted).ToList();
            return Task.FromResult(((IReadOnlyList<Domain.Entities.Account>)q, (long)q.Count));
        }

        public Task<IReadOnlyList<Domain.Entities.Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Domain.Entities.Account>)Items
                .Where(a => a.TenantId == tenantId && a.ParentAccountId == parentId && !a.IsDeleted).ToList());

        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct)
            => Task.FromResult(false);

        public Task InsertAsync(Domain.Entities.Account account, CancellationToken ct) { Items.Add(account); return Task.CompletedTask; }

        public Task UpdateAsync(Domain.Entities.Account account, CancellationToken ct) => Task.CompletedTask;
    }

    private static FixtureState Fixture()
    {
        var model = NewModel("active");
        var accountId = Guid.NewGuid();
        var accounts = new FakeTerritoryAccountReader();
        accounts.Accounts.Add(new TerritoryAccountSnapshot(
            accountId, "ACC-1", "Account One", "hospital", null, "active", "TR", "IST", "BEY"));
        var models = new FakeTerritoryModelRepo(); models.Items.Add(model);
        var assignments = new FakeAccountTerritoryAssignmentRepo();
        var assignment = NewAssignment(accountId, model.Id, "ZONE-1", "Zone One");
        assignments.Items.Add(assignment);

        var tenant = TenantFactory.Tenant(TenantA);
        return new(
            model, assignment, accountId, accounts, assignments, models,
            new GetTerritoryCoverageSummaryHandler(tenant, accounts, assignments, models),
            new GetAccountTerritoryAssignmentHistoryHandler(tenant, accounts, assignments));
    }

    private sealed record FixtureState(
        TerritoryModel Model, AccountTerritoryAssignment Assignment, Guid AccountId,
        FakeTerritoryAccountReader Accounts, FakeAccountTerritoryAssignmentRepo Assignments,
        FakeTerritoryModelRepo Models,
        GetTerritoryCoverageSummaryHandler Coverage,
        GetAccountTerritoryAssignmentHistoryHandler History);
}
