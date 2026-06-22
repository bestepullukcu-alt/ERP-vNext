using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleRegistration;

public sealed record RegisterModuleManifestCommand(ModuleManifestDocument Manifest)
    : IRequest<Response<ModuleManifestReconcileResult>>;

/// <summary>Summary of one idempotent, best-effort reconcile pass over a pushed module manifest.</summary>
public sealed record ModuleManifestReconcileResult(
    string ModuleCode,
    string CatalogAction,   // "created" | "updated"
    int PagesUpserted,
    int ActionsUpserted,
    int PermissionsSynced,
    IReadOnlyList<string> PagesSkipped);
