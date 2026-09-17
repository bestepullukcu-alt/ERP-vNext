using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S11 — <see cref="MeetingSeriesRepository"/> against a REAL MongoDB, not the in-memory
/// <see cref="FakeMeetingSeriesRepository"/> every handler test in this folder is built on. The fake reproduces
/// the unique-name check in LINQ (see <c>FindByNameAsync</c>) and would keep passing even if the real
/// <c>ux_meeting_series_tenant_name</c> index were dropped — this file is the ONE place that would go red.
/// Shared-database pattern reused from <c>MongoIntegrationHarness</c>, exactly as
/// <c>MeetingMinutesVersionMongoTests</c>/<c>RecordLinkRepositoryMongoTests</c> already establish for this module.
/// </summary>
public sealed class MeetingSeriesMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingSeriesRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new MeetingSeriesRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private MeetingSeries Series(string name, Guid? tenantId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId ?? _harness.TenantId,
        Name = name,
        MeetingTypeId = Guid.NewGuid(),
        Frequency = MeetingSeriesFrequency.Weekly,
        Interval = 1,
        StartsAt = DateTimeOffset.UtcNow.AddDays(7),
        OrganizerUserId = Guid.NewGuid(),
        LeadTimeDays = 14,
        CreatedBy = "test"
    };

    /// <summary>The storage-level guarantee behind "two series with the same name in one tenant is impossible":
    /// drop <c>ux_meeting_series_tenant_name</c> and this test goes red — the second insert would succeed
    /// instead of throwing, and <see cref="MeetingSeriesRepository.CreateAsync"/> would return normally instead
    /// of the driver rejecting the write.</summary>
    [Fact]
    public async Task A_second_series_with_the_same_name_in_the_same_tenant_is_refused_by_the_real_unique_index()
    {
        await _repository.CreateAsync(Series("Haftalık Kalite Toplantısı"));

        await Assert.ThrowsAsync<MongoWriteException>(
            () => _repository.CreateAsync(Series("Haftalık Kalite Toplantısı")));
    }

    [Fact]
    public async Task The_same_name_in_a_different_tenant_is_not_a_collision()
    {
        await _repository.CreateAsync(Series("Haftalık Kalite Toplantısı"));

        var otherTenantId = Guid.NewGuid();
        _harness.TenantContext.SetTenant(otherTenantId);
        try
        {
            var created = await _repository.CreateAsync(Series("Haftalık Kalite Toplantısı", otherTenantId));
            Assert.NotNull(created);
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }
    }

    [Fact]
    public async Task Another_tenants_context_cannot_read_this_tenants_series()
    {
        var created = await _repository.CreateAsync(Series("Aylık Yönetim Gözden Geçirmesi"));

        var otherTenantId = Guid.NewGuid();
        _harness.TenantContext.SetTenant(otherTenantId);
        try
        {
            Assert.Null(await _repository.GetByIdAsync(created.Id));
            Assert.Empty(await _repository.ListAllAsync());
            Assert.Empty(await _repository.ListActiveAsync());
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }
    }

    [Fact]
    public async Task ListActiveAsync_excludes_a_deactivated_series_but_ListAllAsync_still_shows_it()
    {
        var active = Series("Aktif Seri");
        var inactive = Series("Pasif Seri");
        inactive.IsActive = false;
        await _repository.CreateAsync(active);
        await _repository.CreateAsync(inactive);

        var activeList = await _repository.ListActiveAsync();
        var allList = await _repository.ListAllAsync();

        Assert.Contains(activeList, s => s.Id == active.Id);
        Assert.DoesNotContain(activeList, s => s.Id == inactive.Id);
        Assert.Contains(allList, s => s.Id == active.Id);
        Assert.Contains(allList, s => s.Id == inactive.Id);
    }

    [Fact]
    public async Task An_expected_version_write_conflict_is_reported_as_false_not_thrown()
    {
        var created = await _repository.CreateAsync(Series("Çeyreklik Değerlendirme"));

        var firstUpdate = await _repository.UpdateAsync(created, created.Version);
        var staleSecondUpdate = await _repository.UpdateAsync(created, created.Version - 1);

        Assert.True(firstUpdate);
        Assert.False(staleSecondUpdate);
    }

    [Fact]
    public async Task Deleting_a_series_soft_deletes_it_and_frees_its_name_for_reuse()
    {
        var created = await _repository.CreateAsync(Series("Silinecek Seri"));

        await _repository.DeleteAsync(created.Id);

        Assert.Null(await _repository.GetByIdAsync(created.Id));
        // The unique index joins IsDeleted — a soft-deleted row's name is free again.
        var recreated = await _repository.CreateAsync(Series("Silinecek Seri"));
        Assert.NotNull(recreated);
    }
}
