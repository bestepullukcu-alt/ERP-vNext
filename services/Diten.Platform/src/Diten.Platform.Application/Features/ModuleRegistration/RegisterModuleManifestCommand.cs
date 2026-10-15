using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleRegistration;

public sealed record RegisterModuleManifestCommand(
    ModuleManifestDocument Manifest,
    string? TrustedProducerOwnerCode = null,
    // WP-PLATFORM-AUDIT-INTX-01 — true only from the internal service-to-service endpoint (another service pushing
    // its manifest). False = Platform's own in-process self-registration worker. The audit record tells them apart.
    bool PushedOverInternalEndpoint = false)
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
