using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 4 (CT-WF-D2-FIX4), item 3 — an SLA rule that is added, tightened or replaced is
/// seen at once by the tasks that were RESTING under the old rules. The sweep rests a task nothing could be done to for a day;
/// without this a rule written an hour later waited out that day.
/// </summary>
public sealed partial class WorkflowSlaEscalationTests
{
    [Fact]
    public async Task Adding_a_rule_forgets_the_look_time_of_the_templates_open_tasks_and_of_no_others()
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-RESET-CREATE"));
        var waiting = await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);
        var escalated = await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.Escalated, WorkflowInstanceStatus.Escalated);
        var closed = await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.Cancelled, WorkflowInstanceStatus.Cancelled);
        var otherTemplate = await SeedRestingAsync(f, Guid.NewGuid(), ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);

        var created = await new CreateSlaEscalationRuleHandler(f.Templates, f.Rules, f.TenantContext, f.Instances, f.Tasks)
            .Handle(CreateRule(template.Id), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Null(waiting.EscalationNextLookAtUtc);        // looked at again by the next sweep …
        Assert.Null(escalated.EscalationNextLookAtUtc);       // … an escalated task is open too
        Assert.NotNull(closed.EscalationNextLookAtUtc);       // a task under a closed instance is nobody's to look at
        Assert.NotNull(otherTemplate.EscalationNextLookAtUtc); // and another template's rules did not change
    }

    [Fact]
    public async Task Republishing_a_definition_forgets_the_look_time_of_the_templates_open_tasks_and_of_no_others()
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-RESET-PUBLISH"));
        var waiting = await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);
        var otherTemplate = await SeedRestingAsync(f, Guid.NewGuid(), ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);

        var published = await new PublishWorkflowDefinitionHandler(
                f.Templates, f.Versions, f.TenantContext, new FakeCurrentUserContext(), f.Rules, f.Instances, f.Tasks)
            .Handle(PublishCommand(template.Id), CancellationToken.None);

        Assert.True(published.IsSuccessful, published.ReasonCode);
        Assert.Null(waiting.EscalationNextLookAtUtc);
        Assert.NotNull(otherTemplate.EscalationNextLookAtUtc);
    }

    [Fact]
    public async Task The_container_fills_the_reset_seams_of_both_rule_handlers()
    {
        // Optional constructor seams default to "do nothing": only the container proves the registrations fill them.
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-RESET-DI"));
        var waiting = await SeedRestingAsync(f, template.Id, ApprovalTaskStatus.WaitingApproval, WorkflowInstanceStatus.Active);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkflowTemplateRepository>(f.Templates);
        services.AddSingleton<IWorkflowTemplateVersionRepository>(f.Versions);
        services.AddSingleton<ISlaEscalationRuleRepository>(f.Rules);
        services.AddSingleton<IWorkflowInstanceRepository>(f.Instances);
        services.AddSingleton<IApprovalTaskRepository>(f.Tasks);
        services.AddSingleton<ITenantContext>(f.TenantContext);
        services.AddSingleton<ICurrentUserContext>(new FakeCurrentUserContext());
        services.AddTransient<CreateSlaEscalationRuleHandler>();
        services.AddTransient<PublishWorkflowDefinitionHandler>();
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<CreateSlaEscalationRuleHandler>()
            .Handle(CreateRule(template.Id), CancellationToken.None);
        Assert.Null(waiting.EscalationNextLookAtUtc);

        waiting.EscalationNextLookAtUtc = DateTime.UtcNow.AddHours(12);
        await provider.GetRequiredService<PublishWorkflowDefinitionHandler>()
            .Handle(PublishCommand(template.Id), CancellationToken.None);
        Assert.Null(waiting.EscalationNextLookAtUtc);
    }

    private static PublishWorkflowDefinitionCommand PublishCommand(Guid templateId) =>
        new(templateId,
            new PublishWorkflowDefinitionRequest(
                DefinitionJson: "{}",
                SchemaVersion: "1.0",
                ExpressionVersion: "1.0",
                ExpectedTemplateVersion: null,
                ExpectedRowVersion: null,
                PublishReason: "D2-FIX4 reset"),
            Correlation);

    /// <summary>An overdue task that RESTED (a look time half a day away) under an instance of the template.</summary>
    private static async Task<ApprovalTask> SeedRestingAsync(
        TestFixture f, Guid templateId, ApprovalTaskStatus taskStatus, WorkflowInstanceStatus instanceStatus)
    {
        var instance = await f.Instances.CreateAsync(new WorkflowInstance
        {
            TenantId = f.TenantContext.TenantId,
            TemplateId = templateId,
            WorkflowTemplateId = templateId,
            ObjectType = "ResetTest",
            ObjectId = Guid.NewGuid().ToString("N"),
            ObjectRef = "reset|" + Guid.NewGuid().ToString("N"),
            Status = instanceStatus,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });
        return await f.Tasks.CreateAsync(new ApprovalTask
        {
            TenantId = f.TenantContext.TenantId,
            WorkflowInstanceId = instance.Id,
            Status = taskStatus,
            DueAt = DateTimeOffset.UtcNow.AddDays(-1),
            EscalationNextLookAtUtc = DateTime.UtcNow.AddHours(12)
        });
    }
}
