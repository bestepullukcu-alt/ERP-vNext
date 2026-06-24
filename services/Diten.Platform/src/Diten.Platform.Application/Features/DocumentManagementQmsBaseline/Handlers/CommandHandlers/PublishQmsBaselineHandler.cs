using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class PublishQmsBaselineHandler
    : IRequestHandler<PublishQmsBaselineCommand, Response<QmsBaselinePublishResult>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly IBaselineSnapshotManifestRepository _manifestRepository;
    private readonly BaselineSnapshotHasher _hasher;
    private readonly ITenantContext _tenantContext;

    public PublishQmsBaselineHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        IBaselineSnapshotManifestRepository manifestRepository,
        BaselineSnapshotHasher hasher,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _manifestRepository = manifestRepository;
        _hasher = hasher;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsBaselinePublishResult>> Handle(PublishQmsBaselineCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            // Cross-tenant or unknown id are indistinguishable by design (NL-01 non-leakage).
            return Response<QmsBaselinePublishResult>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Draft)
        {
            return Response<QmsBaselinePublishResult>.Fail(
                "Only a DRAFT baseline can be published.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        if (request.ExpectedVersion > 0 && baseline.Version != request.ExpectedVersion)
        {
            return Response<QmsBaselinePublishResult>.Fail(
                "Stale baseline version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        if (definitions.Count == 0)
        {
            return Response<QmsBaselinePublishResult>.Fail(
                "Baseline has no definitions to publish.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        var computation = _hasher.Compute(definitions);

        var manifest = new BaselineSnapshotManifest
        {
            TenantId = tenantId,
            ManifestId = $"MAN-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            BaselineReleaseId = baseline.Id,
            ManifestVersion = "1.0",
            DefinitionIds = computation.DefinitionIds,
            DefinitionHashes = computation.DefinitionHashes,
            StructuralControlsHash = computation.StructuralControlsHash,
            SnapshotHash = computation.SnapshotHash
        };
        await _manifestRepository.CreateAsync(manifest, ct);

        var expectedVersion = baseline.Version;
        baseline.Status = BaselineReleaseStatus.Published;
        baseline.SnapshotHash = computation.SnapshotHash;
        baseline.ManifestId = manifest.Id;
        baseline.PublishedAt = DateTimeOffset.UtcNow;

        var updated = await _baselineRepository.UpdateAsync(baseline, expectedVersion, ct);
        if (!updated)
        {
            return Response<QmsBaselinePublishResult>.Fail(
                "Stale baseline version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var result = new QmsBaselinePublishResult(
            baseline.Id,
            BaselineReleaseStatus.Published.ToString().ToUpperInvariant(),
            computation.SnapshotHash,
            manifest.Id,
            manifest.ManifestVersion,
            definitions.Count);
        return Response<QmsBaselinePublishResult>.Success(result, 200, request.CorrelationId);
    }
}
