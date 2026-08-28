using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// In-memory test doubles for the MOD-0167 FU04 runtime.
/// <para>Every FOREIGN repository double counts its write calls, because "a template binds and never produces" is a
/// CONTRACT of this FU rather than an aspiration: only a counter can prove that no code path writes a segment, a
/// frequency policy or a content row. The same is true of the reference validator, where the per-request dedup is a
/// counted promise.</para>
/// </summary>
internal static class StrategyTemplateTestDoubles
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly DateTimeOffset Past = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Future = new(2999, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static TenantContext Tenant(Guid id)
    {
        var context = new TenantContext();
        context.SetTenant(id);
        return context;
    }
}

/// <summary>Template store. Reads hand back a COPY, exactly as a document store does, so a handler that mutates what it
/// loaded cannot appear to have written when its optimistic replace was rejected.</summary>
internal sealed class FakeStrategyTemplateRepository : IStrategyTemplateRepository
{
    public List<TemplateEntity> Rows { get; } = new();
    public int InsertCalls { get; private set; }
    public int ReplaceCalls { get; private set; }

    public Task<TemplateEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Copy(Rows.FirstOrDefault(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted)));

    public Task<IReadOnlyList<TemplateEntity>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TemplateEntity>>(
            Rows.Where(t => t.TenantId == tenantId && !t.IsDeleted)
                .Select(Copy)
                .Select(t => t!)
                .OrderBy(t => t.TemplateCode)
                .ThenBy(t => t.TemplateVersion)
                .ToList());

    public Task<IReadOnlyList<TemplateEntity>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TemplateEntity>>(
            Rows.Where(t => t.TenantId == tenantId && !t.IsDeleted && t.VersionLineageId == versionLineageId)
                .Select(Copy)
                .Select(t => t!)
                .OrderBy(t => t.TemplateVersion)
                .ToList());

    public Task<IReadOnlyList<TemplateEntity>> ListByCodeAsync(
        Guid tenantId, string templateCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TemplateEntity>>(
            Rows.Where(t => t.TenantId == tenantId
                            && !t.IsDeleted
                            && string.Equals(t.TemplateCode, templateCode, StringComparison.OrdinalIgnoreCase))
                .Select(Copy)
                .Select(t => t!)
                .OrderBy(t => t.TemplateVersion)
                .ToList());

    public Task InsertAsync(TemplateEntity entity, CancellationToken cancellationToken)
    {
        InsertCalls++;
        Rows.Add(Copy(entity)!);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(TemplateEntity entity, int expectedVersion, CancellationToken cancellationToken)
    {
        ReplaceCalls++;
        var stored = Rows.FirstOrDefault(t => t.Id == entity.Id && t.TenantId == entity.TenantId);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        Rows.Remove(stored);
        entity.Version = expectedVersion + 1;
        Rows.Add(Copy(entity)!);
        return Task.FromResult(true);
    }

    public TemplateEntity Stored(Guid id) => Rows.Single(t => t.Id == id);

    private static TemplateEntity? Copy(TemplateEntity? source)
    {
        if (source is null)
        {
            return null;
        }

        var (segments, frequency, products, contents) = CloneChildren(source);
        return new TemplateEntity
        {
            Id = source.Id,
            TenantId = source.TenantId,
            IsDeleted = source.IsDeleted,
            DeletedAt = source.DeletedAt,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Version = source.Version,
            TemplateCode = source.TemplateCode,
            TemplateName = source.TemplateName,
            SubjectType = source.SubjectType,
            TemplateStatus = source.TemplateStatus,
            TemplateVersion = source.TemplateVersion,
            VersionLineageId = source.VersionLineageId,
            SupersededByTemplateId = source.SupersededByTemplateId,
            BusinessUnitId = source.BusinessUnitId,
            Description = source.Description,
            Notes = source.Notes,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            SegmentBindings = segments,
            FrequencyIntent = frequency,
            ProductLines = products,
            ContentBindings = contents,
            BindingsFrozenAt = source.BindingsFrozenAt,
            ActivatedAt = source.ActivatedAt,
            ActivatedBy = source.ActivatedBy,
            ArchivedAt = source.ArchivedAt,
            ArchivedBy = source.ArchivedBy,
            CreatedBy = source.CreatedBy,
            UpdatedBy = source.UpdatedBy
        };
    }

    /// <summary>A deep copy that KEEPS the child ids (unlike the new-version clone, which regenerates them).</summary>
    private static (List<StrategyTemplateSegmentBinding>, StrategyTemplateFrequencyIntent,
        List<StrategyTemplateProductLine>, List<StrategyTemplateContentBinding>) CloneChildren(TemplateEntity source)
    {
        var segments = source.SegmentBindings.Select(b => new StrategyTemplateSegmentBinding
        {
            BindingId = b.BindingId,
            SegmentId = b.SegmentId,
            SegmentLineageId = b.SegmentLineageId,
            SegmentVersionAtBinding = b.SegmentVersionAtBinding,
            SegmentCodeDisplay = b.SegmentCodeDisplay,
            BindingRole = b.BindingRole,
            SortOrder = b.SortOrder,
            Notes = b.Notes
        }).ToList();

        var frequency = new StrategyTemplateFrequencyIntent
        {
            Mode = source.FrequencyIntent.Mode,
            VisitFrequencyPolicyId = source.FrequencyIntent.VisitFrequencyPolicyId,
            PolicyCodeDisplay = source.FrequencyIntent.PolicyCodeDisplay,
            FrequencyType = source.FrequencyIntent.FrequencyType,
            RequiredVisitCount = source.FrequencyIntent.RequiredVisitCount,
            PeriodType = source.FrequencyIntent.PeriodType,
            IntentNote = source.FrequencyIntent.IntentNote
        };

        var products = source.ProductLines.Select(l => new StrategyTemplateProductLine
        {
            LineId = l.LineId,
            GlobalProductId = l.GlobalProductId,
            GlobalProductCodeDisplay = l.GlobalProductCodeDisplay,
            LineWeightPercentage = l.LineWeightPercentage,
            SkuAllocationMode = l.SkuAllocationMode,
            SortOrder = l.SortOrder,
            Notes = l.Notes,
            SkuAllocations = l.SkuAllocations.Select(a => new StrategyTemplateSkuAllocation
            {
                AllocationId = a.AllocationId,
                GskuId = a.GskuId,
                GskuCanonicalCodeDisplay = a.GskuCanonicalCodeDisplay,
                Percentage = a.Percentage,
                SortOrder = a.SortOrder
            }).ToList()
        }).ToList();

        var contents = source.ContentBindings.Select(c => new StrategyTemplateContentBinding
        {
            BindingId = c.BindingId,
            ContentRefType = c.ContentRefType,
            ContentRefId = c.ContentRefId,
            ContentCodeDisplay = c.ContentCodeDisplay,
            ContentVersionAtBinding = c.ContentVersionAtBinding,
            SortOrder = c.SortOrder,
            Notes = c.Notes
        }).ToList();

        return (segments, frequency, products, contents);
    }
}

/// <summary>Segment store — READ-only from this FU's point of view. Every write method counts, and the no-write guard
/// test asserts the counters stay at zero.</summary>
internal sealed class FakeSegmentReadRepository : ISegmentRepository
{
    public List<Segment> Rows { get; } = new();
    public int WriteCalls { get; private set; }

    public Task<Segment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id && !s.IsDeleted));

    public Task<IReadOnlyList<Segment>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Segment>>(Rows.Where(s => s.TenantId == tenantId).ToList());

    public Task<IReadOnlyList<Segment>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Segment>>(
            Rows.Where(s => s.TenantId == tenantId && s.VersionLineageId == versionLineageId).ToList());

    public Task<IReadOnlyList<Segment>> ListByCodeAsync(
        Guid tenantId, string segmentCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Segment>>(
            Rows.Where(s => s.TenantId == tenantId && s.SegmentCode == segmentCode).ToList());

    public Task InsertAsync(Segment entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(Segment entity, int expectedVersion, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult(true);
    }

    public Segment Add(
        Guid tenantId,
        string code = "cardio-a",
        string subjectType = SegmentSubjectTypes.Contact,
        string status = SegmentStatuses.Active,
        bool archived = false)
    {
        var id = Guid.NewGuid();
        var segment = new Segment
        {
            Id = id,
            TenantId = tenantId,
            SegmentCode = code,
            SegmentName = code,
            SegmentType = SegmentTypes.Dynamic,
            SubjectType = subjectType,
            SegmentStatus = archived ? SegmentStatuses.Archived : status,
            SegmentVersion = 1,
            VersionLineageId = id,
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            ArchivedAt = archived ? StrategyTemplateTestDoubles.Now : null
        };
        Rows.Add(segment);
        return segment;
    }
}

/// <summary>Frequency policy store — READ-only from this FU. <see cref="WriteCalls"/> proves no policy is ever created
/// or updated by a template write, in ANY intent mode.</summary>
internal sealed class FakeVisitFrequencyPolicyRepository : IVisitFrequencyPolicyRepository
{
    public List<VisitFrequencyPolicy> Rows { get; } = new();
    public int WriteCalls { get; private set; }

    public Task<VisitFrequencyPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted));

    public Task<IReadOnlyList<VisitFrequencyPolicy>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<VisitFrequencyPolicy>>(Rows.Where(p => p.TenantId == tenantId).ToList());

    public Task<VisitFrequencyPolicy?> GetActiveByCodeAsync(
        Guid tenantId, string policyCode, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(p => p.TenantId == tenantId && p.PolicyCode == policyCode));

    public Task<IReadOnlyList<VisitFrequencyPolicy>> ListActiveByTargetsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> targetIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<VisitFrequencyPolicy>>(
            Rows.Where(p => p.TenantId == tenantId && targetIds.Contains(p.TargetId)).ToList());

    public Task InsertAsync(VisitFrequencyPolicy policy, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(VisitFrequencyPolicy policy, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public VisitFrequencyPolicy Add(
        Guid tenantId,
        string status = FrequencyPolicyStatus.Active,
        string targetType = FrequencyTargetType.Contact,
        Guid targetId = default)
    {
        var policy = new VisitFrequencyPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyCode = "weekly-core",
            PolicyName = "Weekly core",
            TargetType = targetType,
            TargetId = targetId == default ? Guid.NewGuid() : targetId,
            FrequencyType = FrequencyType.Weekly,
            RequiredVisitCount = 2,
            PeriodType = FrequencyPeriodType.Week,
            Priority = 400,
            Source = FrequencySource.Manual,
            Status = status,
            EffectiveFrom = StrategyTemplateTestDoubles.Past
        };
        Rows.Add(policy);
        return policy;
    }
}

