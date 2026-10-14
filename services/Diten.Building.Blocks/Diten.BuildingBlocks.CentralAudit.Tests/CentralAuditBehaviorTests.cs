using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.BuildingBlocks.CentralAudit.Tests;

/// <summary>
/// BL-501 — the shared behavior, through the real MediatR pipeline and the production registration. What it must do:
/// a SUCCEEDED marked command leaves exactly one event with the caller's tenant / company / actor and the declared
/// category / operation / entity; a failed one leaves none; an unmarked one leaves none; the request body is never copied.
/// </summary>
public sealed class CentralAuditBehaviorTests
{
    [Fact]
    public async Task A_succeeded_marked_command_leaves_exactly_one_event_with_the_callers_scope_and_its_declaration()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));

        var row = Assert.Single(harness.Store.Rows);
        Assert.Equal(Harness.Tenant, row.TenantId);
        Assert.Equal(Harness.Company, row.CompanyId);
        Assert.Equal(Harness.User, row.ActorId);
        Assert.Equal("Ayşe Denetçi", row.ActorDisplayName);
        Assert.Equal(CentralAuditActorTypes.TenantUser, row.ActorType);
        Assert.Equal("MasterData", row.Category);
        Assert.Equal("Create", row.Operation);
        Assert.Equal("Widget", row.EntityType);
        Assert.Equal(CreateWidgetCommand.CreatedId, row.EntityId);
        Assert.Equal("widget-catalog", row.SourceModule);
        Assert.Equal("Diten.TestService", row.SourceService);
        Assert.Equal(nameof(CreateWidgetCommand), row.RequestType);
        Assert.Equal(Harness.Correlation, row.CorrelationId);
        Assert.Equal(harness.Clock.GetUtcNow().UtcDateTime, row.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, row.OccurredAtUtc.Kind);
        Assert.Equal(CentralAuditOutboxStatus.Pending, row.Status);
        Assert.False(row.WrittenInCommandTransaction);
        Assert.NotEqual(Guid.Empty, row.Id);
    }

    [Theory]
    [InlineData(Ending.AnswersFailure)]
    [InlineData(Ending.Throws)]
    public async Task A_failed_marked_command_leaves_nothing(Ending ending)
    {
        using var harness = new Harness();

        if (ending == Ending.Throws)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendAsync(new CreateWidgetCommand(ending)));
        }
        else
        {
            Assert.False((await harness.SendAsync(new CreateWidgetCommand(ending))).IsSuccessful);
        }

        Assert.Empty(harness.Store.Rows);
    }

    [Fact]
    public async Task An_unmarked_command_leaves_nothing()
    {
        using var harness = new Harness();

        await harness.SendAsync(new RenameWidgetCommand());

        Assert.Empty(harness.Store.Rows);
    }

    /// <summary>s7 — the tenant is the caller's resolved scope, never a value the request carries.</summary>
    [Fact]
    public async Task The_event_is_filed_under_the_callers_tenant_not_a_tenant_named_in_the_request()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds, TenantIdInBody: Harness.OtherTenant));

        var row = Assert.Single(harness.Store.Rows);
        Assert.Equal(Harness.Tenant, row.TenantId);
        Assert.NotEqual(Harness.OtherTenant, row.TenantId);
    }

    [Fact]
    public async Task The_request_body_is_not_copied_into_the_event_or_the_platform_payload()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds, Secret: "do-not-copy-me-9f3a"));
        await harness.DispatchAsync();

        var (_, body) = Assert.Single(harness.Platform.Calls);
        Assert.DoesNotContain("do-not-copy-me-9f3a", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_resolved_scope_the_command_still_runs_and_the_missing_record_is_logged_as_an_error()
    {
        using var harness = new Harness(resolvedScope: false);

        var result = await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));

        Assert.True(result.IsSuccessful);
        Assert.Empty(harness.Store.Rows);
        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(nameof(CreateWidgetCommand), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_post_command_write_that_fails_does_not_fail_the_command_and_is_logged_with_tenant_and_event_id()
    {
        using var harness = new Harness();
        harness.Store.FailInserts = true;

        var result = await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));

        Assert.True(result.IsSuccessful);
        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Error
                                                   && e.Message.Contains(Harness.Tenant.ToString(), StringComparison.Ordinal)
                                                   && e.Message.Contains("EventId=", StringComparison.Ordinal));
    }

    // ── in the command's transaction ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Enlisted_in_a_committed_transaction_the_event_is_written_there_once_and_confirmed_when_the_command_succeeds()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.TransactionCommitsThenSucceeds));

        var row = Assert.Single(harness.Store.Rows);
        Assert.True(row.WrittenInCommandTransaction);
        Assert.Equal(CentralAuditOutboxStatus.Pending, row.Status);
        Assert.Equal(CreateWidgetCommand.CreatedId, row.EntityId);
    }

    /// <summary>s2 — the row lives and dies with the command's transaction.</summary>
    [Fact]
    public async Task Enlisted_in_a_transaction_that_aborts_the_event_is_gone_with_it()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.TransactionAbortsThenFails));

        Assert.Empty(harness.Store.Rows);
    }

    [Fact]
    public async Task Two_transactions_of_one_command_leave_one_event()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.TwoTransactionsThenSucceeds));

        Assert.Single(harness.Store.Rows);
    }

    /// <summary>
    /// A transaction can commit although the command then fails (it answered "not found" inside the transaction, or a
    /// later step of a multi-transaction command failed). The row is kept — nothing in the outbox is ever deleted — but a
    /// failed command has no central record: it is Abandoned and never sent.
    /// </summary>
    [Fact]
    public async Task A_command_that_fails_after_one_of_its_transactions_committed_leaves_an_abandoned_row_that_is_never_sent()
    {
        using var harness = new Harness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendAsync(new CreateWidgetCommand(Ending.TransactionCommitsThenFails)));

        Assert.Equal(CentralAuditOutboxStatus.Abandoned, Assert.Single(harness.Store.Rows).Status);
        harness.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, await harness.DispatchAsync());
        Assert.Empty(harness.Platform.Calls);
    }

    [Fact]
    public async Task A_nested_marked_command_enlists_its_own_event_and_the_outer_one_is_written_after()
    {
        using var harness = new Harness();

        await harness.SendAsync(new CreateWidgetCommand(Ending.SendsNestedThenSucceeds));

        Assert.Equal(2, harness.Store.Rows.Count);
        var nested = Assert.Single(harness.Store.Rows, r => r.RequestType == nameof(NestedWidgetCommand));
        Assert.True(nested.WrittenInCommandTransaction);
        Assert.Equal(NestedWidgetCommand.NestedId, nested.EntityId);
        var outer = Assert.Single(harness.Store.Rows, r => r.RequestType == nameof(CreateWidgetCommand));
        Assert.False(outer.WrittenInCommandTransaction);
        Assert.Equal(CreateWidgetCommand.CreatedId, outer.EntityId);
    }

    [Fact]
    public async Task A_row_its_command_never_confirmed_is_released_after_the_orphan_window_never_dropped()
    {
        using var harness = new Harness();
        var recorder = harness.Get<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        using (var scope = recorder.CreateScope())
        {
            // The process "dies" after the commit: Begin + Enlist + commit, no Complete.
            var rec = (CentralAuditRecorder)scope.ServiceProvider.GetService(typeof(CentralAuditRecorder))!;
            rec.Begin(new CreateWidgetCommand(Ending.Succeeds), nameof(CreateWidgetCommand));
            var session = FakeSession.Start();
            await rec.EnlistAsync(session.Handle, CreateWidgetCommand.CreatedId);
            session.Commit();
        }

        Assert.Equal(CentralAuditOutboxStatus.AwaitingCommand, Assert.Single(harness.Store.Rows).Status);
        Assert.Equal(0, await harness.DispatchAsync());
        Assert.Empty(harness.Platform.Calls);

        harness.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(1, await harness.DispatchAsync());
        Assert.Equal(CentralAuditOutboxStatus.Delivered, Assert.Single(harness.Store.Rows).Status);
    }
}

