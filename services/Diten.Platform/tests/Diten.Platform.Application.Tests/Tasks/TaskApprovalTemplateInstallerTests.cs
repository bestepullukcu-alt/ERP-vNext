using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 Phase 3 — the LAZY TEMPLATE INSTALLER inside <see cref="TaskApprovalService"/> (pack §12 K2,
/// charter DCP-004 §10.4 Binding A).
///
/// <para>The gate tests prove approval BLOCKS; the toggle tests prove it is switched on and off. Neither touches
/// the step in between: the first approval in a tenant has to bring a MOD-0023 template into existence. That
/// install runs inside a normal user request, on the very path where a duplicate would be permanent — a second
/// definition row would leave two "task approval" flows in the tenant's Workflow Designer with no way to tell
/// which one the engine will pick, and a tenant that adopted its neighbour's template would be a cross-tenant
/// data leak wearing a configuration hat. Every test below pins one of those failure modes shut.</para>
///
/// <para>MOD-0023's files are never modified: the doubles here re-create its create/publish/start contract,
/// including the tenant-unique template-code index that makes the install idempotent under a race.</para>
/// </summary>
public sealed class TaskApprovalTemplateInstallerTests
{
    private const string DefaultCode = "task-approval";

    // ── Installed once, not once per approval ─────────────────────────────────

    [Fact]
    public async Task A_second_approval_in_the_same_tenant_REUSES_the_template_instead_of_installing_another()
    {
        // The install is lazy, so it sits on the request path of every approval, not on a one-off startup worker.
        // If it did not look the code up first, the tenant would collect one definition per approval ever raised.
        var installer = new Installer();

        var first = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);
        var second = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);

        // ONE definition row, created and published exactly once…
        Assert.Single(installer.Templates.Rows);
        Assert.Single(installer.Mediator.Created);
        Assert.Single(installer.Mediator.Published);

        // …and both approvals ran on that same template, so the tenant has one flow, not two.
        var templateId = installer.Templates.Rows.Single().Id;
        Assert.Equal(2, installer.Mediator.Starts.Count);
        Assert.All(installer.Mediator.Starts, start => Assert.Equal(templateId, start.TemplateId));
    }

    // ── The race: the loser adopts, it does not duplicate and it does not throw ──

    [Fact]
    public async Task An_installer_that_LOSES_the_race_adopts_the_winners_template_rather_than_duplicating_it()
    {
        // The window is real: two first-ever approvals in a fresh tenant can both read "no template" before
        // either has committed. The double reproduces exactly that ordering — the by-code read answers "absent"
        // (the loser's snapshot), while the tenant-unique index already sees the winner's committed row and
        // refuses the insert. The loser must then re-read and adopt, because failing here would leave the
        // tenant's very first approval permanently un-startable.
        var installer = new Installer();
        var winner = installer.Templates.SeedPublished(TaskTestData.Tenant, DefaultCode);
        installer.Templates.BlindLookups = 1;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        // Converged on ONE template: no duplicate row, no exception, and the approval still started.
        Assert.NotNull(instanceId);
        Assert.Single(installer.Templates.Rows);
        Assert.Equal(winner.Id, Assert.Single(installer.Mediator.Starts).TemplateId);

        // It did try to create — otherwise this test would be proving nothing about the race.
        Assert.Single(installer.Mediator.Created);
    }

    [Fact]
    public async Task A_publish_that_lost_to_a_concurrent_publisher_still_counts_as_installed()
    {
        // Same convergence rule one step later: the template exists unpublished, our publish is refused because
        // somebody else published it first. An instance needs a PUBLISHED version, and there now is one — so
        // treating the refusal as a failure would block an approval whose template is perfectly usable.
        var installer = new Installer();
        var template = installer.Templates.SeedUnpublished(TaskTestData.Tenant, DefaultCode);
        installer.Mediator.PublishRefusedBecauseAnotherPublisherWon = true;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.NotNull(instanceId);
        Assert.Single(installer.Templates.Rows);
        Assert.Equal(template.Id, Assert.Single(installer.Mediator.Starts).TemplateId);
    }

    // ── Tenant scope ──────────────────────────────────────────────────────────

    [Fact]
    public async Task One_tenants_template_is_never_adopted_by_another_tenant()
    {
        // Template code is unique PER TENANT, so "task-approval" existing somewhere is not the same question as
        // it existing HERE. A lookup that missed the tenant filter would silently run tenant B's approvals
        // through tenant A's flow — A's approvers, A's steps — which is a cross-tenant leak, not a shortcut.
        var installer = new Installer();
        var tenantATemplate = installer.Templates.SeedPublished(TaskTestData.Tenant, DefaultCode);

        installer.Tenant.SetTenant(TaskTestData.OtherTenant);
        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.NotNull(instanceId);

        // B installed its OWN, under its own tenant id…
        Assert.Equal(2, installer.Templates.Rows.Count);
        var tenantBTemplate = installer.Templates.Rows.Single(row => row.TenantId == TaskTestData.OtherTenant);
        Assert.NotEqual(tenantATemplate.Id, tenantBTemplate.Id);

        // …and started the approval against it, not against A's.
        Assert.Equal(tenantBTemplate.Id, Assert.Single(installer.Mediator.Starts).TemplateId);
    }

    [Fact]
    public async Task The_tenant_scope_check_is_not_vacuous_the_double_WOULD_hand_over_the_other_tenants_template()
    {
        // Guards the test above from rotting into a tautology. If the double simply never returned another
        // tenant's row, the scoping assertion would pass no matter what the service did. Here the tenant filter
        // is the ONE thing removed — and the leak duly appears: B adopts A's template and installs nothing.
        var installer = new Installer();
        var tenantATemplate = installer.Templates.SeedPublished(TaskTestData.Tenant, DefaultCode);

        installer.Tenant.SetTenant(TaskTestData.OtherTenant);
        installer.Templates.IgnoreTenantScope = true;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.NotNull(instanceId);
        Assert.Equal(tenantATemplate.Id, Assert.Single(installer.Mediator.Starts).TemplateId);
        Assert.Single(installer.Templates.Rows);
    }

    // ── Configuration wins over the built-in default ──────────────────────────

    [Fact]
    public async Task A_configured_template_code_and_name_are_used_instead_of_the_built_in_default()
    {
        // The default flow is a day-one fallback, not a decision. A tenant that designed its own approval in the
        // Workflow Designer points configuration at it; if the code were hard-wired, that design would be
        // ignored and every task would still be approved by the fallback single step.
        var installer = new Installer(new TaskApprovalOptions
        {
            TemplateCode = "acme-task-signoff",
            TemplateName = "ACME task sign-off"
        });

        // The DEFAULT template also exists here, published and ready — it must be left alone.
        var defaultTemplate = installer.Templates.SeedPublished(TaskTestData.Tenant, DefaultCode);

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.NotNull(instanceId);

        var created = Assert.Single(installer.Mediator.Created);
        Assert.Equal("acme-task-signoff", created.TemplateCode);
        Assert.Equal("ACME task sign-off", created.Name);

        var configured = installer.Templates.Rows.Single(row => row.TemplateCode == "acme-task-signoff");
        Assert.Equal(configured.Id, Assert.Single(installer.Mediator.Starts).TemplateId);
        Assert.NotEqual(defaultTemplate.Id, installer.Mediator.Starts.Single().TemplateId);
    }

    [Fact]
    public async Task A_configured_template_that_already_exists_is_adopted_and_nothing_new_is_installed()
    {
        // The point of pointing configuration at a designed template is to USE it. Installing a fresh copy of
        // "their" code would overwrite the intent with the fallback flow.
        var installer = new Installer(new TaskApprovalOptions { TemplateCode = "acme-task-signoff" });
        var designed = installer.Templates.SeedPublished(TaskTestData.Tenant, "acme-task-signoff");

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.NotNull(instanceId);
        Assert.Empty(installer.Mediator.Created);
        Assert.Empty(installer.Mediator.Published);
        Assert.Equal(designed.Id, Assert.Single(installer.Mediator.Starts).TemplateId);
    }

    // ── Nothing usable to start on: null, never an exception ───────────────────

    [Fact]
    public async Task A_template_that_cannot_be_published_yields_NO_instance_so_the_task_stays_gated()
    {
        // An instance needs a PUBLISHED version (WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION). A draft-only template is
        // therefore not something to start on — and pretending otherwise would hand the caller an instance id
        // that never existed, which the fail-closed gate would then read as "approval outstanding" forever.
        var installer = new Installer();
        installer.Templates.SeedUnpublished(TaskTestData.Tenant, DefaultCode);
        installer.Mediator.PublishIsRefused = true;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        // Null, not an exception: the caller keeps the task with ApprovalRequired still true and retries later.
        Assert.Null(instanceId);
        Assert.Empty(installer.Mediator.Starts);
    }

    [Fact]
    public async Task A_start_refused_with_NO_ACTIVE_VERSION_yields_NO_instance_and_does_not_throw()
    {
        // The same shortfall as seen from MOD-0023's side of the seam: the template resolved, the start was
        // attempted, and the engine answered WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION. MOD-0024 must report "not
        // started" rather than surface the workflow module's failure into the user's create/edit.
        var installer = new Installer();
        installer.Mediator.StartFailureReasonCode = WorkflowReasonCodes.WorkflowTemplateNoActiveVersion;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.Null(instanceId);
        // It DID ask — this is the refusal path, not the "never tried" path above.
        Assert.Single(installer.Mediator.Starts);
    }

    [Fact]
    public async Task A_template_that_can_be_neither_found_nor_installed_yields_NO_instance()
    {
        // MOD-0023 refused the install for a reason that is not a lost race (no winner appears on the re-read),
        // so there is nothing to start. Same contract as every other shortfall: null, and the task survives.
        var installer = new Installer();
        installer.Mediator.CreateFailureReasonCode = WorkflowReasonCodes.PermissionDenied;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.Null(instanceId);
        Assert.Empty(installer.Mediator.Starts);
        Assert.Empty(installer.Templates.Rows);
    }

    [Fact]
    public async Task A_workflow_module_that_THROWS_yields_NO_instance_rather_than_losing_the_users_task()
    {
        // A workflow outage must not cost the user the work they already typed, and must not become an approval
        // bypass either. The only correct answer is "no instance": the task is kept and start stays gated.
        var installer = new Installer();
        installer.Mediator.Throws = true;

        var instanceId = await installer.Service.TryStartApprovalAsync(ApprovalTask(), CancellationToken.None);

        Assert.Null(instanceId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TaskItem ApprovalTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Needs sign-off",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        ApprovalRequired = true,
        ApprovalManagerUserId = TaskTestData.Rival,
        Version = 1
    };

    /// <summary>
    /// The service under test wired to the MOD-0023 doubles, with the tenant context shared between them exactly
    /// as a request scope shares it in production.
    /// </summary>
    private sealed class Installer
    {
        public Installer(TaskApprovalOptions? options = null)
        {
            Tenant = new FakeTenantContext(TaskTestData.Tenant);
            Templates = new FakeWorkflowTemplateStore(Tenant);
            Mediator = new FakeWorkflowMediator(Templates, Tenant);
            Service = new TaskApprovalService(
                Mediator,
                Templates,
                new UnreadWorkflowInstanceRepository(),
                new UnreadApprovalTaskRepository(),
                Tenant,
                new FakeCurrentUserContext(TaskTestData.Me),
                Options.Create(options ?? new TaskApprovalOptions()),
                NullLogger<TaskApprovalService>.Instance);
        }

        public FakeTenantContext Tenant { get; }
        public FakeWorkflowTemplateStore Templates { get; }
        public FakeWorkflowMediator Mediator { get; }
        public TaskApprovalService Service { get; }
    }
}

