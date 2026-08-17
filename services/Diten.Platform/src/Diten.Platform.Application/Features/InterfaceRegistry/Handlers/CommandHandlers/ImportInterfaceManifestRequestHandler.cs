using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.CommandHandlers;

public sealed class ImportInterfaceManifestRequestHandler
    : IRequestHandler<ImportInterfaceManifestRequest, Response<InterfaceManifestImportResultDto>>
{
    private readonly IInterfaceRegistryRepository _interfaceRegistryRepository;
    private readonly IModuleCatalogRepository _moduleCatalogRepository;

    public ImportInterfaceManifestRequestHandler(
        IInterfaceRegistryRepository interfaceRegistryRepository,
        IModuleCatalogRepository moduleCatalogRepository)
    {
        _interfaceRegistryRepository = interfaceRegistryRepository;
        _moduleCatalogRepository = moduleCatalogRepository;
    }

    public async Task<Response<InterfaceManifestImportResultDto>> Handle(ImportInterfaceManifestRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceModuleCode = ModuleCatalogCodeNormalizer.Normalize(request.Manifest.SourceModuleCode);
        var owner = await _moduleCatalogRepository.GetByCodeAsync(sourceModuleCode, ct);
        if (owner is null)
        {
            return Response<InterfaceManifestImportResultDto>.Fail("ownerModuleCode was not found in Module Catalog.", 400);
        }

        var snapshots = request.Manifest.Interfaces
            .Select(InterfaceRegistryMapper.ToSnapshot)
            .ToArray();
        var manifestHash = InterfaceManifestHasher.HashManifest(snapshots);
        var existingBatch = await _interfaceRegistryRepository.GetBatchByManifestHashAsync(
            request.Manifest.SourceService.Trim(),
            sourceModuleCode,
            manifestHash,
            ct);

        if (existingBatch is not null)
        {
            var existingDiffItems = await _interfaceRegistryRepository.GetDiffItemsAsync(existingBatch.BatchId, ct);
            return Response<InterfaceManifestImportResultDto>.Success(ToResult(existingBatch, existingDiffItems));
        }

        foreach (var snapshot in snapshots)
        {
            if (!string.Equals(snapshot.OwnerModuleCode, sourceModuleCode, StringComparison.Ordinal))
            {
                return Response<InterfaceManifestImportResultDto>.Fail("Interface ownerModuleCode must match manifest sourceModuleCode.", 400);
            }

            if (await _interfaceRegistryRepository.ExistsDefinitionVersionAsync(snapshot.InterfaceCode, snapshot.InterfaceVersion, ct))
            {
                return Response<InterfaceManifestImportResultDto>.Fail("InterfaceCode + Version already exists.", 409);
            }
        }

        var activeSnapshots = await _interfaceRegistryRepository.GetActiveSnapshotsAsync(ct);
        var batch = new InterfaceDiscoveryBatch
        {
            SourceService = request.Manifest.SourceService.Trim(),
            SourceModuleCode = sourceModuleCode,
            ManifestHash = manifestHash,
            ImportedAtUtc = DateTimeOffset.UtcNow,
            Status = InterfaceRegistryStatuses.PendingReview
        };
        var diffItems = InterfaceDiffBuilder.Build(batch.BatchId, snapshots, activeSnapshots);

        batch.NewCount = diffItems.Count(x => x.ChangeType == Diten.BuildingBlocks.InterfaceRegistry.Abstractions.InterfaceChangeType.New);
        batch.ChangedCount = diffItems.Count(x => x.ChangeType == Diten.BuildingBlocks.InterfaceRegistry.Abstractions.InterfaceChangeType.Changed);
        batch.DeprecatedCount = diffItems.Count(x => x.ChangeType == Diten.BuildingBlocks.InterfaceRegistry.Abstractions.InterfaceChangeType.Deprecated);
        batch.MissingCount = diffItems.Count(x => x.ChangeType == Diten.BuildingBlocks.InterfaceRegistry.Abstractions.InterfaceChangeType.Missing);
        batch.UnchangedCount = diffItems.Count(x => x.ChangeType == Diten.BuildingBlocks.InterfaceRegistry.Abstractions.InterfaceChangeType.Unchanged);

        await _interfaceRegistryRepository.CreateBatchAsync(batch, ct);
        await _interfaceRegistryRepository.CreateDiffItemsAsync(diffItems, ct);

        return Response<InterfaceManifestImportResultDto>.Success(ToResult(batch, diffItems), 201);
    }

    private static InterfaceManifestImportResultDto ToResult(
        InterfaceDiscoveryBatch batch,
        IReadOnlyList<InterfaceDiscoveryDiffItem> diffItems) =>
        new(
            batch.BatchId,
            batch.SourceService,
            batch.SourceModuleCode,
            batch.ManifestHash,
            batch.Status,
            batch.NewCount,
            batch.ChangedCount,
            batch.DeprecatedCount,
            batch.MissingCount,
            batch.UnchangedCount,
            diffItems.Select(InterfaceRegistryMapper.ToDto).ToList());
}
