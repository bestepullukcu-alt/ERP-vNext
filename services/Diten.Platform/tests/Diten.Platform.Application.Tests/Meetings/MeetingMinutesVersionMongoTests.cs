using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S6 (WP-MG-MOD0357-S6-MINUTES-01, AC6 proof (c)) — <c>MeetingMinutesVersionRepository</c> against a
/// REAL MongoDB. K4's whole promise — "two v1 rows for one meeting is impossible" — is a claim about the
/// UNIQUE (tenant, meeting, versionNumber) index in the schema manifest, not about anything the fake repository
/// (used by every other S6 test) can prove: the fake's own <c>TryCreateAsync</c> checks the SAME triple in LINQ
/// before inserting, so it would keep passing even if the real index were dropped. This file is the ONE place
/// that would go red if it were.
/// </summary>
public sealed class MeetingMinutesVersionMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingMinutesVersionRepository _repository = null!;
    private readonly Guid _meetingId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new MeetingMinutesVersionRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    // TenantId is set here for the record initializer's sake only — TenantRepository<T>.CreateAsync
    // overwrites it from the ACTIVE context on every write, which is exactly what the cross-tenant tests below
    // rely on (switching context, not the candidate row, is what changes which tenant a row lands in).
    private MeetingMinutesVersion Row(int versionNumber, MinutesStatus status = MinutesStatus.Draft, Guid? meetingId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _harness.TenantId,
        MeetingId = meetingId ?? _meetingId,
        VersionNumber = versionNumber,
        Status = status,
        CreatedBy = "test"
    };

    /// <summary>AC6 proof (c) — drop <c>ux_meeting_minutes_versions_tenant_meeting_version</c> from
    /// <c>PlatformSchemaManifest.Meetings.cs</c> and this test goes red: the second insert would succeed
    /// instead of being refused, and <see cref="MeetingMinutesVersionRepository.TryCreateAsync"/> would return
    /// the SAME (non-null) row it returns for the first.</summary>
    [Fact]
    public async Task A_second_v1_for_the_same_meeting_is_refused_by_the_real_unique_index()
    {
        var first = await _repository.TryCreateAsync(Row(1));
        Assert.NotNull(first);

        var second = await _repository.TryCreateAsync(Row(1));

        Assert.Null(second);
    }

    [Fact]
    public async Task A_second_meeting_or_a_second_version_number_is_NOT_a_collision()
    {
        Assert.NotNull(await _repository.TryCreateAsync(Row(1)));
        Assert.NotNull(await _repository.TryCreateAsync(Row(2))); // same meeting, next version
        Assert.NotNull(await _repository.TryCreateAsync(Row(1, meetingId: Guid.NewGuid()))); // different meeting, same number
    }

    [Fact]
    public async Task GetLatest_returns_the_highest_version_number()
    {
        await _repository.TryCreateAsync(Row(1, MinutesStatus.Published));
        await _repository.TryCreateAsync(Row(2, MinutesStatus.Published));
        await _repository.TryCreateAsync(Row(3, MinutesStatus.Draft));

        var latest = await _repository.GetLatestByMeetingIdAsync(_meetingId);

        Assert.Equal(3, latest!.VersionNumber);
    }

    [Fact]
    public async Task Another_tenants_context_cannot_read_this_tenants_minutes()
    {
        await _repository.TryCreateAsync(Row(1));

        var otherTenantId = Guid.NewGuid();
        _harness.TenantContext.SetTenant(otherTenantId);
        try
        {
            Assert.Null(await _repository.GetLatestByMeetingIdAsync(_meetingId));
            Assert.Empty(await _repository.ListByMeetingIdAsync(_meetingId));
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }
    }

    /// <summary>The same triple, in a DIFFERENT tenant's own context, is not a collision — the unique index is
    /// (tenant, meeting, version), not (meeting, version) alone. <c>TenantRepository&lt;T&gt;.CreateAsync</c>
    /// stamps <c>TenantId</c> from the ACTIVE context on write, so the second tenant is switched to for real,
    /// not merely passed on the candidate row.</summary>
    [Fact]
    public async Task The_same_meeting_id_and_version_number_in_a_different_tenant_is_not_a_collision()
    {
        Assert.NotNull(await _repository.TryCreateAsync(Row(1)));

        var otherTenantId = Guid.NewGuid();
        _harness.TenantContext.SetTenant(otherTenantId);
        try
        {
            Assert.NotNull(await _repository.TryCreateAsync(Row(1)));
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }
    }
}
