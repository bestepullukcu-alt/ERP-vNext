using Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W5 PLT-NOTIFY — the dispatch's source module over a REAL MongoDB: written and read back, filtered exactly (and
/// only within the tenant asked for), absent on a row written before the field existed, and the operator's list query
/// answers with it.
/// </summary>
public sealed class NotificationDispatchSourceModuleMongoTests : IAsyncLifetime
{
    private static readonly Guid Tenant = Guid.Parse("c7c7c7c7-0000-0000-0000-0000000000c7");
    private static readonly Guid OtherTenant = Guid.Parse("d8d8d8d8-0000-0000-0000-0000000000d8");

    private MongoIntegrationHarness _harness = null!;
    private NotificationDispatchRepository _dispatches = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("dispatch_source_module", SchemaProfile.Notification);
        _dispatches = new NotificationDispatchRepository(_harness.DbContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_list_filters_by_source_module_exactly_and_within_the_tenant_only()
    {
        var crm = await SeedAsync(Tenant, "MOD-0155");
        var platform = await SeedAsync(Tenant, null);
        await SeedAsync(Tenant, "MOD-0156");
        await SeedAsync(OtherTenant, "MOD-0155");

        var onlyCrm = await _dispatches.ListByTenantAsync(Tenant, 0, 50, null, null, null, null, "MOD-0155");
        var everything = await _dispatches.ListByTenantAsync(Tenant);
        var unfilteredNewOverload = await _dispatches.ListByTenantAsync(Tenant, 0, 50, null, null, null, null, sourceModule: null);

        Assert.Equal([crm.Id], onlyCrm.Select(d => d.Id));
        Assert.Equal("MOD-0155", Assert.Single(onlyCrm).SourceModule);
        Assert.Equal(3, everything.Count);
        Assert.Equal(everything.Select(d => d.Id).OrderBy(x => x), unfilteredNewOverload.Select(d => d.Id).OrderBy(x => x));
        Assert.Null(everything.Single(d => d.Id == platform.Id).SourceModule);
    }

    [Fact]
    public async Task A_row_written_before_the_field_existed_reads_as_no_source_module()
    {
        var seeded = await SeedAsync(Tenant, "MOD-0155");
        var collection = _harness.Database.GetCollection<NotificationDispatch>(PlatformCollections.NotificationDispatches);
        // The same row, as an older writer left it: no SourceModule element at all.
        var unset = await collection.UpdateOneAsync(
            Builders<NotificationDispatch>.Filter.Eq(d => d.Id, seeded.Id),
            Builders<NotificationDispatch>.Update.Unset(d => d.SourceModule));
        Assert.Equal(1, unset.ModifiedCount);
        var raw = await _harness.Database.GetCollection<BsonDocument>(PlatformCollections.NotificationDispatches)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.False(Assert.Single(raw).Contains("SourceModule"));

        var read = await _dispatches.GetByIdForTenantAsync(Tenant, seeded.Id);

        Assert.NotNull(read);
        Assert.Null(read!.SourceModule);
        Assert.Empty(await _dispatches.ListByTenantAsync(Tenant, 0, 50, null, null, null, null, "MOD-0155"));
    }

    [Fact]
    public async Task The_operator_list_query_filters_by_and_answers_with_the_source_module()
    {
        await SeedAsync(Tenant, "MOD-0155");
        await SeedAsync(Tenant, null);

        var handler = new GetNotificationDispatchListHandler(_dispatches);
        var filtered = await handler.Handle(new GetNotificationDispatchListQuery(Tenant, SourceModule: "  MOD-0155 "), CancellationToken.None);
        var all = await handler.Handle(new GetNotificationDispatchListQuery(Tenant), CancellationToken.None);

        Assert.True(filtered.IsSuccessful);
        Assert.Equal("MOD-0155", Assert.Single(filtered.Data!).SourceModule);
        Assert.Equal(2, all.Data!.Count);
        Assert.Contains(all.Data, item => item.SourceModule is null);
    }

    private async Task<NotificationDispatch> SeedAsync(Guid tenant, string? sourceModule)
    {
        var dispatch = new NotificationDispatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            TemplateKey = "crm.visitreport.submitted",
            Locale = "tr",
            Status = NotificationDispatchStatus.Sent,
            Subject = "s",
            To = [new EmailRecipient { Email = "m@a.test", DisplayName = "M" }],
            QueuedAt = DateTimeOffset.UtcNow,
            SourceModule = sourceModule
        };
        return await _dispatches.CreateAsync(dispatch);
    }
}
