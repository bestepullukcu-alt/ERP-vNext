using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitContentSequence;

/// <summary>
/// WP-SB-3b — the <see cref="JourneyProgress"/> aggregate: <c>Advance</c> (next stage, wrap to 0 + Cycle + 1 after the
/// last stage — S3-7, exposure + 1), the read-side reset of a stale index, the string-Guid class map, the unique
/// (tenant, contact, product, journey) key and the tenant bound on every read. Also the frozen PlannedVisit content
/// item class map.
/// </summary>
public sealed class JourneyProgressTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Advance_moves_to_the_next_stage_and_counts_the_exposure()
    {
        var progress = new JourneyProgress();
        var report = Guid.NewGuid();

        progress.Advance(stageCount: 3, report, At);

        Assert.Equal(1, progress.CurrentStageIndex);
        Assert.Equal(0, progress.LastCompletedStageIndex);
        Assert.Equal((At, report), (progress.LastCompletedAt!.Value, progress.LastVisitReportId!.Value));
        Assert.Equal((0, 1), (progress.Cycle, progress.ExposureCount));
    }

    [Fact]
    public void Advance_after_the_last_stage_wraps_to_the_first_and_counts_a_cycle()
    {
        var progress = new JourneyProgress();

        progress.Advance(3, null, At);
        progress.Advance(3, null, At);
        progress.Advance(3, null, At); // the last stage is completed

        Assert.Equal(0, progress.CurrentStageIndex);
        Assert.Equal(2, progress.LastCompletedStageIndex);
        Assert.Equal((1, 3), (progress.Cycle, progress.ExposureCount));

        progress.Advance(3, null, At);
        Assert.Equal((1, 1, 4), (progress.CurrentStageIndex, progress.Cycle, progress.ExposureCount));
    }

    [Fact]
    public void A_stale_index_reads_and_advances_from_stage_zero()
    {
        var progress = new JourneyProgress { CurrentStageIndex = 5 };

        Assert.True(progress.IsStageIndexStale(3));
        Assert.Equal(0, progress.EffectiveStageIndex(3));
        Assert.Equal(5, progress.CurrentStageIndex); // a read never rewrites it

        progress.Advance(3, null, At);
        Assert.Equal((0, 1), (progress.LastCompletedStageIndex!.Value, progress.CurrentStageIndex));
    }

    [Fact]
    public void A_journey_without_stages_cannot_advance()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new JourneyProgress().Advance(0, null, At));

    [Fact]
    public void StageAfter_projects_with_the_same_wrap_as_advance()
    {
        Assert.Equal(1, JourneyProgress.StageAfter(0, 1, 3));
        Assert.Equal(0, JourneyProgress.StageAfter(2, 1, 3));
        Assert.Equal(2, JourneyProgress.StageAfter(1, 4, 3));
    }

    [Fact]
    public void Class_map_stores_the_key_guids_as_strings_and_round_trips()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var progress = new JourneyProgress
        {
            TenantId = Guid.NewGuid(), ContactId = Guid.NewGuid(), ProductId = Guid.NewGuid(), JourneyId = Guid.NewGuid(),
            CurrentStageIndex = 2, LastCompletedStageIndex = 1, LastVisitReportId = Guid.NewGuid(), Cycle = 3, ExposureCount = 7
        };

        var doc = progress.ToBsonDocument();
        foreach (var field in new[] { "ContactId", "ProductId", "JourneyId", "LastVisitReportId" })
        {
            Assert.Equal(BsonType.String, doc[field].BsonType);
        }

        var back = BsonSerializer.Deserialize<JourneyProgress>(doc);
        Assert.Equal(
            (progress.ContactId, progress.ProductId, progress.JourneyId, progress.LastVisitReportId, 2, 1, 3, 7),
            (back.ContactId, back.ProductId, back.JourneyId, back.LastVisitReportId, back.CurrentStageIndex,
                back.LastCompletedStageIndex, back.Cycle, back.ExposureCount));
    }

    [Fact]
    public void The_key_is_a_unique_index_over_tenant_contact_product_journey()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var index = JourneyProgressRepository.KeyIndex();
        var keys = index.Keys.Render(
            BsonSerializer.SerializerRegistry.GetSerializer<JourneyProgress>(), BsonSerializer.SerializerRegistry);
        var options = (MongoDB.Driver.CreateIndexOptions<JourneyProgress>)index.Options;

        Assert.Equal(new[] { "TenantId", "ContactId", "ProductId", "JourneyId" }, keys.Names);
        Assert.True(options.Unique);
        var partial = options.PartialFilterExpression.Render(
            BsonSerializer.SerializerRegistry.GetSerializer<JourneyProgress>(), BsonSerializer.SerializerRegistry);
        Assert.Equal(new BsonDocument("IsDeleted", false), partial); // an equality, never $ne
    }

    [Fact]
    public void Every_read_filter_is_bound_to_the_tenant()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var tenant = Guid.NewGuid();
        var contact = Guid.NewGuid();
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<JourneyProgress>();

        foreach (var filter in new[]
                 {
                     JourneyProgressRepository.ContactFilter(tenant, contact),
                     JourneyProgressRepository.KeyFilter(tenant, contact, Guid.NewGuid(), Guid.NewGuid())
                 })
        {
            var rendered = filter.Render(serializer, BsonSerializer.SerializerRegistry).ToString();
            Assert.Contains(tenant.ToString(), rendered);
            Assert.Contains(contact.ToString(), rendered); // string-Guid on both sides of the query
        }
    }

    [Fact]
    public async Task The_source_reader_reads_progress_only_for_the_server_resolved_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var contact = Guid.NewGuid();
        var repository = new InMemoryProgressRepository();
        repository.Rows.Add(new JourneyProgress { TenantId = tenantA, ContactId = contact, CurrentStageIndex = 2 });

        var asA = Reader(tenantA, repository);
        var asB = Reader(tenantB, repository);
        var noTenant = new VisitContentSourceReader(new TenantContext(), repository, null!, null!, null!);

        Assert.Single(await asA.ListProgressAsync(contact, default));
        Assert.Empty(await asB.ListProgressAsync(contact, default));
        Assert.Empty(await noTenant.ListProgressAsync(contact, default));
        Assert.Equal(new[] { tenantA, tenantB }, repository.TenantsAsked);
    }

    [Fact]
    public void Planned_visit_content_items_class_map_stores_guids_as_strings()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var item = new PlannedVisitContentItem
        {
            ProductId = Guid.NewGuid(), JourneyId = Guid.NewGuid(), StageId = Guid.NewGuid(), PathId = Guid.NewGuid(),
            Role = "promo", PathVersion = "2.0",
            Steps = { new PlannedVisitContentStep { StepId = Guid.NewGuid(), ContentId = Guid.NewGuid(), Minutes = 3 } },
            Claims = { new PlannedVisitContentClaim { ClaimId = Guid.NewGuid(), ClaimCode = "CL-1" } },
            Warnings = { "journey_audience_mismatch" }
        };
        var plan = new PlannedVisitEntity { ContentItems = { item } };

        var doc = plan.ToBsonDocument();
        var stored = doc["ContentItems"].AsBsonArray[0].AsBsonDocument;
        Assert.All(new[] { "ProductId", "JourneyId", "StageId", "PathId" }, f => Assert.Equal(BsonType.String, stored[f].BsonType));
        Assert.Equal(BsonType.String, stored["Steps"].AsBsonArray[0]["ContentId"].BsonType);
        Assert.Equal(BsonType.String, stored["Claims"].AsBsonArray[0]["ClaimId"].BsonType);

        var back = BsonSerializer.Deserialize<PlannedVisitEntity>(doc).ContentItems.Single();
        Assert.Equal((item.ProductId, item.PathId, "2.0", 3), (back.ProductId, back.PathId, back.PathVersion, back.Steps[0].Minutes!.Value));

        // A plan written before SB-3b (no ContentItems element) still reads — with an empty list.
        doc.Remove("ContentItems");
        Assert.Empty(BsonSerializer.Deserialize<PlannedVisitEntity>(doc).ContentItems);
    }

    private static VisitContentSourceReader Reader(Guid tenantId, IJourneyProgressRepository repository)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        return new VisitContentSourceReader(tenant, repository, null!, null!, null!);
    }

    private sealed class InMemoryProgressRepository : IJourneyProgressRepository
    {
        public List<JourneyProgress> Rows { get; } = new();
        public List<Guid> TenantsAsked { get; } = new();

        public Task<JourneyProgress?> GetByKeyAsync(
            Guid tenantId, Guid contactId, Guid productId, Guid journeyId, CancellationToken cancellationToken)
            => Task.FromResult(Rows.FirstOrDefault(r => r.TenantId == tenantId && r.ContactId == contactId
                                                        && r.ProductId == productId && r.JourneyId == journeyId));

        public Task<IReadOnlyList<JourneyProgress>> ListByContactAsync(
            Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        {
            TenantsAsked.Add(tenantId);
            return Task.FromResult<IReadOnlyList<JourneyProgress>>(
                Rows.Where(r => r.TenantId == tenantId && r.ContactId == contactId).ToList());
        }

        public Task<bool> UpsertAsync(JourneyProgress entity, int expectedVersion, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
