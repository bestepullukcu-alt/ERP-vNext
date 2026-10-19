using System.Reflection;
using System.Security.Claims;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Organization.SelfRegistration;
using Diten.Platform.Application.Features.OrganizationSeats;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

/// <summary>
/// WP-VW-W3L PLT-ORG-SEAT (K-L3, K-L8) — a user's current PRIMARY seat (position → organization unit → legal entity), read
/// through <see cref="ITaskSeatDirectory"/> (the real <see cref="TaskSeatDirectory"/> over in-memory rows): an active
/// Primary is the seat; a future / ended / cancelled Primary or only a Secondary is no seat; two active Primaries → the
/// earliest, reported; own seat with no key, anybody else only with <see cref="OrganizationSeatPermissions.Read"/> (403,
/// before any read); another tenant's rows are never a seat; the bulk form ≤ 100, one read round, the same answers as the
/// single form; the API key is a tenant-assignable manifest permission; the controller decides the key from the claims.
/// </summary>
public sealed class OrganizationSeatTests
{
    private static readonly Guid Tenant = Guid.Parse("a1a1a1a1-0000-0000-0000-0000000000a1");
    private static readonly Guid OtherTenant = Guid.Parse("b2b2b2b2-0000-0000-0000-0000000000b2");
    private static readonly Guid Me = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Colleague = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid Stranger = Guid.Parse("33333333-0000-0000-0000-000000000003");
    private static readonly Guid LegalEntityTr = Guid.Parse("44444444-0000-0000-0000-0000000000e1");
    private static readonly Guid LegalEntityUz = Guid.Parse("44444444-0000-0000-0000-0000000000e2");

    private static DateTimeOffset Now => DateTimeOffset.UtcNow; // the directory reads the real clock (TaskSeatDirectory)

    // ── the seat rule (through the directory) ────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_active_primary_is_the_seat_with_its_unit_and_legal_entity()
    {
        var w = new World();
        var (position, unit) = w.Seat("tr-medical-representative", "TRRO-FF", LegalEntityTr);
        var from = Now.AddDays(-30);
        w.Assign(Me, position, from);

        var seat = await w.Single(Me, mayReadOthers: false);

        Assert.Equal(200, seat.StatusCode);
        var primary = seat.Data!.Primary!;
        Assert.Equal((Me, position.Id, "tr-medical-representative", unit.Id, "TRRO-FF", LegalEntityTr, from),
            (seat.Data.UserId, primary.PositionId, primary.PositionCode, primary.OrganizationUnitId, primary.OrganizationUnitCode, primary.LegalEntityId, primary.EffectiveFrom));
    }

    [Theory]
    [InlineData("future")]
    [InlineData("ended")]
    [InlineData("ends-now")]
    [InlineData("cancelled")]
    [InlineData("secondary-only")]
    [InlineData("acting-only")]
    [InlineData("none")]
    public async Task A_primary_that_is_not_active_now_or_a_non_primary_seat_is_no_seat(string shape)
    {
        var w = new World();
        var (position, _) = w.Seat("tr-medical-representative", "TRRO-FF", LegalEntityTr);
        switch (shape)
        {
            case "future": w.Assign(Me, position, Now.AddDays(1)); break;
            case "ended": w.Assign(Me, position, Now.AddDays(-30), to: Now.AddDays(-1)); break;
            case "ends-now": w.Assign(Me, position, Now.AddDays(-30), to: Now.AddSeconds(-1)); break;
            case "cancelled": w.Assign(Me, position, Now.AddDays(-30), cancelled: true); break;
            case "secondary-only": w.Assign(Me, position, Now.AddDays(-30), type: AssignmentType.Secondary); break;
            case "acting-only": w.Assign(Me, position, Now.AddDays(-30), type: AssignmentType.Acting); break;
        }

        var seat = await w.Single(Me, mayReadOthers: false);

        Assert.Equal(200, seat.StatusCode);
        Assert.Equal(Me, seat.Data!.UserId);
        Assert.Null(seat.Data.Primary);
    }

