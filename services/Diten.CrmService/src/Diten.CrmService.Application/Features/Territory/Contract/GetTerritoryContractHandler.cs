using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Contract;

public sealed class GetTerritoryContractHandler : IRequestHandler<GetTerritoryContractQuery, Response<TerritoryContractDto>>
{
    public const string ModuleId = "MOD-0151";
    public const string ModuleName = "Territory Management";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU01-territory-model-node-backend-only; FU02-territory-hierarchy-ui-viewer; " +
        "FU02A-country-business-unit-scope-selector-hardening; FU02B-lifecycle-computed-expiry-draft-soft-delete; " +
        "FU03-assignment-rules-and-preview; FU04-resource-assignments; FU05-account-assignment-apply-history; " +
        "FU05A-coverage-summary-model-lifecycle-guard; FU08-import-export-hardening; " +
        "FU09A-visit-route-readiness-read-only";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "preview remains side-effect free; FU05 apply is a separate explicit user-confirmed action",
        "FU03 evaluates geography / account-type / account-list rules; other published rule types are deferred",
        "account coverage is stored only in AccountTerritoryAssignment; Account and Contact masters are never mutated",
        "account assignment apply is active-model-only and all-or-nothing for controlled validation/conflict failures",
        "current coverage requires an active territory model; assignments of an inactive/archived/superseded model stay in history only",
        "workflow approval remains FU06; FU05 supportsWorkflowActivation is false",
        "ending a resource assignment sets status=ended + validTo; it is never deleted",
        "no workflow activation",
        "no evidence pack",
        "import is dry-run first: the upload endpoint defaults to dryRun=true and apply is a separate route",
        "import writes nothing on a dry-run — not even an import run history row",
        "Model/Nodes/AssignmentRules import is sheet-level all-or-nothing; AccountAssignments is batch-level all-or-nothing",
        "account assignment import runs the FU05 apply guards; it is not a second write path",
        "resource assignment import is export/template/dry-run only — applying it is a separate FU08A",
        "FU09A exposes readiness inputs only; it creates no route, visit plan, frequency policy, score or workflow",
        "CoverageSummary and Plan vs Current are export-only; they cannot be imported",
        "import never hard-deletes: 'delete' is refused and closing is done with 'end'",
        "manual lifecycle only; workflow approval remains out of scope",
        "computed expiry only; no background scheduler",
        "live create smoke requires published-values"
    };

    private readonly ITenantContext _tenant;
    private readonly ITerritoryReferenceValidator _referenceValidator;

    public GetTerritoryContractHandler(ITenantContext tenant, ITerritoryReferenceValidator referenceValidator)
    {
        _tenant = tenant;
        _referenceValidator = referenceValidator;
    }

    public async Task<Response<TerritoryContractDto>> Handle(GetTerritoryContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryContractDto>.Fail("Tenant context is required.", 400);
        }

        var readiness = await _referenceValidator.GetReadinessAsync(cancellationToken);
        var missing = readiness.Where(r => !r.Ready).Select(r => r.SetCode).ToList();

        // FU01 create/update needs only territory-model-status (models) and territory-level + territory-node-status
        // (nodes). Contract "isReady" reflects the FULL required matrix so operators see the whole publish backlog,
        // but the three FU01-critical sets being ready is what actually unblocks live create smoke.
        var isReady = readiness.All(r => r.Ready);

        var dto = new TerritoryContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            isReady,
            TerritoryFeatureFlags.Current,
            readiness,
            missing,
            TerritoryPermissions.All,
            CurrentLimitations);

        return Response<TerritoryContractDto>.Success(dto);
    }
}