/// <summary>
/// MOD-0023's template store as MOD-0024 sees it: reads carry the tenant execution filter, and the tenant-unique
/// template-code index is modelled separately from the reads so the install race can be expressed deterministically
/// rather than with threads.
/// </summary>
internal sealed class FakeWorkflowTemplateStore(ITenantContext tenantContext) : IWorkflowTemplateRepository
{
    private readonly List<WorkflowTemplate> _rows = [];

    public IReadOnlyList<WorkflowTemplate> Rows => _rows;

    /// <summary>
    /// How many by-code reads must answer "absent" even though the row IS committed. This is the race window: the
    /// losing installer read before the winner committed, so its snapshot saw nothing.
    /// </summary>
    public int BlindLookups { get; set; }

    /// <summary>
    /// Drops the tenant filter. Used by ONE test, whose job is to prove the tenant-scope assertions are not
    /// vacuous — never as a convenience.
    /// </summary>
    public bool IgnoreTenantScope { get; set; }

    /// <summary>Seeds a template that is already published, i.e. usable for an instance start.</summary>
    public WorkflowTemplate SeedPublished(Guid tenantId, string templateCode)
    {
        var row = Seed(tenantId, templateCode);
        row.ActivePublishedVersionId = Guid.NewGuid();
        row.CurrentVersionId = row.ActivePublishedVersionId;
        row.Status = WorkflowTemplateStatus.Published;
        return row;
    }

