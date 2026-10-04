using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 5 (CT-WF-D2-FIX5), item 6 — the failure path of the look reset the rule handlers run
/// after a rule is saved. It is best effort and says so: the rule stays saved, the failure is logged once with what failed (the
/// template and the kind of exception — never the exception's own text, which can carry a connection string or a name), and a
/// shutdown (cancellation) is not swallowed as a failure.
/// </summary>
public sealed partial class WorkflowSlaEscalationTests
{
    /// <summary>Planted in the exception text: nothing the handlers log may carry it.</summary>
    private const string PersonalData = "alice@example.invalid";

    [Theory]
    [InlineData("listing the open instances")]
    [InlineData("resetting the look times")]
    public async Task A_failed_look_reset_leaves_the_saved_rule_and_is_logged_without_personal_data(string failingStep)
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-RESET-FAIL-CREATE"));
        await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);
        var (instances, tasks) = FailingSeams(f, failingStep, new TimeoutException("the store could not be reached for " + PersonalData));
        var log = new EntryLog<CreateSlaEscalationRuleHandler>();

        var created = await new CreateSlaEscalationRuleHandler(f.Templates, f.Rules, f.TenantContext, instances, tasks, log)
            .Handle(CreateRule(template.Id), CancellationToken.None);

        Assert.True(created.IsSuccessful);                 // a reset that fails cannot undo a rule that is already saved
        Assert.Equal(201, created.StatusCode);
        Assert.Single(f.Rules.Items);
        AssertLoggedOnceWithoutPersonalData(log, template.Id);
    }

    [Fact]
    public async Task A_failed_look_reset_after_a_publish_leaves_the_published_version_and_is_logged_without_personal_data()
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-RESET-FAIL-PUBLISH"));
        await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);
        var (instances, tasks) = FailingSeams(f, "resetting the look times", new TimeoutException("down: " + PersonalData));
        var log = new EntryLog<PublishWorkflowDefinitionHandler>();

        var published = await new PublishWorkflowDefinitionHandler(
                f.Templates, f.Versions, f.TenantContext, new FakeCurrentUserContext(), f.Rules, instances, tasks, log)
            .Handle(PublishCommand(template.Id), CancellationToken.None);

        Assert.True(published.IsSuccessful, published.ReasonCode);
        AssertLoggedOnceWithoutPersonalData(log, template.Id);
    }

    [Fact]
    public async Task A_cancelled_look_reset_is_not_swallowed_as_a_failure()
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-RESET-CANCELLED"));
        await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);
        var (instances, tasks) = FailingSeams(f, "resetting the look times", new OperationCanceledException());
        var log = new EntryLog<CreateSlaEscalationRuleHandler>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CreateSlaEscalationRuleHandler(f.Templates, f.Rules, f.TenantContext, instances, tasks, log)
                .Handle(CreateRule(template.Id), CancellationToken.None));

        Assert.Empty(log.Entries);                         // a shutdown is not "the reset failed"
    }

    private static void AssertLoggedOnceWithoutPersonalData<T>(EntryLog<T> log, Guid templateId)
    {
        var warning = Assert.Single(log.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(templateId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TimeoutException), warning.Message, StringComparison.Ordinal);
        Assert.Null(warning.Exception);                    // no exception object: a sink would write its text and its stack
        Assert.DoesNotContain(log.Entries, e => e.Message.Contains(PersonalData, StringComparison.Ordinal));
    }

    /// <summary>The two repositories the reset reads and writes, with the named step throwing <paramref name="failure"/>.</summary>
    private static (IWorkflowInstanceRepository Instances, IApprovalTaskRepository Tasks) FailingSeams(
        TestFixture f, string failingStep, Exception failure)
    {
        if (failingStep == "listing the open instances")
        {
            var instances = new Mock<IWorkflowInstanceRepository>();
            instances.Setup(x => x.ListOpenIdsByTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
            return (instances.Object, f.Tasks);
        }

        var tasks = new Mock<IApprovalTaskRepository>();
        tasks.Setup(x => x.ResetEscalationLooksAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        return (f.Instances, tasks.Object);
    }

    private sealed class EntryLog<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
