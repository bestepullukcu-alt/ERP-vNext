using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 4 — recurrence rule CRUD, through the real <see cref="TasksController"/> actions.
///
/// <para>There is no recurrence SCREEN this turn (deliberately out of scope), so the API is the only surface a
/// rule can be created from. That makes these actions the product, not an implementation detail — a rule that
/// cannot be created, listed and retired through them is a feature nobody can reach.</para>
/// </summary>
public sealed class TaskRecurrenceRuleCrudTests
{
    [Fact]
    public async Task A_rule_can_be_created_read_back_and_listed()
    {
        var harness = new Harness();

        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Weekly, interval: 2);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);

        var fetched = await harness.GetAsync(created.Data);
        Assert.True(fetched.IsSuccessful);
        // Enum as a STRING on the wire — the live Platform convention, and one an enum-as-number defect already
        // cost this module once.
        Assert.Equal("Weekly", fetched.Data!.Frequency);
        Assert.Equal(2, fetched.Data.Interval);

        var listed = await harness.ListAsync();
        Assert.Single(listed.Data!);
    }

    [Fact]
    public async Task A_rule_with_NO_frequency_is_refused()
    {
        /*
         * A schedule that never fires. Accepting it would put a live-looking row in the list that produces
         * nothing — which reads as a broken sweep rather than as the misconfigured rule it is.
         */
        var harness = new Harness();

        var created = await harness.CreateAsync(TaskRecurrenceFrequency.None, interval: 1);

        Assert.False(created.IsSuccessful);
        Assert.Equal(400, created.StatusCode);
        Assert.Equal(TaskReasonCodes.RecurrenceFrequencyRequired, created.ReasonCode);
    }

    [Fact]
    public async Task A_rule_that_ENDS_before_it_STARTS_is_refused()
    {
        var harness = new Harness();

        var created = await harness.CreateAsync(
            TaskRecurrenceFrequency.Daily,
            interval: 1,
            startsAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(created.IsSuccessful);
        Assert.Equal(TaskReasonCodes.RecurrenceWindowInvalid, created.ReasonCode);
    }

    [Fact]
    public async Task An_interval_below_one_is_normalised_rather_than_stored()
    {
        // Interval 0 would make every occurrence the same instant, so the schedule would fire once and never
        // again. One is "every period", which is what a caller sending 0 almost certainly meant.
        var harness = new Harness();

        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Daily, interval: 0);

        Assert.Equal(1, (await harness.GetAsync(created.Data)).Data!.Interval);
    }

    [Fact]
    public async Task A_rule_can_be_edited()
    {
        var harness = new Harness();
        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Daily, interval: 1);
        var current = (await harness.GetAsync(created.Data)).Data!;

        var updated = await harness.UpdateAsync(
            created.Data, current.Version, TaskRecurrenceFrequency.Monthly, interval: 3, isActive: false);

        Assert.True(updated.IsSuccessful);
        var after = (await harness.GetAsync(created.Data)).Data!;
        Assert.Equal("Monthly", after.Frequency);
        Assert.Equal(3, after.Interval);
        Assert.False(after.IsActive);
    }

    [Fact]
    public async Task An_edit_on_a_STALE_version_is_refused()
    {
        var harness = new Harness();
        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Daily, interval: 1);

        var updated = await harness.UpdateAsync(
            created.Data, expectedVersion: 99, TaskRecurrenceFrequency.Daily, interval: 1, isActive: true);

        Assert.False(updated.IsSuccessful);
        Assert.Equal(409, updated.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, updated.ReasonCode);
    }

    [Fact]
    public async Task An_edit_does_NOT_clear_the_last_generated_period()
    {
        /*
         * The duplicate this slice exists to prevent, arriving through the edit form instead of the sweep. If an
         * edit reset the claim, re-pointing a rule would let it regenerate a period it has already made.
         */
        var harness = new Harness();
        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Daily, interval: 1);
        var rule = harness.Rules.All.Single();
        rule.LastProcessInstanceId = "task-recurrence:abc:20260105T090000Z";
        rule.LastGeneratedAt = DateTimeOffset.UtcNow;

        await harness.UpdateAsync(created.Data, rule.Version, TaskRecurrenceFrequency.Weekly, 1, true);

        Assert.Equal("task-recurrence:abc:20260105T090000Z", harness.Rules.All.Single().LastProcessInstanceId);
    }

    [Fact]
    public async Task A_deleted_rule_leaves_the_list_and_stops_being_active()
    {
        /*
         * SOFT delete, and IsActive goes false with it. Both, because the sweep checks three independent reasons
         * a rule owes nothing and a retired rule that only stamped DeletedAt would keep producing work if any
         * future reader forgot one of them. The row itself survives — generated tasks point at it.
         */
        var harness = new Harness();
        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Daily, interval: 1);

        var deleted = await harness.DeleteAsync(created.Data);

        Assert.True(deleted.IsSuccessful);
        Assert.Empty((await harness.ListAsync()).Data!);
        Assert.Equal(404, (await harness.GetAsync(created.Data)).StatusCode);

        var stored = harness.Rules.All.Single();
        Assert.NotNull(stored.DeletedAt);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task A_PAUSED_rule_still_appears_in_the_list()
    {
        // Non-vacuity for the delete test, and a real requirement: a rule that vanished when it was switched off
        // could never be switched back on.
        var harness = new Harness();
        var created = await harness.CreateAsync(TaskRecurrenceFrequency.Daily, interval: 1, isActive: false);

        Assert.Single((await harness.ListAsync()).Data!);
        Assert.False((await harness.GetAsync(created.Data)).Data!.IsActive);
    }

    [Fact]
    public async Task Deleting_a_rule_that_is_already_gone_is_a_404()
    {
        var harness = new Harness();

        Assert.Equal(404, (await harness.DeleteAsync(Guid.NewGuid())).StatusCode);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        private readonly TasksController _controller;

        public Harness()
        {
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            Rules = new FakeTaskRecurrenceRuleRepository(tenant);
            var user = new FakeCurrentUserContext(TaskTestData.Me);

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(
                new RecurrenceCrudMediator(Rules, tenant, user), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public FakeTaskRecurrenceRuleRepository Rules { get; }

        public async Task<Response<Guid>> CreateAsync(
            TaskRecurrenceFrequency frequency,
            int interval,
            DateTimeOffset? startsAt = null,
            DateTimeOffset? endsAt = null,
            bool isActive = true)
            => Unwrap<Guid>(await _controller.CreateRecurrenceRule(
                new CreateTaskRecurrenceRuleRequest(
                    Name: "Tekrarlayan iş",
                    Frequency: frequency,
                    Interval: interval,
                    StartsAt: startsAt,
                    EndsAt: endsAt,
                    TaskTemplateId: null,
                    IsActive: isActive),
                CancellationToken.None));

        public async Task<Response<NoContent>> UpdateAsync(
            Guid id, int expectedVersion, TaskRecurrenceFrequency frequency, int interval, bool isActive)
            => Unwrap<NoContent>(await _controller.UpdateRecurrenceRule(
                id,
                new UpdateTaskRecurrenceRuleRequest(
                    Name: "Tekrarlayan iş",
                    Frequency: frequency,
                    Interval: interval,
                    StartsAt: null,
                    EndsAt: null,
                    TaskTemplateId: null,
                    IsActive: isActive,
                    ExpectedVersion: expectedVersion),
                CancellationToken.None));

        public async Task<Response<NoContent>> DeleteAsync(Guid id)
            => Unwrap<NoContent>(await _controller.DeleteRecurrenceRule(id, CancellationToken.None));

        public async Task<Response<TaskRecurrenceRuleDto>> GetAsync(Guid id)
            => Unwrap<TaskRecurrenceRuleDto>(await _controller.GetRecurrenceRule(id, CancellationToken.None));

        public async Task<Response<IReadOnlyList<TaskRecurrenceRuleDto>>> ListAsync()
            => Unwrap<IReadOnlyList<TaskRecurrenceRuleDto>>(
                await _controller.GetRecurrenceRules(CancellationToken.None));

        /// <summary>The controller copies the status code onto the result verbatim, so this asserts on the wire.</summary>
        private static Response<T> Unwrap<T>(IActionResult result)
            => result is NoContentResult
                ? Response<T>.Success(204, "corr")
                : (Response<T>)((ObjectResult)result).Value!;
    }

    private sealed class RecurrenceCrudMediator(
        FakeTaskRecurrenceRuleRepository rules,
        FakeTenantContext tenant,
        FakeCurrentUserContext user) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                CreateTaskRecurrenceRuleCommand command => (Task<TResponse>)(object)
                    new CreateTaskRecurrenceRuleHandler(rules, tenant, user).Handle(command, ct),
                UpdateTaskRecurrenceRuleCommand command => (Task<TResponse>)(object)
                    new UpdateTaskRecurrenceRuleHandler(rules, user).Handle(command, ct),
                DeleteTaskRecurrenceRuleCommand command => (Task<TResponse>)(object)
                    new DeleteTaskRecurrenceRuleHandler(rules, user).Handle(command, ct),
                GetTaskRecurrenceRuleListQuery query => (Task<TResponse>)(object)
                    new GetTaskRecurrenceRuleListHandler(rules).Handle(query, ct),
                GetTaskRecurrenceRuleByIdQuery query => (Task<TResponse>)(object)
                    new GetTaskRecurrenceRuleByIdHandler(rules).Handle(query, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
