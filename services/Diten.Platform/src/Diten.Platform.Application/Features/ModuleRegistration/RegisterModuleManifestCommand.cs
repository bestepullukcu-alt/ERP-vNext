using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleRegistration;

public sealed record RegisterModuleManifestCommand(ModuleManifestDocument Manifest)
    : IRequest<Response<ModuleManifestReconcileResult>>, ITransactionOwnedAuditCommand;

/// <summary>Summary of one idempotent, best-effort reconcile pass over a pushed module manifest.</summary>
public sealed record ModuleManifestReconcileResult(
    string ModuleCode,
    string CatalogAction,   // "created" | "updated"
    int PagesUpserted,
    int ActionsUpserted,
    int PermissionsSynced,
    IReadOnlyList<string> PagesSkipped,
    int PagesPruned = 0,    // MC-6 — module pages soft-deleted because the manifest no longer declares them
    int ActionsPruned = 0); // MC-6 — module page-actions soft-deleted because the manifest no longer declares them
