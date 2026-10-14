using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>WP-VW-W3 (BE-a) — the execution store in memory, with the database's 1:1 rule (a second insert for the same
/// plan answers false) and switches for the record-write failure tests.</summary>
internal sealed class FakeVisitExecutionRepository : IVisitExecutionRepository
{
    public List<ExecutionEntity> Items { get; } = new();
    public bool ThrowOnInsert { get; set; }
    public bool RefuseInsert { get; set; }
    public bool RefuseReplace { get; set; }

    public Task<ExecutionEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

    public Task<ExecutionEntity?> GetByPlannedVisitIdAsync(Guid tenantId, Guid plannedVisitId, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == tenantId && x.PlannedVisitId == plannedVisitId && !x.IsDeleted));

    public Task<IReadOnlyList<ExecutionEntity>> ListByPlannedVisitIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> plannedVisitIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ExecutionEntity>>(
            Items.Where(x => x.TenantId == tenantId && plannedVisitIds.Contains(x.PlannedVisitId)).ToList());

    public Task<bool> InsertAsync(ExecutionEntity entity, CancellationToken ct)
    {
        if (ThrowOnInsert)
        {
            throw new InvalidOperationException("database down");
        }

        if (RefuseInsert || Items.Any(x => x.TenantId == entity.TenantId && x.PlannedVisitId == entity.PlannedVisitId))
        {
            return Task.FromResult(false);
        }

        Items.Add(entity);
        return Task.FromResult(true);
    }

    public Task<bool> ReplaceAsync(ExecutionEntity entity, int expectedVersion, CancellationToken ct)
    {
        if (RefuseReplace)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        return Task.FromResult(true);
    }
}

internal sealed class FakeVisitEvidencePolicyRepository : IVisitEvidencePolicyRepository
{
    public VisitEvidencePolicy? Stored { get; set; }
    public int Writes { get; private set; }

    public Task<VisitEvidencePolicy?> GetAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult(Stored is not null && Stored.TenantId == tenantId ? Stored : null);

    public Task<bool> InsertAsync(VisitEvidencePolicy entity, CancellationToken ct)
    {
        Writes++;
        Stored = entity;
        return Task.FromResult(true);
    }

    public Task<bool> ReplaceAsync(VisitEvidencePolicy entity, int expectedVersion, CancellationToken ct)
    {
        Writes++;
        entity.Version = expectedVersion + 1;
        Stored = entity;
        return Task.FromResult(true);
    }
}

/// <summary>The document repository in memory: what was stored, what was compensated, and a failure switch. A read is
/// granted only to the caller the fake is told may read (the Platform's scoped keys).</summary>
internal sealed class FakeVisitEvidenceStore : IVisitEvidenceStore
{
    public List<(Guid ContentId, VisitEvidenceStoreRequest Request)> Stored { get; } = new();
    public List<Guid> Compensated { get; } = new();
    public int? FailWith { get; set; }
    public bool Readable { get; set; } = true;

    public Task<ContentArtifactStoreResult> StoreAsync(VisitEvidenceStoreRequest request, CancellationToken ct)
    {
        if (FailWith is { } status)
        {
            throw new ContentArtifactStoreException(status, "store failed");
        }

        var id = Guid.NewGuid();
        Stored.Add((id, request));
        return Task.FromResult(new ContentArtifactStoreResult(id, "sha", request.Content.LongLength, request.MediaType));
    }

    public Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken ct)
    {
        var hit = Stored.FirstOrDefault(s => s.ContentId == contentId);
        return Task.FromResult(hit.Request is null || !Readable
            ? null
            : new ContentArtifactReadResult(new MemoryStream(hit.Request.Content), hit.Request.MediaType, "x", hit.Request.Content.LongLength));
    }

    public Task CompensateAsync(Guid contentId, CancellationToken ct)
    {
        Compensated.Add(contentId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUserNames(IReadOnlyDictionary<Guid, string> names) : IUserDisplayNameResolver
{
    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            names.Where(n => userIds.Contains(n.Key)).ToDictionary(n => n.Key, n => n.Value));
}

/// <summary>The <c>visit-objection-category</c> set: published with the given values, or not published at all.</summary>
internal sealed class FakeObjectionCategories(params string[] values) : IReferenceDataValidator
{
    public bool Published { get; set; } = true;

    public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
        => Task.FromResult(new ReferenceValidationResult(
            !Published ? ReferenceValidationStatus.SetMissing
            : values.Contains(value) ? ReferenceValidationStatus.Valid
            : ReferenceValidationStatus.InvalidValue,
            setCode, value));
}
