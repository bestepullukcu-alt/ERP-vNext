using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>One disposable mongod with the PRODUCTION organization schema — never the shared server on 27017.</summary>
public sealed class PositionAssignmentDatesMongoFixture : IAsyncLifetime
{
    private DisposableStandaloneMongo? _mongo;

    public IPlatformDbContext DbContext { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PlatformTestSerializers.Register();
        _mongo = await DisposableStandaloneMongo.StartAsync();
        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{_mongo.Port}/?directConnection=true");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        Client = client;
        Database = client.GetDatabase("diten_platform_standalone_org_seat_dates");
        DbContext = new PlatformDbContext(client, Database);
        await PlatformSchemaManifest.ApplyAsync(Database, new[] { SchemaProfile.Organization });
    }

    public async Task DisposeAsync()
    {
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }
    }
}

/// <summary>
/// BL-526 — a seat (position assignment) whose END date is written: by the organization screen's PUT, by a POST that
/// carries both dates, and by the repository directly, against the production indexes on a real mongod.
///
/// <para>No <c>DateTimeOffset</c> serializer is registered (BL-030), so <c>EffectiveFrom</c> and <c>EffectiveTo</c>
/// are each stored as a BSON array <c>[ticks, offsetMinutes]</c>. MongoDB refuses to index a document in which TWO
/// fields of one compound index are arrays ("cannot index parallel arrays"): an index holding both dates makes every
/// seat with an end date unwritable — the open seat (no end) saves, ending it fails.</para>
/// </summary>
public sealed class PositionAssignmentDatesMongoTests(PositionAssignmentDatesMongoFixture mongo)
    : IClassFixture<PositionAssignmentDatesMongoFixture>
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task Ending_an_open_seat_through_the_screen_route_saves_the_end_date()
    {
        var position = await SeedPositionAsync();
        var seat = await SeedOpenSeatAsync(position);
        using var host = Host();

        var response = await host.PutJsonAsync($"/api/platform/position-assignments/{seat.Id}",
            Token("platform.position-assignments.update"), Body(position, To));

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        Assert.Equal(To, (await StoredAsync(seat.Id)).EffectiveTo);
    }

    [Fact]
    public async Task Creating_a_seat_with_both_dates_through_the_screen_route_saves_it()
    {
        var position = await SeedPositionAsync();
        using var host = Host();

        var response = await host.PostJsonAsync("/api/platform/position-assignments",
            Token("platform.position-assignments.create"), Body(position, To));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var id = JsonDocument.Parse(body).RootElement.GetProperty("data").GetGuid();
        Assert.Equal(To, (await StoredAsync(id)).EffectiveTo);
    }

    [Fact]
    public async Task The_repository_writes_an_end_date_on_an_open_seat()
    {
        var position = await SeedPositionAsync();
        var seat = await SeedOpenSeatAsync(position);
        var repository = new PositionAssignmentRepository(mongo.DbContext, Tenant());

        seat.EffectiveTo = To;
        await repository.UpdateAsync(seat);

        Assert.Equal(To, (await StoredAsync(seat.Id)).EffectiveTo);
    }

    [Fact]
    public async Task A_database_carrying_the_retired_indexes_is_upgraded_at_startup_and_then_ends_a_seat()
    {
        // A database as the live ones are today: the two indexes over both dates, and one open seat.
        var database = mongo.Client.GetDatabase("diten_platform_standalone_org_seat_upgrade");
        var seats = database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments);
        await seats.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PositionAssignment>(
                new BsonDocument { { "TenantId", 1 }, { "PositionId", 1 }, { "EffectiveFrom", 1 }, { "EffectiveTo", 1 }, { "IsDeleted", 1 } },
                new CreateIndexOptions { Name = "ix_position_assignments_position_interval" }),
            new CreateIndexModel<PositionAssignment>(
                new BsonDocument { { "TenantId", 1 }, { "UserId", 1 }, { "EffectiveFrom", 1 }, { "EffectiveTo", 1 }, { "IsDeleted", 1 } },
                new CreateIndexOptions { Name = "ix_position_assignments_user_interval" })
        ]);
        var seat = new PositionAssignment { TenantId = _tenant, PositionId = Guid.NewGuid(), UserId = _user, EffectiveFrom = From };
        await seats.InsertOneAsync(seat);

        // Production startup order: the migrations (drops), then the manifest (builds).
        await PlatformSchemaMigrations.RunAsync(database);
        await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.Organization });

        var names = (await (await seats.Indexes.ListAsync()).ToListAsync()).Select(i => i["name"].AsString).ToList();
        Assert.All(PlatformSchemaMigrations.RetiredPositionAssignmentIndexes, retired => Assert.DoesNotContain(retired, names));
        Assert.Contains("ix_position_assignments_position_from", names);
        Assert.Contains("ix_position_assignments_user_from", names);

        seat.EffectiveTo = To;
        await new PositionAssignmentRepository(new PlatformDbContext(mongo.Client, database), Tenant()).UpdateAsync(seat);
        Assert.Equal(To, (await seats.Find(Builders<PositionAssignment>.Filter.Eq(x => x.Id, seat.Id)).SingleAsync()).EffectiveTo);
    }

    private SignedTenantTokenHttpHost Host() => new(services =>
    {
        services.AddSingleton(mongo.DbContext);
        services.AddScoped<IPositionAssignmentRepository>(sp =>
            new PositionAssignmentRepository(sp.GetRequiredService<IPlatformDbContext>(), sp.GetRequiredService<ITenantContext>()));
        services.AddScoped<IPositionRepository>(sp =>
            new PositionRepository(sp.GetRequiredService<IPlatformDbContext>(), sp.GetRequiredService<ITenantContext>()));
        services.AddScoped<IUserReferenceValidator, ReferenceableUsers>();
    });

    private string Token(string permission) => SignedTenantTokenHttpHost.TenantUserToken(_tenant, _admin, permission);

    private string Body(Position position, DateTimeOffset? to) => JsonSerializer.Serialize(new
    {
        positionId = position.Id,
        userId = _user,
        effectiveFrom = From,
        effectiveTo = to,
        assignmentType = "Primary"
    });

    private TenantContext Tenant()
    {
        var context = new TenantContext();
        context.SetTenant(_tenant);
        return context;
    }

    private async Task<Position> SeedPositionAsync()
    {
        var position = new Position
        {
            TenantId = _tenant,
            Code = "BL526-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Quality Lead",
            OrganizationUnitId = Guid.NewGuid()
        };
        await mongo.Database.GetCollection<Position>(PlatformCollections.Positions).InsertOneAsync(position);
        return position;
    }

    private async Task<PositionAssignment> SeedOpenSeatAsync(Position position)
    {
        var seat = new PositionAssignment
        {
            TenantId = _tenant,
            PositionId = position.Id,
            UserId = _user,
            EffectiveFrom = From
        };
        await mongo.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments).InsertOneAsync(seat);
        return seat;
    }

    private async Task<PositionAssignment> StoredAsync(Guid id) =>
        await mongo.Database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments)
            .Find(Builders<PositionAssignment>.Filter.Eq(x => x.Id, id)).SingleAsync();

    private sealed class ReferenceableUsers : IUserReferenceValidator
    {
        public Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Response<UserReferenceDto>.Success(new UserReferenceDto(userId, true)));
    }
}
