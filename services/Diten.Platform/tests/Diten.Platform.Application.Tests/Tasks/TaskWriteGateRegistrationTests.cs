using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.TimeEntry;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX2 — the request handlers as PRODUCTION wires them (<c>AddApplication</c>: the real MediatR
/// registration and the real service registrations), not as the HTTP suite's router builds them by hand. An OPTIONAL
/// dependency the container does not know is passed as null without a word — for the accept handler a null read rule
/// is a 404 on every accept — so each one must be registered.
///
/// <para>FIX3 (item 9) — and not only REGISTERED: the container is BUILT from those registrations (with in-memory stores
/// standing where Infrastructure would) and an accept is sent through MediatR, the whole pipeline in front of it. A
/// registration that resolved to nothing, or a pipeline that could not be built, would fail here, not on the first live
/// accept.</para>
/// </summary>
public sealed class TaskWriteGateRegistrationTests
{
    [Fact]
    public void AddApplication_wires_the_request_handlers_with_their_read_rule_request_service_and_write_gate()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<AcceptTaskItemCommand, Response<NoContent>>)
                                       && d.ImplementationType == typeof(AcceptTaskItemHandler));
        foreach (var required in new[] { typeof(ITaskReadAccessPolicy), typeof(ITaskRequestService), typeof(ITaskWriteGate) })
        {
            Assert.Contains(services, d => d.ServiceType == required);
        }

        foreach (var handler in new[] { typeof(AcceptTaskItemHandler), typeof(ReturnTaskItemHandler), typeof(CreateTaskItemHandler) })
        {
            foreach (var optional in handler.GetConstructors().Single().GetParameters().Where(p => p.HasDefaultValue))
            {
                Assert.True(services.Any(d => d.ServiceType == optional.ParameterType),
                    $"{handler.Name}: optional {optional.ParameterType.Name} {optional.Name} is not registered — it would be null in production");
            }
        }
    }

    /// <summary>MUTATION TARGET (the production registrations of the read rule, the request service and the write gate,
    /// and the accept handler's use of them). The person a request is addressed to accepts it through the BUILT
    /// container's MediatR: the task becomes theirs, filed under their unit.</summary>
    [Fact]
    public async Task The_built_container_accepts_a_request_sent_through_MediatR()
    {
        var asked = TaskTestData.Me;
        var sender = TaskTestData.Other;
        var unit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "PL-HQ", Name = "PL", LegalEntityId = Guid.NewGuid(),
            Status = OrgUnitStatus.Active, OrgUnitType = OrgUnitType.HQ
        };
        var position = new Position
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "PL-QA-1", Name = "QA", OrganizationUnitId = unit.Id,
            Status = PositionStatus.Active
        };
        var seat = new PositionAssignment
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, PositionId = position.Id, UserId = asked,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
        };
        var request = new TaskItem
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Title = "Talep", Lifecycle = TaskLifecycle.Open,
            AssignmentTarget = TaskAssignmentTarget.Offered, OfferedToUserId = asked, IsRequest = true,
            CreatedByUserId = sender, OrganizationUnitId = Guid.NewGuid(), CreatedBy = "fix3"
        };
        var tasks = new FakeTaskItemRepository(request);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        // Where Infrastructure and the request pipeline would stand — the rules above them are the production ones.
        services.AddSingleton<ITaskItemRepository>(tasks);
        services.AddSingleton<ITaskAssignmentRepository>(new FakeTaskAssignmentRepository());
        services.AddSingleton<ITaskWatcherRepository>(new FakeTaskWatcherRepository());
        services.AddSingleton<ITaskNotificationService>(new FakeTaskNotificationService());
        services.AddSingleton<IOrganizationUnitRepository>(new FakeOrganizationUnitRepository(unit));
        services.AddSingleton<IPositionRepository>(new FakePositionRepository(position));
        services.AddSingleton<IPositionAssignmentRepository>(new FakePositionAssignmentRepository(seat));
        services.AddSingleton<ITaskNotificationRecipientResolver>(
            new FakeTaskNotificationRecipientResolver((asked, "asked@tenant.example")));
        services.AddSingleton<IDataScopeResolver>(new FakeDataScopeResolver());
        services.AddSingleton<ICurrentUserContext>(new FakeCurrentUserContext(asked));
        services.AddSingleton<ITenantContext>(new FakeTenantContext(TaskTestData.Tenant));
        services.AddSingleton<IActorPermissionContext>(TaskActors.None());
        services.AddSingleton<ITenantAuthorizationContext>(new Caller(asked));
        services.AddSingleton<IAuditService>(new CapturingAuditService());

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var answer = await mediator.Send(new AcceptTaskItemCommand(
            request.Id, new TaskTransitionRequest(request.Version, null, null), "corr-fix3-container"));

        Assert.True(answer.StatusCode == 204, $"{answer.StatusCode} {answer.ReasonCode} {string.Join("|", answer.Errors)}");
        var stored = await tasks.GetByIdAsync(request.Id);
        Assert.Equal((TaskAssignmentTarget.Person, (Guid?)asked, unit.Id),
            (stored!.AssignmentTarget, stored.AssigneeUserId, stored.OrganizationUnitId));
        Assert.IsType<TaskWriteGate>(scope.ServiceProvider.GetRequiredService<ITaskWriteGate>());
        Assert.IsType<TaskReadAccessPolicy>(scope.ServiceProvider.GetRequiredService<ITaskReadAccessPolicy>());
    }

    /// <summary>The caller as the audit pipeline reads it.</summary>
    private sealed class Caller(Guid userId) : ITenantAuthorizationContext
    {
        public Guid TenantId => TaskTestData.Tenant;
        public Guid UserId => userId;
        public string? ActorType => "tenant_user";
        public bool IsAuthenticated => true;
        public bool IsPlatformAdmin => false;
        public IReadOnlyList<string> PermissionKeys => [];
        public IReadOnlyList<Guid> RoleIds => [];
        public IReadOnlyList<string> RoleNames => [];
        public IReadOnlyList<Guid> OrgUnitIds => [];
        public IReadOnlyList<Guid> PositionIds => [];
        public Guid? LegalEntityId => null;
        public string? Country => null;
        public IReadOnlyList<Guid> ManagerChain => [];
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
