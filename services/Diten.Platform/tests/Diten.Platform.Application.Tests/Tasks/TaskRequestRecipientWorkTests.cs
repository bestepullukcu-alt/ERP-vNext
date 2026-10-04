using System.Reflection;
using System.Runtime.ExceptionServices;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 (A6, CT decision) — EVERY "NO" DOES THE SAME WORK. Whether somebody can be sent a request
/// is answered with one code for every reason (no seat, a switched-off account, unknown); it used to be answered at
/// different COST — one query for "no seat", the seat, position and unit reads plus AuthService for "seat, account off" —
/// and the time it took told the sender whether that person holds an active seat here. Measured by counting the calls
/// each dependency receives, never by a clock.
/// </summary>
public sealed class TaskRequestRecipientWorkTests
{
    private static readonly Guid WithSeatActive = Guid.NewGuid();
    private static readonly Guid WithSeatSwitchedOff = Guid.NewGuid();
    private static readonly Guid NoSeatActive = Guid.NewGuid();
    private static readonly Guid Unknown = Guid.NewGuid();

    /// <summary>MUTATION TARGET (TaskRequestService.ResolveRecipientAsync — read everything, then decide).</summary>
    [Fact]
    public async Task Every_answer_about_a_recipient_costs_the_same_calls_whoever_they_are()
    {
        var calls = new Dictionary<Guid, string>();
        var answers = new Dictionary<Guid, bool>();
        foreach (var person in new[] { WithSeatActive, WithSeatSwitchedOff, NoSeatActive, Unknown })
        {
            var (service, log) = Service();
            answers[person] = await service.ResolveRecipientAsync(person, CancellationToken.None) is not null;
            calls[person] = string.Join(" → ", log);
        }

        // Non-vacuity: the four really are four different answers to "can this person be asked".
        Assert.Equal(new[] { true, false, false, false },
            new[] { answers[WithSeatActive], answers[WithSeatSwitchedOff], answers[NoSeatActive], answers[Unknown] });
        Assert.Contains("ActiveForUserAsync", calls[NoSeatActive]);
        Assert.Contains("ResolveAsync", calls[NoSeatActive]);
        Assert.All(calls.Values, sequence => Assert.Equal(calls[WithSeatActive], sequence));
    }

    private static (TaskRequestService Service, List<string> Log) Service()
    {
        var unit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "PL", Name = "PL", LegalEntityId = Guid.NewGuid(),
            Status = OrgUnitStatus.Active, OrgUnitType = OrgUnitType.HQ
        };
        var position = new Position
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "PL-1", Name = "PL-1", OrganizationUnitId = unit.Id,
            Status = PositionStatus.Active
        };
        PositionAssignment Seat(Guid user) => new()
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, PositionId = position.Id, UserId = user,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
        };

        var log = new List<string>();
        var service = new TaskRequestService(
            Counting<ITaskSeatDirectory>.Wrap(new FakePositionAssignmentRepository(Seat(WithSeatActive), Seat(WithSeatSwitchedOff)), log),
            Counting<IPositionRepository>.Wrap(new FakePositionRepository(position), log),
            Counting<IOrganizationUnitRepository>.Wrap(new FakeOrganizationUnitRepository(unit), log),
            Counting<ITaskNotificationRecipientResolver>.Wrap(new FakeTaskNotificationRecipientResolver(
                (WithSeatActive, "a@tenant.example"), (NoSeatActive, "c@tenant.example")), log),
            TaskActors.Holding(Diten.Platform.Application.Features.Tasks.TaskPermissions.RequestsCreate),
            new FakeCurrentUserContext(TaskTestData.Me));
        return (service, log);
    }

    /// <summary>Forwards every call unchanged and writes its interface and method into the shared log.</summary>
    public class Counting<T> : DispatchProxy where T : class
    {
        private T _inner = null!;
        private List<string> _log = null!;

        public static T Wrap(T inner, List<string> log)
        {
            var proxy = Create<T, Counting<T>>();
            var self = (Counting<T>)(object)proxy;
            self._inner = inner;
            self._log = log;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            _log.Add($"{typeof(T).Name}.{targetMethod!.Name}");
            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