    /// <summary>Seeds a draft-only template — it exists, but no instance can start on it yet.</summary>
    public WorkflowTemplate SeedUnpublished(Guid tenantId, string templateCode) => Seed(tenantId, templateCode);

    private WorkflowTemplate Seed(Guid tenantId, string templateCode)
    {
        var row = new WorkflowTemplate
        {
            TenantId = tenantId,
            TemplateCode = templateCode,
            Name = templateCode,
            Status = WorkflowTemplateStatus.Draft
        };

        _rows.Add(row);
        return row;
    }

    /// <summary>
    /// What the unique index sees: every COMMITTED row for the tenant, blind window or not. A database index does
    /// not read the caller's stale snapshot, which is exactly why the losing insert fails.
    /// </summary>
    public bool ViolatesUniqueTemplateCode(string templateCode)
        => _rows.Any(row => row.TemplateCode == templateCode
                            && !row.IsDeleted
                            && (IgnoreTenantScope || row.TenantId == tenantContext.TenantId));

    public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default)
    {
        if (BlindLookups > 0)
        {
            BlindLookups--;
            return Task.FromResult<WorkflowTemplate?>(null);
        }

        return Task.FromResult(_rows.FirstOrDefault(
            row => row.TemplateCode == templateCode
                   && !row.IsDeleted
                   && (IgnoreTenantScope || row.TenantId == tenantContext.TenantId)));
    }

    public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_rows.FirstOrDefault(
            row => row.Id == id
                   && !row.IsDeleted
                   && (IgnoreTenantScope || row.TenantId == tenantContext.TenantId)));

    public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default)
    {
        _rows.Add(template);
        return Task.FromResult(template);
    }

    public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WorkflowTemplate>>(
            _rows.Where(row => !row.IsDeleted && row.TenantId == tenantContext.TenantId).ToList());

    public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not edit MOD-0023 templates directly.");
}

