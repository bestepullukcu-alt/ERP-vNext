using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

/// <summary>
/// Calendar-code uniqueness is enforced in TWO places that must agree: the repository guard (which produces the
/// friendly 409) and the unique index's partial filter (the real backstop). These run against a real MongoDB
/// precisely because a guard that is looser than the index is invisible to a mocked test — it only shows up as an
/// E11000 500 at runtime, which is the defect being fixed here.
/// </summary>
public sealed class WorkingCalendarCodeUniquenessMongoTests : IAsyncLifetime
{
    private const string CollectionName = "working_calendars";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    private MongoClient _client = null!;
    private string _databaseName = null!;
    private IMongoDatabase _database = null!;
    private WorkingCalendarRepository _repository = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _client = new MongoClient(settings);
        _databaseName = $"diten_wc_code_{Guid.NewGuid():N}";
        _database = _client.GetDatabase(_databaseName);
        await _database.RunCommandAsync<object>("{ ping: 1 }");
        await MongoDbIndexConfigurations.EnsureIndexesAsync(_database);

        // Platform actor: no ambient tenant, so the repository operates on the country layer.
        var tenantContext = new TenantContext();
        tenantContext.SetPlatformContext(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        _repository = new WorkingCalendarRepository(new PlatformDbContext(_client, _database), tenantContext);
    }

    public Task DisposeAsync() => _client.DropDatabaseAsync(_databaseName);

    private IMongoCollection<Wc> Collection => _database.GetCollection<Wc>(CollectionName);

    private static Wc Row(string status, Guid? tenantId = null, string code = "TR-2026") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CalendarCode = code,
        CalendarName = code,
        CountryCode = "TR",
        CalendarYear = 2026,
        ScopeType = tenantId is null ? WorkingCalendarScopeType.Country : WorkingCalendarScopeType.Tenant,
        CalendarStatus = status,
        WeekendDays = ["saturday", "sunday"]
    };

    [Fact]
    public async Task Archived_row_releases_its_code_for_the_guard()
    {
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Archived));

        Assert.False(await _repository.ExistsByCodeAsync(null, "TR", 2026, "TR-2026"));
    }

    [Fact]
    public async Task Archived_row_releases_its_code_for_the_UNIQUE_INDEX_too()
    {
        // The regression: the guard was fixed but the index still held the archived row, so the create fell through
        // to an E11000 and surfaced as a 500 instead of succeeding.
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Archived));

        var replacement = Row(WorkingCalendarStatus.Draft);
        var exception = await Record.ExceptionAsync(() => _repository.CreateAsync(replacement));

        Assert.Null(exception);
        Assert.Equal(2, await Collection.CountDocumentsAsync(FilterDefinition<Wc>.Empty));
    }

    [Theory]
    [InlineData(WorkingCalendarStatus.Draft)]
    [InlineData(WorkingCalendarStatus.Active)]
    public async Task A_live_row_still_holds_its_code(string status)
    {
        await Collection.InsertOneAsync(Row(status));

        Assert.True(await _repository.ExistsByCodeAsync(null, "TR", 2026, "TR-2026"));
    }

    [Fact]
    public async Task Two_live_rows_with_the_same_code_are_still_rejected_by_the_index()
    {
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Draft));

        var duplicate = Row(WorkingCalendarStatus.Active);
        var exception = await Record.ExceptionAsync(() => _repository.CreateAsync(duplicate));

        var write = Assert.IsType<MongoWriteException>(exception);
        Assert.Equal(ServerErrorCategory.DuplicateKey, write.WriteError.Category);
    }

    [Fact]
    public async Task Many_archived_rows_may_share_one_code()
    {
        // Archiving repeatedly across a year must not accumulate into a lockout.
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Archived));
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Archived));
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Archived));

        Assert.False(await _repository.ExistsByCodeAsync(null, "TR", 2026, "TR-2026"));
        Assert.Null(await Record.ExceptionAsync(() => _repository.CreateAsync(Row(WorkingCalendarStatus.Active))));
    }

    [Fact]
    public async Task Excluded_id_is_still_honoured_so_a_row_does_not_collide_with_itself()
    {
        var existing = Row(WorkingCalendarStatus.Active);
        await Collection.InsertOneAsync(existing);

        Assert.True(await _repository.ExistsByCodeAsync(null, "TR", 2026, "TR-2026"));
        Assert.False(await _repository.ExistsByCodeAsync(null, "TR", 2026, "TR-2026", existing.Id));
    }

    [Fact]
    public async Task The_country_layer_and_a_tenant_may_share_a_code()
    {
        // TenantId participates in the key, so this was already true — asserted here so narrowing the partial
        // filter cannot quietly take it away.
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Active));

        var tenantRow = Row(WorkingCalendarStatus.Active, TenantId);
        Assert.Null(await Record.ExceptionAsync(() => _repository.CreateAsync(tenantRow)));
    }

    [Fact]
    public async Task Single_active_remains_a_separate_invariant()
    {
        // Releasing the code on archive must not weaken "at most one active calendar per scope+country+year".
        // Different CODE, both active — uniqueness lets this through; ExistsActiveAsync is what must catch it.
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Active, code: "TR-2026"));

        Assert.False(await _repository.ExistsByCodeAsync(null, "TR", 2026, "TR-2026-B"));
        Assert.True(await _repository.ExistsActiveAsync(null, "TR", 2026, null));
    }

    [Fact]
    public async Task An_archived_row_does_not_satisfy_the_single_active_check_either()
    {
        await Collection.InsertOneAsync(Row(WorkingCalendarStatus.Archived));

        Assert.False(await _repository.ExistsActiveAsync(null, "TR", 2026, null));
    }
}
