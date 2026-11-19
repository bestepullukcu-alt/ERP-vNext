using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Persistence.Warehouse;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — the temporary RCS-001 counter behind <see cref="IWarehouseRecordCodeAllocator"/> (pack §8.7, §22; Q-07 B), resolved from the
/// REAL host's container on the suite's own replica set: <c>{COMPANY}-{TYPE}-{000001}</c>, one atomic counter per (tenant, company prefix, type),
/// never reset, unique tenant-wide, growing past six digits (AC-16's counter part; task numbering itself is DL-2).
/// K3 sabotage (s): make the increment a read-then-write → the concurrent test issues one number twice → red.
/// </summary>
public sealed class WarehouseRecordCodeAllocatorTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public WarehouseRecordCodeAllocatorTests(WarehouseApiFactory factory) => _factory = factory;

    private Guid NewTenant()
    {
        var tenant = Guid.NewGuid();
        _factory.TrackWarehouseTenant(tenant);
        return tenant;
    }

    private async Task<string> NextAsync(Guid tenant, string company, string type)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(tenant, Guid.NewGuid(), Guid.NewGuid());
        return await scope.ServiceProvider.GetRequiredService<IWarehouseRecordCodeAllocator>().NextAsync(company, type);
    }

    [Fact]
    public async Task The_first_numbers_of_a_counter_are_000001_000002_in_the_rcs_format()
    {
        var tenant = NewTenant();
        Assert.Equal("GMG-PUT-000001", await NextAsync(tenant, "GMG", "PUT"));
        Assert.Equal("GMG-PUT-000002", await NextAsync(tenant, "GMG", "PUT"));
    }

    [Fact]
    public async Task Concurrent_callers_never_get_the_same_number()
    {
        var tenant = NewTenant();
        var numbers = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() => NextAsync(tenant, "ACME", "PUT"))));

        Assert.Equal(40, numbers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(1, 40).Select(n => $"ACME-PUT-{n:D6}"), numbers.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_counter_is_per_tenant_company_prefix_and_type()
    {
        var tenant = NewTenant();
        var other = NewTenant();
        Assert.Equal("GMG-PUT-000001", await NextAsync(tenant, "GMG", "PUT"));
        Assert.Equal("GMG-PUT-000002", await NextAsync(tenant, "GMG", "PUT"));
        Assert.Equal("GMG-PCK-000001", await NextAsync(tenant, "GMG", "PCK"));
        Assert.Equal("XY-PUT-000001", await NextAsync(tenant, "XY", "PUT"));
        Assert.Equal("GMG-PUT-000001", await NextAsync(other, "GMG", "PUT"));   // another tenant's own counter
        Assert.Equal("GMG-PUT-000003", await NextAsync(tenant, "GMG", "PUT"));  // never reset by the others

        var counters = _factory.Collection(WarehouseCollections.NumberCounters).Find(new BsonDocument("TenantId", tenant.ToString("D"))).ToList();
        Assert.Equal(3, counters.Count);
    }

    [Fact]
    public async Task Past_999999_the_number_grows_to_seven_digits_and_never_wraps()
    {
        var tenant = NewTenant();
        await _factory.Collection(WarehouseCollections.NumberCounters).InsertOneAsync(new BsonDocument
        {
            ["TenantId"] = tenant.ToString("D"), ["CompanyCodePrefix"] = "GMG", ["TypePrefix"] = "PUT", ["Seq"] = 999_998L
        });

        Assert.Equal("GMG-PUT-999999", await NextAsync(tenant, "GMG", "PUT"));
        Assert.Equal("GMG-PUT-1000000", await NextAsync(tenant, "GMG", "PUT"));
        Assert.Equal("GMG-PUT-1000001", await NextAsync(tenant, "GMG", "PUT"));
    }

    [Theory]
    [InlineData("gmg", "PUT")]
    [InlineData("G", "PUT")]
    [InlineData("ABCDEF", "PUT")]
    [InlineData("GMG", "XYZ")]
    public async Task An_invalid_prefix_or_type_is_refused_before_the_counter_moves(string company, string type)
    {
        var tenant = NewTenant();
        await Assert.ThrowsAsync<ArgumentException>(() => NextAsync(tenant, company, type));
        Assert.Empty(_factory.Collection(WarehouseCollections.NumberCounters).Find(new BsonDocument("TenantId", tenant.ToString("D"))).ToList());
    }

    [Fact]
    public async Task One_counter_document_per_key_is_enforced_by_the_unique_index()
    {
        var tenant = NewTenant();
        await NextAsync(tenant, "GMG", "PUT");
        var duplicate = new BsonDocument { ["TenantId"] = tenant.ToString("D"), ["CompanyCodePrefix"] = "GMG", ["TypePrefix"] = "PUT", ["Seq"] = 5L };
        var refused = await Assert.ThrowsAsync<MongoWriteException>(() => _factory.Collection(WarehouseCollections.NumberCounters).InsertOneAsync(duplicate));
        Assert.Contains(WarehouseIndexConfiguration.CounterIndex, refused.Message, StringComparison.Ordinal);
    }
}