/// <summary>
/// The MOD-0023 command surface MOD-0024 is allowed to use, reproduced at the mediator seam: create (with the
/// tenant-unique code guard the real handler applies), publish, and instance start. Anything else is a boundary
/// violation and fails loudly.
/// </summary>
internal sealed class FakeWorkflowMediator(FakeWorkflowTemplateStore templates, ITenantContext tenantContext)
    : IMediator
{
    public List<CreateWorkflowDefinitionRequest> Created { get; } = [];
    public List<Guid> Published { get; } = [];
    public List<StartWorkflowInstanceRequest> Starts { get; } = [];

    /// <summary>Forces the create to fail for a reason that is NOT a lost race.</summary>
    public string? CreateFailureReasonCode { get; set; }

    /// <summary>The publish is refused and nobody else published either — nothing is startable.</summary>
    public bool PublishIsRefused { get; set; }

    /// <summary>The publish is refused because a concurrent publisher already succeeded.</summary>
    public bool PublishRefusedBecauseAnotherPublisherWon { get; set; }

    /// <summary>MOD-0023 accepts the start request and then refuses it with this reason code.</summary>
    public string? StartFailureReasonCode { get; set; }

    /// <summary>The workflow module is unreachable — every command throws.</summary>
    public bool Throws { get; set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        if (Throws)
        {
            throw new InvalidOperationException("MOD-0023 is unreachable.");
        }

        return request switch
        {
            CreateWorkflowDefinitionCommand create => Task.FromResult((TResponse)Create(create)),
            PublishWorkflowDefinitionCommand publish => Task.FromResult((TResponse)Publish(publish)),
            StartWorkflowInstanceCommand start => Task.FromResult((TResponse)Start(start)),
            _ => throw new NotSupportedException(
                $"MOD-0024 must not send {request.GetType().Name} to MOD-0023.")
        };
    }

    private object Create(CreateWorkflowDefinitionCommand command)
    {
        Created.Add(command.Request);

        if (CreateFailureReasonCode is not null)
        {
            return Response<WorkflowDefinitionDetailDto>.Fail(
                "refused", 400, CreateFailureReasonCode, command.CorrelationId);
        }

        // The real handler's tenant-scoped duplicate guard. Modelled off the committed rows, not the caller's
        // read, so a loser of the install race is refused here even though its own lookup said "absent".
        if (templates.ViolatesUniqueTemplateCode(command.Request.TemplateCode))
        {
            return Response<WorkflowDefinitionDetailDto>.Fail(
                $"A workflow definition with code '{command.Request.TemplateCode}' already exists.",
                409,
                WorkflowReasonCodes.DuplicateTemplateCode,
                command.CorrelationId);
        }

        var row = new WorkflowTemplate
        {
            TenantId = tenantContext.TenantId,
            TemplateCode = command.Request.TemplateCode,
            Name = command.Request.Name,
            Description = command.Request.Description,
            Status = WorkflowTemplateStatus.Draft
        };

        templates.CreateAsync(row).GetAwaiter().GetResult();

        return Response<WorkflowDefinitionDetailDto>.Success(
            new WorkflowDefinitionDetailDto(
                row.Id, row.TemplateCode, row.Name, row.Description, row.Status.ToString(),
                row.ActivePublishedVersionId, row.CurrentVersionId, row.CreatedAt),
            201,
            command.CorrelationId);
    }

    private object Publish(PublishWorkflowDefinitionCommand command)
    {
        Published.Add(command.TemplateId);

        var row = templates.Rows.FirstOrDefault(t => t.Id == command.TemplateId);

        if (PublishRefusedBecauseAnotherPublisherWon && row is not null)
        {
            // The winner's publish already landed; ours is refused as a conflict.
            row.ActivePublishedVersionId = Guid.NewGuid();
            row.CurrentVersionId = row.ActivePublishedVersionId;
            row.Status = WorkflowTemplateStatus.Published;
        }

        if (PublishIsRefused || PublishRefusedBecauseAnotherPublisherWon || row is null)
        {
            return Response<PublishWorkflowDefinitionResponse>.Fail(
                "refused", 409, WorkflowReasonCodes.WorkflowTemplatePublishConflict, command.CorrelationId);
        }

        row.ActivePublishedVersionId = Guid.NewGuid();
        row.CurrentVersionId = row.ActivePublishedVersionId;
        row.Status = WorkflowTemplateStatus.Published;

        return Response<PublishWorkflowDefinitionResponse>.Success(
            new PublishWorkflowDefinitionResponse(
                row.Id, row.ActivePublishedVersionId!.Value, 1, true,
                row.Status.ToString(), DateTime.UtcNow, "tester", command.CorrelationId),
            200,
            command.CorrelationId);
    }

    private object Start(StartWorkflowInstanceCommand command)
    {
        Starts.Add(command.Request);

        if (StartFailureReasonCode is not null)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "refused", 409, StartFailureReasonCode, command.CorrelationId);
        }

        return Response<StartWorkflowInstanceResponse>.Success(
            new StartWorkflowInstanceResponse(
                WorkflowInstanceId: Guid.NewGuid(),
                TemplateId: command.Request.TemplateId ?? Guid.Empty,
                TemplateVersionId: Guid.NewGuid(),
                ApprovalTaskId: Guid.NewGuid(),
                AssignmentSnapshotId: Guid.NewGuid(),
                ObjectRef: command.Request.ObjectRef ?? string.Empty,
                Status: nameof(WorkflowInstanceStatus.Active),
                CurrentStage: "approve",
                CurrentStep: "approve",
                StartedAt: DateTimeOffset.UtcNow,
                DueAt: command.Request.DueAt,
                CorrelationId: command.CorrelationId),
            201,
            command.CorrelationId);
    }

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

/// <summary>Installing a template must not read workflow instances; a read here is a failing test, not silence.</summary>
internal sealed class UnreadWorkflowInstanceRepository : IWorkflowInstanceRepository
{
    public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0023 instances.");

    public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read instances.");

    public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read instances.");

    public Task<WorkflowInstance?> GetLatestByObjectRefAsync(
        string objectRef, string objectType, string objectId, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read instances.");

    public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read instances.");

    public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0023 instances.");
}

/// <summary>Same boundary for approval tasks: the installer has no business reading them.</summary>
internal sealed class UnreadApprovalTaskRepository : IApprovalTaskRepository
{
    public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0023 approval tasks.");

    public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read approval tasks.");

    public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read approval tasks.");

    public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read approval tasks.");

    public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(
        Guid workflowInstanceId, CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read approval tasks.");

    public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
        => throw new NotSupportedException("The template installer must not read approval tasks.");

    public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0023 approval tasks.");
}
