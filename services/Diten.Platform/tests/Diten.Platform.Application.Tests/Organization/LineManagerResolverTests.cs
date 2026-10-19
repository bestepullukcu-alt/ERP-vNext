using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.OrganizationSeats;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

/// <summary>
/// WP-VW-W5 PLT-NOTIFY (K-N2) — the shared "line manager of X" over the REAL <see cref="TaskSeatDirectory"/>: the holders
/// of the seat the primary seat reports to, never the person themselves; a vacant seat moves up; a cycle and the depth
/// guard end the walk with nobody; and a tenant other than the ambient one is answered with nobody.
/// </summary>
public sealed class LineManagerResolverTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Person = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid Boss = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid BigBoss = Guid.Parse("30000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task The_line_manager_is_whoever_holds_the_seat_the_primary_seat_reports_to()
    {
        var w = new World();
        var top = w.Seat((Guid?)null);
        var boss = w.Seat(top);
        var mine = w.Seat(boss);
        w.Hold(Person, mine);
        w.Hold(Boss, boss);
        w.Hold(BigBoss, top);

        var result = await w.Resolver.ResolveAsync(Tenant, Person);

        Assert.Equal([Boss], result.UserIds);
        Assert.Equal(LineManagerResolution.LineManager, result.Resolution);
        Assert.Equal(boss.Id, result.PositionId);
    }

    [Fact]
    public async Task The_person_is_never_their_own_line_manager_a_self_held_seat_counts_as_vacant()
    {
        var w = new World();
        var top = w.Seat((Guid?)null);
        var boss = w.Seat(top);
        var mine = w.Seat(boss);
        w.Hold(Person, mine);
        // The person ALSO holds the seat they report to (an acting manager of their own team), and nobody else does.
        w.Hold(Person, boss, AssignmentType.Secondary);
        w.Hold(BigBoss, top);

        var result = await w.Resolver.ResolveAsync(Tenant, Person);

        Assert.Equal([BigBoss], result.UserIds);
        Assert.Equal(LineManagerResolution.ManagerChain, result.Resolution);
        Assert.DoesNotContain(Person, result.UserIds);
    }

    [Fact]
    public async Task A_vacant_seat_moves_up_and_says_so()
    {
        var w = new World();
        var top = w.Seat((Guid?)null);
        var vacant = w.Seat(top);
        var mine = w.Seat(vacant);
        w.Hold(Person, mine);
        w.Hold(BigBoss, top);

        var result = await w.Resolver.ResolveAsync(Tenant, Person);

        Assert.Equal([BigBoss], result.UserIds);
        Assert.Equal(LineManagerResolution.ManagerChain, result.Resolution);
        Assert.Equal(top.Id, result.PositionId);
    }

    [Fact]
    public async Task A_cycle_in_the_org_chart_ends_the_walk_with_nobody()
    {
        var w = new World();
        var a = w.Seat((Guid?)null);
        var b = w.Seat(a.Id);
        a.ReportsToPositionId = b.Id; // a → b → a, both vacant
        var mine = w.Seat(a);
        w.Hold(Person, mine);

        var result = await w.Resolver.ResolveAsync(Tenant, Person);

        Assert.Same(LineManagers.Nobody, result);
        Assert.Equal(LineManagerResolution.None, result.Resolution);
    }

    [Theory]
    [InlineData(LineManagerResolver.MaxChainDepth, true)]
    [InlineData(LineManagerResolver.MaxChainDepth + 1, false)]
    public async Task The_walk_goes_at_most_the_depth_guard_up(int depthOfTheHolder, bool found)
    {
        var w = new World();
        var cursor = w.Seat((Guid?)null);
        w.Hold(BigBoss, cursor);
        for (var i = 1; i < depthOfTheHolder; i++)
        {
            cursor = w.Seat(cursor); // vacant seats between the person and the one holder
        }

        var mine = w.Seat(cursor);
        w.Hold(Person, mine);

        var result = await w.Resolver.ResolveAsync(Tenant, Person);

        Assert.Equal(found, result.UserIds.Contains(BigBoss));
        Assert.Equal(found ? LineManagerResolution.ManagerChain : LineManagerResolution.None, result.Resolution);
    }

    [Fact]
    public async Task No_seat_or_no_reporting_line_is_nobody()
    {
        var w = new World();
        var loner = w.Seat((Guid?)null);
        w.Hold(Boss, loner);

        Assert.Same(LineManagers.Nobody, await w.Resolver.ResolveAsync(Tenant, Person)); // no seat at all
        Assert.Same(LineManagers.Nobody, await w.Resolver.ResolveAsync(Tenant, Boss));   // a seat that reports nowhere
    }

    [Fact]
    public async Task Asked_about_another_tenant_than_the_ambient_one_the_answer_is_nobody()
    {
        var w = new World();
        var boss = w.Seat((Guid?)null);
        var mine = w.Seat(boss);
        w.Hold(Person, mine);
        w.Hold(Boss, boss);

        Assert.Equal([Boss], (await w.Resolver.ResolveAsync(Tenant, Person)).UserIds);
        Assert.Same(LineManagers.Nobody, await w.Resolver.ResolveAsync(Guid.NewGuid(), Person));
        Assert.Same(LineManagers.Nobody, await w.Resolver.ResolveAsync(Guid.Empty, Person));
    }

    private sealed class World
    {
        private readonly List<Position> _positions = [];
        private readonly List<PositionAssignment> _assignments = [];

        public LineManagerResolver Resolver => new(
            new TaskSeatDirectory(new FakePositionAssignmentRepository([.. _assignments])),
            new FakePositionRepository([.. _positions]),
            new FakeTenantContext(Tenant),
            NullLogger<LineManagerResolver>.Instance);

        public Position Seat(Position reportsTo) => Seat((Guid?)reportsTo.Id);

        public Position Seat(Guid? reportsTo)
        {
            var position = new Position
            {
                Id = Guid.NewGuid(), TenantId = Tenant, Code = "P" + _positions.Count, Name = "P" + _positions.Count,
                OrganizationUnitId = Guid.NewGuid(), ReportsToPositionId = reportsTo
            };
            _positions.Add(position);
            return position;
        }

        public void Hold(Guid user, Position position, AssignmentType type = AssignmentType.Primary) =>
            _assignments.Add(new PositionAssignment
            {
                Id = Guid.NewGuid(), TenantId = Tenant, UserId = user, PositionId = position.Id,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10), AssignmentType = type
            });
    }
}