/// <summary>KnowledgePath store — READ-only from this FU.</summary>
internal sealed class FakeKnowledgePathRepository : IKnowledgePathRepository
{
    public List<KnowledgePath> Rows { get; } = new();
    public int WriteCalls { get; private set; }

    public Task<KnowledgePath?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted));

    public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<KnowledgePath>>(Rows.Where(p => p.TenantId == tenantId).ToList());

    public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(
        Guid tenantId, string pathCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<KnowledgePath>>(
            Rows.Where(p => p.TenantId == tenantId && p.PathCode == pathCode).ToList());

    public Task InsertAsync(KnowledgePath entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult(true);
    }

    public KnowledgePath Add(
        Guid tenantId, string status = KnowledgePathStatuses.Published, bool archived = false)
    {
        var path = new KnowledgePath
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PathCode = "onboarding",
            PathName = "Onboarding",
            SubjectId = Guid.NewGuid(),
            Objective = "Teach the basics",
            PathVersion = "1.0",
            PathStatus = status,
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            ArchivedAt = archived ? StrategyTemplateTestDoubles.Now : null
        };
        Rows.Add(path);
        return path;
    }
}

/// <summary>ContentEngagementJourney store — READ-only from this FU.</summary>
internal sealed class FakeContentEngagementJourneyRepository : IContentEngagementJourneyRepository
{
    public List<ContentEngagementJourney> Rows { get; } = new();
    public int WriteCalls { get; private set; }

