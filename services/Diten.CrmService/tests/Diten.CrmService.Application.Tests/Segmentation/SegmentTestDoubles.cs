using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// In-memory test doubles for the MOD-0167 FU02 runtime. Every reader counts its own calls, because the N+1 ban is a
/// CONTRACT of this FU rather than an aspiration: a resolution must touch each source a fixed number of times no matter
/// how many candidates it evaluates, and only a counter can prove that.
/// </summary>
internal static class SegmentTestDoubles
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly DateTimeOffset Past = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Future = new(2999, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static TenantContext Tenant(Guid id)
    {
        var context = new TenantContext();
        context.SetTenant(id);
        return context;
    }
}

/// <summary>Segment store. Replace honours the optimistic token, so a concurrency conflict is reproducible.</summary>
internal sealed class FakeSegmentRepository : ISegmentRepository
{
    public List<Segment> Rows { get; } = new();

    // Reads hand back a COPY, exactly as a document store does. Without that, a handler mutating the entity it loaded
    // would appear to have written even when its optimistic replace was rejected, and the "no silent overwrite" test
    // would be testing nothing.
    public Task<Segment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Copy(Rows.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id && !s.IsDeleted)));

    private static Segment? Copy(Segment? source)
    {
        if (source is null)
        {
            return null;
        }

        var clone = new Segment
        {
            Id = source.Id,
            TenantId = source.TenantId,
            IsDeleted = source.IsDeleted,
            DeletedAt = source.DeletedAt,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Version = source.Version,
            SegmentCode = source.SegmentCode,
            SegmentName = source.SegmentName,
            SegmentType = source.SegmentType,
            SubjectType = source.SubjectType,
            SegmentStatus = source.SegmentStatus,
            SegmentVersion = source.SegmentVersion,
            VersionLineageId = source.VersionLineageId,
            SupersededBySegmentId = source.SupersededBySegmentId,
            BusinessUnitId = source.BusinessUnitId,
            Description = source.Description,
            Notes = source.Notes,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            MatchMode = source.MatchMode,
            CriteriaFrozenAt = source.CriteriaFrozenAt,
            ActivatedAt = source.ActivatedAt,
            ActivatedBy = source.ActivatedBy,
            ArchivedAt = source.ArchivedAt,
            ArchivedBy = source.ArchivedBy,
            CreatedBy = source.CreatedBy,
            UpdatedBy = source.UpdatedBy
        };

        clone.Criteria = source.Criteria.Select(n => new SegmentCriteriaNode
        {
            NodeId = n.NodeId,
            ParentNodeId = n.ParentNodeId,
            NodeKind = n.NodeKind,
            GroupOperator = n.GroupOperator,
            AttributeCode = n.AttributeCode,
            Operator = n.Operator,
            Values = new List<string>(n.Values),
            ValueType = n.ValueType,
            Parameters = new Dictionary<string, string>(n.Parameters),
            Negate = n.Negate,
            SortOrder = n.SortOrder,
            Label = n.Label
        }).ToList();
        return clone;
    }

    public Task<IReadOnlyList<Segment>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Segment>>(
            Rows.Where(s => s.TenantId == tenantId && !s.IsDeleted).ToList());

    public Task<IReadOnlyList<Segment>> ListByLineageAsync(
        Guid tenantId, Guid versionLineageId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Segment>>(
            Rows.Where(s => s.TenantId == tenantId && !s.IsDeleted && s.VersionLineageId == versionLineageId)
                .OrderBy(s => s.SegmentVersion).ToList());

    public Task<IReadOnlyList<Segment>> ListByCodeAsync(
        Guid tenantId, string segmentCode, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Segment>>(
            Rows.Where(s => s.TenantId == tenantId && !s.IsDeleted
                            && string.Equals(s.SegmentCode, segmentCode, StringComparison.OrdinalIgnoreCase))
                .ToList());

    public Task InsertAsync(Segment entity, CancellationToken cancellationToken)
    {
        Rows.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(Segment entity, int expectedVersion, CancellationToken cancellationToken)
    {
        var index = Rows.FindIndex(s => s.Id == entity.Id && s.TenantId == entity.TenantId);
        if (index < 0 || Rows[index].Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        Rows[index] = entity;
        return Task.FromResult(true);
    }
}

internal sealed class FakeTargetCustomerRepository : ITargetCustomerRepository
{
    public List<TargetCustomer> Rows { get; } = new();

    public Task<TargetCustomer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(t => t.TenantId == tenantId && t.Id == id && !t.IsDeleted));

    public Task<IReadOnlyList<TargetCustomer>> ListBySegmentAsync(
        Guid tenantId, Guid segmentId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TargetCustomer>>(
            Rows.Where(t => t.TenantId == tenantId && t.SegmentId == segmentId && !t.IsDeleted).ToList());

    public Task<IReadOnlyList<TargetCustomer>> ListBySubjectAsync(
        Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TargetCustomer>>(
            Rows.Where(t => t.TenantId == tenantId && !t.IsDeleted
                            && t.SubjectId == subjectId
                            && string.Equals(t.SubjectType, subjectType, StringComparison.Ordinal))
                .ToList());

    public Task InsertAsync(TargetCustomer entity, CancellationToken cancellationToken)
    {
        Rows.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(TargetCustomer entity, int expectedVersion, CancellationToken cancellationToken)
    {
        var index = Rows.FindIndex(t => t.Id == entity.Id && t.TenantId == entity.TenantId);
        if (index < 0 || Rows[index].Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        Rows[index] = entity;
        return Task.FromResult(true);
    }
}

/// <summary>Candidate source with call counters. <see cref="LoadCandidatesCalls"/> staying at zero is how the
/// "a static segment never runs the criteria engine" rule is PROVEN rather than asserted.</summary>
internal sealed class FakeCandidateSource : ISegmentCandidateSource
{
    public List<SegmentSubjectSnapshot> Candidates { get; } = new();
    public List<SegmentLinkProjection> Links { get; } = new();
    public List<SegmentAccountAttributeProjection> Attributes { get; } = new();
    public bool ForceCapExceeded { get; set; }

    public int LoadCandidatesCalls { get; private set; }
    public int LoadSubjectsCalls { get; private set; }
    public int LoadLinksCalls { get; private set; }
    public int LoadAttributesCalls { get; private set; }

    public Task<SegmentCandidateLoad> LoadCandidatesAsync(
        Guid tenantId, string subjectType, IReadOnlyList<SegmentCriteriaNode> criteria, string matchMode,
        int cap, CancellationToken cancellationToken)
    {
        LoadCandidatesCalls++;
        return Task.FromResult(ForceCapExceeded
            ? new SegmentCandidateLoad(Array.Empty<SegmentSubjectSnapshot>(), true, cap)
            : new SegmentCandidateLoad(Candidates.ToList(), false, cap));
    }

    public Task<IReadOnlyList<SegmentSubjectSnapshot>> LoadSubjectsByIdsAsync(
        Guid tenantId, string subjectType, IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        LoadSubjectsCalls++;
        return Task.FromResult<IReadOnlyList<SegmentSubjectSnapshot>>(
            Candidates.Where(c => subjectIds.Contains(c.SubjectId)).ToList());
    }

    public Task<IReadOnlyList<SegmentLinkProjection>> LoadLinksAsync(
        Guid tenantId, string subjectType, IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        LoadLinksCalls++;
        return Task.FromResult<IReadOnlyList<SegmentLinkProjection>>(
            Links.Where(l => subjectIds.Contains(l.ContactId) || subjectIds.Contains(l.AccountId)).ToList());
    }

    public Task<IReadOnlyList<SegmentAccountAttributeProjection>> LoadAccountAttributesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        LoadAttributesCalls++;
        return Task.FromResult<IReadOnlyList<SegmentAccountAttributeProjection>>(
            Attributes.Where(a => accountIds.Contains(a.AccountId)).ToList());
    }
}

internal sealed class FakeConsentBulkReader : ISegmentConsentBulkReader
{
    public List<ConsentRecord> Consents { get; } = new();
    public List<PreferenceRecord> Preferences { get; } = new();
    public int Calls { get; private set; }

    public Task<SegmentConsentSnapshot> LoadAsync(
        Guid tenantId, string subjectType, IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new SegmentConsentSnapshot(
            Consents.Where(c => subjectIds.Contains(c.SubjectId)).ToList(),
            Preferences.Where(p => subjectIds.Contains(p.SubjectId)).ToList()));
    }
}

internal sealed class FakeTerritoryCoverageReader : ISegmentTerritoryCoverageReader
{
    public bool CoverageAvailable { get; set; } = true;
    public List<SegmentCoverageProjection> Coverage { get; } = new();
    public int Calls { get; private set; }

    public Task<SegmentCoverageLoad> LoadAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(CoverageAvailable
            ? new SegmentCoverageLoad(true, Coverage.Where(c => accountIds.Contains(c.AccountId)).ToList())
            : SegmentCoverageLoad.Unavailable);
    }
}

internal sealed class FakeConceptAffinityReader : ISegmentConceptAffinityReader
{
    public bool ProductNodeFound { get; set; } = true;
    public HashSet<string> Specialties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int Calls { get; private set; }

    public Task<SegmentConceptAffinityResult> ResolveSpecialtiesAsync(
        Guid tenantId, string globalProductId, int maxDepth, Guid? conceptSubjectId,
        DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(ProductNodeFound
            ? new SegmentConceptAffinityResult(true, Specialties.ToList())
            : SegmentConceptAffinityResult.NoProductNode);
    }
}

/// <summary>Cross-service value proof. Also counts calls, so the "no cache" rule (the same id twice means two calls)
/// is testable.</summary>
internal sealed class FakeProductReferenceValidator : ISegmentProductReferenceValidator
{
    public ISegmentProductReferenceValidator.Outcome Result { get; set; } =
        ISegmentProductReferenceValidator.Outcome.Valid;

    public int Calls { get; private set; }
    public List<string> Kinds { get; } = new();

    public Task<ISegmentProductReferenceValidator.Outcome> ValidateAsync(
        string referenceKind, Guid referenceId, CancellationToken cancellationToken)
    {
        Calls++;
        Kinds.Add(referenceKind);
        return Task.FromResult(Result);
    }
}

/// <summary>Concept-graph repositories with call counters, used to prove that a resolution performs ONE node read and
/// ONE relationship read, and that nothing is ever inserted or updated into the graph.</summary>
internal sealed class FakeConceptNodeRepository : IConceptNodeRepository
{
    public List<ConceptNode> Rows { get; } = new();
    public int ListCalls { get; private set; }
    public int WriteCalls { get; private set; }

    public Task<ConceptNode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(n => n.TenantId == tenantId && n.Id == id));

    public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<ConceptNode>>(Rows.Where(n => n.TenantId == tenantId).ToList());
    }

    public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<ConceptNode>>(
            Rows.Where(n => n.TenantId == tenantId && n.SubjectId == subjectId).ToList());
    }

    public Task<ConceptNode?> GetActiveByCodeAsync(
        Guid tenantId, Guid subjectId, Guid conceptTypeId, string conceptNodeCode,
        CancellationToken cancellationToken)
        => Task.FromResult<ConceptNode?>(null);

    public Task InsertAsync(ConceptNode entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        Rows.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ConceptNode entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeConceptRelationshipRepository : IConceptRelationshipRepository
{
    public List<ConceptRelationship> Rows { get; } = new();
    public int ListCalls { get; private set; }
    public int WriteCalls { get; private set; }

    public Task<ConceptRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Rows.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

    public Task<IReadOnlyList<ConceptRelationship>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<ConceptRelationship>>(
            Rows.Where(r => r.TenantId == tenantId).ToList());
    }

    public Task<IReadOnlyList<ConceptRelationship>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<ConceptRelationship>>(
            Rows.Where(r => r.TenantId == tenantId && r.SubjectId == subjectId).ToList());
    }

    public Task InsertAsync(ConceptRelationship entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        Rows.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ConceptRelationship entity, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }
}