    [Fact]
    public async Task A_secondary_beside_the_primary_never_replaces_it()
    {
        var w = new World();
        var (home, _) = w.Seat("tr-medical-representative", "TRRO-FF", LegalEntityTr);
        var (other, _) = w.Seat("uz-medical-representative", "UZRO-FF", LegalEntityUz);
        w.Assign(Me, other, Now.AddDays(-90), type: AssignmentType.Secondary);   // earlier, but not primary
        w.Assign(Me, home, Now.AddDays(-10));

        var primary = (await w.Single(Me, mayReadOthers: false)).Data!.Primary!;

        Assert.Equal((home.Id, LegalEntityTr), (primary.PositionId, primary.LegalEntityId));
    }

    [Fact]
    public async Task Two_active_primaries_resolve_to_the_earliest_and_are_reported()
    {
        var w = new World();
        var (later, _) = w.Seat("tr-area-manager", "TRRO-FF", LegalEntityTr);
        var (earlier, _) = w.Seat("uz-area-manager", "UZRO-FF", LegalEntityUz);
        w.Assign(Me, later, Now.AddDays(-5));
        w.Assign(Me, earlier, Now.AddDays(-50));

        var primary = (await w.Single(Me, mayReadOthers: false)).Data!.Primary!;

        Assert.Equal((earlier.Id, LegalEntityUz), (primary.PositionId, primary.LegalEntityId));
        var warning = Assert.Single(w.Log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("2 active Primary assignments", warning.Message, StringComparison.Ordinal);
        Assert.Contains(Me.ToString(), warning.Message, StringComparison.Ordinal);
    }

    // ── who may ask ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_users_seat_needs_the_key_and_is_refused_before_anything_is_read()
    {
        var w = new World();
        var (position, _) = w.Seat("tr-medical-representative", "TRRO-FF", LegalEntityTr);
        w.Assign(Colleague, position, Now.AddDays(-3));

        var refused = await w.Single(Colleague, mayReadOthers: false);
        Assert.Equal(403, refused.StatusCode);
        Assert.Contains(OrganizationSeatPermissions.Read, string.Join(" ", refused.Errors ?? []), StringComparison.Ordinal);
        Assert.Equal(0, w.Reads);

        var refusedBulk = await w.Bulk([Me, Colleague], mayReadOthers: false);
        Assert.Equal(403, refusedBulk.StatusCode);
        Assert.Equal(0, w.Reads);

        var allowed = await w.Single(Colleague, mayReadOthers: true);
        Assert.Equal(200, allowed.StatusCode);
        Assert.Equal(position.Id, allowed.Data!.Primary!.PositionId);

        // own seat: no key needed (single and bulk of only oneself)
        Assert.Equal(200, (await w.Single(Me, mayReadOthers: false)).StatusCode);
        Assert.Equal(200, (await w.Bulk([Me, Me], mayReadOthers: false)).StatusCode);
    }

    [Fact]
    public async Task No_authenticated_caller_or_no_tenant_is_refused()
    {
        var anonymous = new World(caller: Guid.Empty);
        Assert.Equal(403, (await anonymous.Single(Me, mayReadOthers: true)).StatusCode);
        var noTenant = new World(tenantResolved: false);
        Assert.Equal(403, (await noTenant.Single(Me, mayReadOthers: false)).StatusCode);
        Assert.Equal(0, anonymous.Reads + noTenant.Reads);
    }

    // ── tenant ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_rows_are_never_a_seat_even_when_a_read_hands_them_over()
    {
        // The repositories are tenant-scoped (TenantRepository's execution filter). These doubles are deliberately NOT, so
        // the reader's own tenant check is what is measured: another tenant's assignment, position and unit reach it.
        var w = new World();
        var (foreignPosition, _) = w.Seat("tr-medical-representative", "TRRO-FF", LegalEntityTr, tenant: OtherTenant);
        w.Assign(Stranger, foreignPosition, Now.AddDays(-3), tenant: OtherTenant);

        var seat = await w.Single(Stranger, mayReadOthers: true);

        Assert.Equal(200, seat.StatusCode);
        Assert.Null(seat.Data!.Primary);
        Assert.Null(Assert.Single((await w.Bulk([Stranger], mayReadOthers: true)).Data!.Items).Primary);
    }

    // ── the bulk form ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_bulk_form_answers_like_the_single_form_in_one_read_round()
    {
        var w = new World();
        var (tr, _) = w.Seat("tr-medical-representative", "TRRO-FF", LegalEntityTr);
        var (uz, _) = w.Seat("uz-medical-representative", "UZRO-FF", LegalEntityUz);
        var users = Enumerable.Range(1, 60).Select(i => Guid.Parse($"55555555-0000-0000-0000-{i:000000000000}")).ToList();
        for (var i = 0; i < users.Count; i++)
        {
            if (i % 3 == 0) continue;                                              // every third user: no seat
            w.Assign(users[i], i % 2 == 0 ? tr : uz, Now.AddDays(-i - 1));
        }

        w.Reads = 0;
        var bulk = await w.Bulk(users.Concat([users[1]]).ToList(), mayReadOthers: true); // a duplicate is answered once
        Assert.Equal(200, bulk.StatusCode);
        Assert.Equal(3, w.Reads);                                                  // assignments + positions + units, once
        Assert.Equal(users, bulk.Data!.Items.Select(item => item.UserId).ToList()); // the asked order

        foreach (var user in users)
        {
            var single = (await w.Single(user, mayReadOthers: true)).Data!;
            Assert.Equal(single, bulk.Data.Items.Single(item => item.UserId == user));
        }
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(1, 200)]
    [InlineData(100, 200)]
    [InlineData(101, 400)]
    public async Task The_bulk_form_takes_one_to_a_hundred_users(int count, int status)
    {
        var w = new World();
        var users = Enumerable.Range(1, count).Select(i => Guid.Parse($"66666666-0000-0000-0000-{i:000000000000}")).ToList();
        var answer = await w.Bulk(users, mayReadOthers: true);
        Assert.Equal(status, answer.StatusCode);
        if (status == 400) Assert.Equal(0, w.Reads);
        Assert.Equal(400, (await w.Bulk(null, mayReadOthers: true)).StatusCode);
        Assert.Equal(400, (await w.Bulk([Guid.Empty], mayReadOthers: true)).StatusCode);
    }

    // ── API surface ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_endpoints_are_the_K_L8_contract_and_the_key_is_decided_from_the_claims()
    {
        var controller = typeof(OrganizationSeatsController);
        Assert.Equal("api/platform/organization/seats", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("{userId:guid}", controller.GetMethod(nameof(OrganizationSeatsController.Get))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("lookup", controller.GetMethod(nameof(OrganizationSeatsController.Lookup))!.GetCustomAttribute<HttpPostAttribute>()!.Template);
        // no [HasPermission]: the own seat needs no key (the reader decides, from the key the controller reads)
        Assert.Empty(controller.GetMethods().SelectMany(m => m.GetCustomAttributes<API.Security.HasPermissionAttribute>()));
        // …and login-only BY DECISION (BL-410), with its reason, on both actions
        Assert.All(new[] { nameof(OrganizationSeatsController.Get), nameof(OrganizationSeatsController.Lookup) }, name =>
            Assert.False(string.IsNullOrWhiteSpace(controller.GetMethod(name)!.GetCustomAttribute<API.Security.LoginOnlyAttribute>()?.Reason)));

        static ClaimsPrincipal User(params string[] permissions) => new(new ClaimsIdentity(
            [new Claim("sub", Me.ToString()), .. permissions.Select(p => new Claim("permission", p))], "test"));
        Assert.True(OrganizationSeatsController.HoldsReadOthers(User(OrganizationSeatPermissions.Read)));
        Assert.False(OrganizationSeatsController.HoldsReadOthers(User("platform.position-assignments.read")));
        Assert.False(OrganizationSeatsController.HoldsReadOthers(User()));
    }

    [Fact]
    public void The_key_is_a_tenant_assignable_API_permission_of_the_organization_module_with_its_description()
    {
        var manifest = new OrganizationManifestProvider().GetManifest();
        Assert.True(manifest.IsTenantAssignable);
        var permission = Assert.Single(manifest.ApiPermissions ?? [], p => p.PermissionKey == "platform.organization-seats.read");
        Assert.Equal("Tenant", permission.Scope);
        Assert.Contains("primary seat", permission.DisplayName, StringComparison.Ordinal);
        Assert.Equal("platform.organization-seats.read", OrganizationSeatPermissions.Read);
    }

    [Fact]
    public void The_seat_rule_is_consumed_from_the_directory_and_never_written_again()
    {
        var source = File.ReadAllText(RepoPaths.ApplicationSource("Features", "OrganizationSeats", "OrganizationSeats.cs"));
        Assert.Contains("_seats.PrimarySeatsAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IPositionAssignmentRepository", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsActiveNow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AssignmentType", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCancelled", source, StringComparison.Ordinal);
    }

    // ── doubles ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World
    {
        private readonly List<PositionAssignment> _assignments = [];
        private readonly List<Position> _positions = [];
        private readonly List<OrganizationUnit> _units = [];
        private readonly OrganizationSeatReader _reader;

        public int Reads { get; set; }
        public CollectingLogger Log { get; } = new();

        public World(Guid? caller = null, bool tenantResolved = true)
        {
            var assignments = new Assignments(this);
            _reader = new OrganizationSeatReader(new Tenancy(tenantResolved ? Tenant : null), new Caller(caller ?? Me),
                new TaskSeatDirectory(assignments), new Positions(this), new Units(this), Log);
        }

        public (Position Position, OrganizationUnit Unit) Seat(string positionCode, string unitCode, Guid legalEntity, Guid? tenant = null)
        {
            var unit = new OrganizationUnit { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, Code = unitCode, Name = unitCode, LegalEntityId = legalEntity };
            var position = new Position { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, Code = positionCode, Name = positionCode, OrganizationUnitId = unit.Id };
            _units.Add(unit);
            _positions.Add(position);
            return (position, unit);
        }

        public void Assign(Guid user, Position position, DateTimeOffset from, DateTimeOffset? to = null, bool cancelled = false,
            AssignmentType type = AssignmentType.Primary, Guid? tenant = null)
            => _assignments.Add(new PositionAssignment
            {
                Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, UserId = user, PositionId = position.Id, EffectiveFrom = from,
                EffectiveTo = to, IsCancelled = cancelled, AssignmentType = type
            });

        public Task<Common.Response<OrganizationSeatDto>> Single(Guid user, bool mayReadOthers)
            => _reader.Handle(new GetOrganizationSeatQuery(user, mayReadOthers), default);

        public Task<Common.Response<OrganizationSeatsDto>> Bulk(IReadOnlyList<Guid>? users, bool mayReadOthers)
            => _reader.Handle(new LookupOrganizationSeatsQuery(users, mayReadOthers), default);

        // NOT tenant-filtered on purpose (see the tenant test): every non-deleted row of every tenant.
        private sealed class Assignments(World w) : IPositionAssignmentRepository
        {
            public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
            {
                w.Reads++;
                return Task.FromResult<IReadOnlyList<PositionAssignment>>(w._assignments.Where(a => !a.IsDeleted).ToList());
            }

            public Task<PositionAssignment> CreateAsync(PositionAssignment a, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset from, DateTimeOffset? to, Guid? excludeId, CancellationToken ct = default) => throw new NotSupportedException();
            public Task UpdateAsync(PositionAssignment a, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("no writes");
        }

        private sealed class Positions(World w) : IPositionRepository
        {
            public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
            {
                w.Reads++;
                return Task.FromResult<IReadOnlyList<Position>>(w._positions.ToList());
            }

            public Task<Position> CreateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("a read per user");
            public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
            public Task UpdateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("no writes");
        }

        private sealed class Units(World w) : IOrganizationUnitRepository
        {
            public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default)
            {
                w.Reads++;
                return Task.FromResult<IReadOnlyList<OrganizationUnit>>(w._units.ToList());
            }

            public Task<OrganizationUnit> CreateAsync(OrganizationUnit unit, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("a read per user");
            public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
            public Task UpdateAsync(OrganizationUnit unit, CancellationToken ct = default) => throw new NotSupportedException("no writes");
            public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException("no writes");
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

    internal sealed class CollectingLogger : ILogger<OrganizationSeatReader>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