    public Task<ContentEngagementJourney?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted));

    public Task<IReadOnlyList<ContentEngagementJourney>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ContentEngagementJourney>>(
            Rows.Where(j => j.TenantId == tenantId).ToList());

    public Task<IReadOnlyList<ContentEngagementJourney>> ListByCodeAsync(
        Guid tenantId, string journeyCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ContentEngagementJourney>>(
            Rows.Where(j => j.TenantId == tenantId && j.JourneyCode == journeyCode).ToList());

    public Task InsertAsync(ContentEngagementJourney entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(
        ContentEngagementJourney entity, int expectedVersion, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult(true);
    }

    public ContentEngagementJourney Add(
        Guid tenantId, string status = ContentEngagementJourneyStatuses.Published, bool archived = false)
    {
        var journey = new ContentEngagementJourney
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JourneyCode = "adoption",
            JourneyName = "Adoption",
            SubjectId = Guid.NewGuid(),
            Objective = "Drive adoption",
            JourneyVersion = "1.0",
            JourneyStatus = status,
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            ArchivedAt = archived ? StrategyTemplateTestDoubles.Now : null
        };
        Rows.Add(journey);
        return journey;
    }
}

/// <summary>
/// MDM reference validator double. It records EVERY call, so two things can be proven rather than assumed: that each
/// distinct id is proven exactly once per request (dedup), and that a proof runs BEFORE any persistence.
/// </summary>
internal sealed class FakeStrategyReferenceValidator : IStrategyTemplateProductReferenceValidator
{
    public List<(string Kind, Guid Id)> Calls { get; } = new();
    public HashSet<Guid> NotFound { get; } = new();
    public HashSet<Guid> Unavailable { get; } = new();
    public bool AllUnavailable { get; set; }

    public Task<IStrategyTemplateProductReferenceValidator.Outcome> ValidateAsync(
        string referenceKind, Guid referenceId, CancellationToken cancellationToken)
    {
        Calls.Add((referenceKind, referenceId));

        if (AllUnavailable || Unavailable.Contains(referenceId))
        {
            return Task.FromResult(IStrategyTemplateProductReferenceValidator.Outcome.Unavailable);
        }

        return Task.FromResult(NotFound.Contains(referenceId)
            ? IStrategyTemplateProductReferenceValidator.Outcome.NotFound
            : IStrategyTemplateProductReferenceValidator.Outcome.Valid);
    }
}