public sealed class CentralAuditTransactionAmbientTests
{
    [Fact]
    public async Task Outside_a_marked_command_the_static_enlistment_writes_nothing()
    {
        using var harness = new Harness();
        var session = FakeSession.Start();

        await CentralAuditTransaction.EnlistAsync(session.Handle);
        session.Commit();

        Assert.Empty(harness.Store.Rows);
    }
}

/// <summary>
/// A service whose every write runs in an enlisting transaction (Inventory): a success that enlisted nothing wrote nothing
/// — an idempotent replay — and must not file a second record; a command that owns no transaction says so with
/// ICentralAuditOutsideTransaction and is recorded after it succeeds.
/// </summary>
public sealed class RequireEnlistmentTests
{
    [Fact]
    public async Task A_success_that_enlisted_nothing_leaves_no_record()
    {
        using var harness = new Harness(requireEnlistment: true);

        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));

        Assert.Empty(harness.Store.Rows);
    }

    [Fact]
    public async Task An_enlisted_success_is_recorded_once()
    {
        using var harness = new Harness(requireEnlistment: true);

        await harness.SendAsync(new CreateWidgetCommand(Ending.TransactionCommitsThenSucceeds));

        Assert.Equal(CentralAuditOutboxStatus.Pending, Assert.Single(harness.Store.Rows).Status);
    }

    [Fact]
    public async Task A_command_declared_outside_a_transaction_is_recorded_after_it_succeeds()
    {
        using var harness = new Harness(requireEnlistment: true);

        await harness.SendAsync(new PublishWidgetTemplateCommand());

        var row = Assert.Single(harness.Store.Rows);
        Assert.False(row.WrittenInCommandTransaction);
        Assert.Equal("WidgetTemplate", row.EntityType);
    }
}