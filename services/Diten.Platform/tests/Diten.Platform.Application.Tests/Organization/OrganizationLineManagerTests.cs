using System.Reflection;
using System.Text.Json;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.OrganizationSeats;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

/// <summary>
/// WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-2) — <c>GET api/platform/organization/seats/{userId}/line-managers</c> on the PRODUCTION
/// reader over the real <see cref="LineManagerResolver"/> and <see cref="TaskSeatDirectory"/> (in-memory org rows that read
/// the ambient tenant, as the tenant filter does): the holders of the seat the user reports to (with names, the seat's code
/// and name; never an e-mail), a vacant seat moves up the chain, nobody → <c>None</c>; the user themself with no key,
/// anybody else only with <see cref="OrganizationSeatPermissions.Read"/> — refused BEFORE any read (no org read, no name
/// lookup); another tenant's user is nobody; AuthService down → the ids without names, never a failure.
/// </summary>
public sealed class OrganizationLineManagerTests
{
    private static readonly Guid Tenant = Guid.Parse("c3c3c3c3-0000-0000-0000-0000000000c3");
    private static readonly Guid OtherTenant = Guid.Parse("d4d4d4d4-0000-0000-0000-0000000000d4");
    private static readonly Guid Rep = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid Manager = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid CoManager = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid Director = Guid.Parse("50000000-0000-0000-0000-000000000004");
    private static readonly Guid Loner = Guid.Parse("50000000-0000-0000-0000-000000000005");
    private static readonly Guid ForeignRep = Guid.Parse("60000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task The_holders_of_the_seat_the_user_reports_to_with_names_and_the_seat_never_an_email()
    {
        var w = new World(caller: Rep);
        var answer = await w.Read(Rep, mayReadOthers: false);

        Assert.Equal(200, answer.StatusCode);
        var dto = answer.Data!;
        Assert.Equal((Rep, "LineManager", w.ManagerSeat.Id, "tr-area-manager", "Bölge Müdürü İstanbul"),
            (dto.UserId, dto.Resolution, dto.PositionId, dto.PositionCode, dto.PositionName));
        Assert.Equal(new[] { (Manager, "Ayşe Yönetici"), (CoManager, "Can Yönetici") },
            dto.Managers.OrderBy(m => m.UserId).Select(m => (m.UserId, m.DisplayName!)));
        Assert.All(dto.Managers, m => Assert.Equal((w.ManagerSeat.Id, "tr-area-manager"), (m.PositionId, m.PositionCode)));
        Assert.DoesNotContain(dto.Managers, m => m.UserId == Rep);                    // never the person themself
        Assert.DoesNotContain("@", JsonSerializer.Serialize(dto));                    // no address anywhere
        Assert.Equal(1, w.Names.Calls);                                               // ONE batch for every name
    }

    [Fact]
    public async Task Another_users_managers_need_the_key_and_are_refused_before_anything_is_read()
    {
        var w = new World(caller: Loner);
        var refused = await w.Read(Rep, mayReadOthers: false);
        Assert.Equal(403, refused.StatusCode);
        Assert.Contains(OrganizationSeatPermissions.Read, string.Join(" ", refused.Errors));
        Assert.Equal((0, 0), (w.Reads, w.Names.Calls));                               // no org read, no name lookup

        var allowed = await w.Read(Rep, mayReadOthers: true);
        Assert.Equal((200, "LineManager"), (allowed.StatusCode, allowed.Data!.Resolution));

        // no tenant / no caller → refused the same way, before any read
        var noTenant = new World(caller: Rep, tenantResolved: false);
        Assert.Equal(403, (await noTenant.Read(Rep, mayReadOthers: true)).StatusCode);
        Assert.Equal(0, noTenant.Reads);
        Assert.Equal(400, (await w.Read(Guid.Empty, mayReadOthers: true)).StatusCode);
    }

    [Fact]
    public async Task A_vacant_manager_seat_moves_up_the_chain_and_nobody_above_is_none()
    {
        var w = new World(caller: Rep);
        w.Vacate(Manager);
        w.Vacate(CoManager);

        var chain = (await w.Read(Rep, mayReadOthers: false)).Data!;
        Assert.Equal(("ManagerChain", w.DirectorSeat.Id, "tr-director"), (chain.Resolution, chain.PositionId, chain.PositionCode));
        Assert.Equal(new[] { Director }, chain.Managers.Select(m => m.UserId));

        var top = (await new World(caller: Loner).Read(Loner, mayReadOthers: false)).Data!;
        Assert.Equal(("None", (Guid?)null, (string?)null, (string?)null), (top.Resolution, top.PositionId, top.PositionCode, top.PositionName));
        Assert.Empty(top.Managers);
    }

    [Fact]
    public async Task Another_tenants_user_is_nobody_and_names_down_still_answer_the_ids()
    {
        var w = new World(caller: Rep);
        var foreign = (await w.Read(ForeignRep, mayReadOthers: true)).Data!;
        Assert.Equal("None", foreign.Resolution);
        Assert.Empty(foreign.Managers);

        w.Names.Down = true;
        var dto = (await w.Read(Rep, mayReadOthers: false)).Data!;
        Assert.Equal(2, dto.Managers.Count);
        Assert.All(dto.Managers, m => Assert.Null(m.DisplayName));
    }

    [Fact]
    public void The_endpoint_is_the_seats_sibling_login_only_with_its_reason()
    {
        var method = typeof(OrganizationSeatsController).GetMethod(nameof(OrganizationSeatsController.LineManagers))!;
        Assert.Equal("{userId:guid}/line-managers", method.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Contains("platform.organization-seats.read", method.GetCustomAttribute<LoginOnlyAttribute>()!.Reason);
        Assert.Equal("api/platform/organization/seats", typeof(OrganizationSeatsController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    // ── the world ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World
    {
        private readonly List<PositionAssignment> _assignments = [];
        private readonly List<Position> _positions = [];
        private readonly OrganizationLineManagerReader _reader;
        private readonly Tenancy _tenancy;

        public int Reads { get; set; }
        public Names Names { get; } = new();
        public Position ManagerSeat { get; }
        public Position DirectorSeat { get; }

        public World(Guid caller, bool tenantResolved = true)
        {
            _tenancy = new Tenancy(tenantResolved ? Tenant : null);
            DirectorSeat = Seat(Tenant, "tr-director", "Satış Direktörü", null);
            ManagerSeat = Seat(Tenant, "tr-area-manager", "Bölge Müdürü İstanbul", DirectorSeat.Id);
            var repSeat = Seat(Tenant, "tr-medical-representative", "Tıbbi Temsilci", ManagerSeat.Id);
            var lonerSeat = Seat(Tenant, "tr-ceo", "Genel Müdür", null);
            Hold(Tenant, Rep, repSeat);
            Hold(Tenant, Manager, ManagerSeat);
            Hold(Tenant, CoManager, ManagerSeat);
            Hold(Tenant, Director, DirectorSeat);
            Hold(Tenant, Loner, lonerSeat);
            var foreignManager = Seat(OtherTenant, "tr-area-manager", "Foreign", null);
            Hold(OtherTenant, ForeignRep, Seat(OtherTenant, "tr-medical-representative", "Foreign rep", foreignManager.Id));
            Hold(OtherTenant, Guid.NewGuid(), foreignManager);

            var directory = new TaskSeatDirectory(new Assignments(this));
            var positions = new Positions(this);
            _reader = new OrganizationLineManagerReader(_tenancy, new Caller(caller),
                new LineManagerResolver(directory, positions, _tenancy, NullLogger<LineManagerResolver>.Instance),
                positions, NullLogger<OrganizationLineManagerReader>.Instance, Names);
        }

        public Task<Common.Response<OrganizationLineManagersDto>> Read(Guid user, bool mayReadOthers)
            => _reader.Handle(new GetOrganizationLineManagersQuery(user, mayReadOthers), default);

        public void Vacate(Guid user) => _assignments.RemoveAll(a => a.UserId == user);

        private Position Seat(Guid tenant, string code, string name, Guid? reportsTo)
        {
            var p = new Position { Id = Guid.NewGuid(), TenantId = tenant, Code = code, Name = name, OrganizationUnitId = Guid.NewGuid(), ReportsToPositionId = reportsTo };
            _positions.Add(p);
            return p;
        }

        private void Hold(Guid tenant, Guid user, Position seat) => _assignments.Add(new PositionAssignment
        {
            Id = Guid.NewGuid(), TenantId = tenant, UserId = user, PositionId = seat.Id, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
            AssignmentType = AssignmentType.Primary
        });

        // the tenant execution filter: the AMBIENT tenant's rows only
        private sealed class Assignments(World w) : IPositionAssignmentRepository
        {
            public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
            {
                w.Reads++;
                return Task.FromResult<IReadOnlyList<PositionAssignment>>(w._assignments.Where(a => a.TenantId == w._tenancy.TenantId && !a.IsDeleted).ToList());
            }

            public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<PositionAssignment> CreateAsync(PositionAssignment a, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset from, DateTimeOffset? to, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
            public Task UpdateAsync(PositionAssignment a, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("no writes");
        }

        private sealed class Positions(World w) : IPositionRepository
        {
            public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
            {
                w.Reads++;
                return Task.FromResult<IReadOnlyList<Position>>(w._positions.Where(p => p.TenantId == w._tenancy.TenantId).ToList());
            }

            public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default)
            {
                w.Reads++;
                return Task.FromResult(w._positions.FirstOrDefault(p => p.Id == id && p.TenantId == w._tenancy.TenantId));
            }

            public Task<Position> CreateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
            public Task UpdateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("no writes");
        }
    }

    private sealed class Names : IUserDisplayNameResolver
    {
        private static readonly Dictionary<Guid, string> Known = new()
        {
            [Manager] = "Ayşe Yönetici", [CoManager] = "Can Yönetici", [Director] = "Deniz Direktör"
        };

        public int Calls { get; private set; }
        public bool Down { get; set; }

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            Calls++;
            if (Down) throw new HttpRequestException("auth down");
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.Where(Known.ContainsKey).ToDictionary(id => id, id => Known[id]));
        }
    }

    private sealed class Tenancy(Guid? tenant) : ITenantContext
    {
        public Guid TenantId => tenant ?? Guid.Empty;
        public bool IsResolved => tenant is not null;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
        public void ClearTenant() => throw new NotSupportedException();
    }

    private sealed class Caller(Guid user) : ICurrentUserContext
    {
        public Guid UserId => user;
        public string? Email => null;
        public string? DisplayName => null;
        public string ActorName => "test";
        public bool IsAuthenticated => user != Guid.Empty;
    }
}
