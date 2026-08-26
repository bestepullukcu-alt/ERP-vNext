using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

// GetUsageRegistrationsAsync sorts UpdatedAt then CreatedAt — the same two-DateTimeOffset-key shape that
// broke the MOD-0023 gate lookup. These tests run it against a REAL MongoDB to establish whether it is
// broken too, rather than inferring it from the code shape.
public sealed class BusinessReferenceDataUsageLookupMongoTests : IAsyncLifetime
{
    private const string SetCode = "COUNTRY";

    private MongoIntegrationHarness _harness = null!;
    private BusinessReferenceDataStewardshipRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.BusinessReferenceData);
        _repository = new BusinessReferenceDataStewardshipRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    // The row that matters: an updated registration carries a non-null UpdatedAt, so both sort keys are
    // BSON arrays. This is the exact condition that makes MongoDB reject the query.
    [Fact]
    public async Task Usage_registrations_are_listed_when_rows_have_been_updated()
    {
        var older = await SeedAsync(createdAt: Now.AddHours(-5), updatedAt: Now.AddHours(-4), consumerName: "Organization");
        var newer = await SeedAsync(createdAt: Now.AddHours(-3), updatedAt: Now.AddHours(-1), consumerName: "LegalEntity");

        var rows = await _repository.GetUsageRegistrationsAsync(SetCode);

        Assert.Equal(2, rows.Count);
        Assert.Equal(newer.UsageRegistrationId, rows[0].UsageRegistrationId);
        Assert.Equal(older.UsageRegistrationId, rows[1].UsageRegistrationId);
    }

    // Never-updated rows keep UpdatedAt null, which serializes as BSON null rather than an array — the
    // single-array-key case the server accepts. Kept so the null placement stays pinned either way.
    [Fact]
    public async Task Never_updated_registrations_sort_behind_updated_ones()
    {
        var updated = await SeedAsync(createdAt: Now.AddDays(-9), updatedAt: Now.AddHours(-1), consumerName: "Organization");
        var neverUpdated = await SeedAsync(createdAt: Now, updatedAt: null, consumerName: "LegalEntity");

        var rows = await _repository.GetUsageRegistrationsAsync(SetCode);

        Assert.Equal(2, rows.Count);
        Assert.Equal(updated.UsageRegistrationId, rows[0].UsageRegistrationId);
        Assert.Equal(neverUpdated.UsageRegistrationId, rows[1].UsageRegistrationId);
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    /*
     * ⚠ EVERY SEEDED ROW NEEDS ITS OWN CONSUMER, AND THAT IS NOT COSMETIC. Production carries a unique index
     * on (TenantId, SetCode, ConsumerModule, ConsumerName): one module registers one usage of one set, once.
     * Both tests above used to seed two rows with the SAME consumer and pass — because the old harness built
     * no indexes at all, so the constraint production enforces simply did not exist here. The moment the
     * harness started building the BusinessReferenceData profile, Mongo rejected the second insert.
     *
     * That is the failure class this whole round is about, seen from the other side: a test can be green for
     * years against a schema production does not have. The sort behaviour under test is unchanged — it needs
     * two rows, not two identical consumers.
     */
    private async Task<BusinessReferenceDataUsageRegistration> SeedAsync(
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt,
        string consumerName)
    {
        var registration = new BusinessReferenceDataUsageRegistration
        {
            TenantId = _harness.TenantId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            SetCode = SetCode,
            ConsumerModule = "MOD-0288",
            ConsumerName = consumerName,
            IsActive = true,
            IsDeleted = false
        };

        await _harness.Database
            .GetCollection<BusinessReferenceDataUsageRegistration>(PlatformCollections.BusinessReferenceDataUsageRegistrations)
            .InsertOneAsync(registration);

        return registration;
    }
}
