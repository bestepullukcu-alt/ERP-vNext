using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.AssignmentRules;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.Territory.ImportExport;
using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Application.Features.Territory.PlanVsCurrent;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Application.Features.Territory.Nodes;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0151 Territory Management — TerritoryModel + TerritoryNode backend with FU02B manual lifecycle.
/// Workflow approval, assignments, resources, evidence and import/export remain out of scope. Gateway-only.
/// </summary>
[Authorize]
[Route("api/crm/territory-models")]
public sealed class TerritoryModelsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TerritoryModelsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---- TerritoryModel ----

    [HttpGet]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryModelListQuery(search, status, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryModelByIdQuery(id), cancellationToken));

    [HttpPost]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> Create([FromBody] CreateTerritoryModelCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTerritoryModelCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { Id = id }, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ActivateTerritoryModelCommand(id, request?.Reason, request?.CorrelationId), cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new DeactivateTerritoryModelCommand(id, request?.Reason, request?.CorrelationId), cancellationToken));

    [HttpPost("{id:guid}/archive")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveTerritoryModelCommand(id, request?.Reason, request?.CorrelationId), cancellationToken));

    [HttpPost("{id:guid}/delete-draft")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> DeleteDraft(Guid id, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SoftDeleteDraftTerritoryModelCommand(id, request?.Reason, request?.CorrelationId), cancellationToken));

    // ---- TerritoryNode (model-scoped) ----

    [HttpGet("{id:guid}/nodes")]
    [HasPermission(TerritoryPermissions.NodeRead)]
    public async Task<IActionResult> Nodes(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryHierarchyQuery(id), cancellationToken));

    [HttpPost("{id:guid}/nodes")]
    [HasPermission(TerritoryPermissions.NodeManage)]
    public async Task<IActionResult> CreateNode(Guid id, [FromBody] CreateTerritoryNodeCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id }, cancellationToken));

    [HttpPut("{id:guid}/nodes/{nodeId:guid}")]
    [HasPermission(TerritoryPermissions.NodeManage)]
    public async Task<IActionResult> UpdateNode(Guid id, Guid nodeId, [FromBody] UpdateTerritoryNodeCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id, Id = nodeId }, cancellationToken));

    [HttpPost("{id:guid}/nodes/{nodeId:guid}/delete-draft")]
    [HasPermission(TerritoryPermissions.NodeManage)]
    public async Task<IActionResult> DeleteDraftNode(
        Guid id, Guid nodeId, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SoftDeleteDraftTerritoryNodeCommand(id, nodeId, request?.Reason, request?.CorrelationId), cancellationToken));

    // ---- FU03 TerritoryAssignmentRule (model-scoped) ----
    //
    // Rules describe how accounts would be matched to a node. They assign nothing: there is no apply endpoint and no
    // AccountTerritoryAssignment aggregate in this service (FU05). Reads use model.read, writes use model.manage —
    // FU03 opens no new permission, following the FU02B precedent (pack §22.1).

    [HttpGet("{id:guid}/assignment-rules")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> AssignmentRules(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryAssignmentRuleListQuery(id), cancellationToken));

    [HttpGet("{id:guid}/assignment-rules/{ruleId:guid}")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> AssignmentRuleById(Guid id, Guid ruleId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryAssignmentRuleByIdQuery(id, ruleId), cancellationToken));

    [HttpPost("{id:guid}/assignment-rules")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> CreateAssignmentRule(
        Guid id, [FromBody] CreateTerritoryAssignmentRuleCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id }, cancellationToken));

    [HttpPut("{id:guid}/assignment-rules/{ruleId:guid}")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> UpdateAssignmentRule(
        Guid id, Guid ruleId, [FromBody] UpdateTerritoryAssignmentRuleCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id, RuleId = ruleId }, cancellationToken));

    [HttpPost("{id:guid}/assignment-rules/{ruleId:guid}/delete-draft")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> DeleteAssignmentRule(
        Guid id, Guid ruleId, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SoftDeleteTerritoryAssignmentRuleCommand(id, ruleId, request?.Reason, request?.CorrelationId), cancellationToken));

    /// <summary>Runs the model's assignment rules and returns candidates + conflicts. Writes nothing.</summary>
    [HttpPost("{id:guid}/assignment-preview")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> AssignmentPreview(
        Guid id, [FromBody] TerritoryAssignmentPreviewRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PreviewTerritoryAssignmentsCommand(id, request?.RuleId, request?.MaxAccounts, request?.CorrelationId),
            cancellationToken));

    // ---- FU04 TerritoryResourceAssignment (model-scoped) ----
    //
    // "Who is responsible for this territory node / business scope?" — people, not customers. Assigning ACCOUNTS is
    // AccountTerritoryAssignment (FU05) and still has no aggregate or endpoint. Ending an assignment is a status
    // transition, never a delete.

    [HttpGet("{id:guid}/resource-assignments")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ResourceAssignments(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryResourceAssignmentListQuery(id), cancellationToken));

    [HttpGet("{id:guid}/resource-assignments/{assignmentId:guid}")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ResourceAssignmentById(Guid id, Guid assignmentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryResourceAssignmentByIdQuery(id, assignmentId), cancellationToken));

    [HttpPost("{id:guid}/resource-assignments")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> CreateResourceAssignment(
        Guid id, [FromBody] CreateTerritoryResourceAssignmentCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id }, cancellationToken));

    [HttpPut("{id:guid}/resource-assignments/{assignmentId:guid}")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> UpdateResourceAssignment(
        Guid id, Guid assignmentId, [FromBody] UpdateTerritoryResourceAssignmentCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id, AssignmentId = assignmentId }, cancellationToken));

    /// <summary>Soft-deletes a still-proposed assignment. Anything that has taken effect must be ended instead.</summary>
    [HttpPost("{id:guid}/resource-assignments/{assignmentId:guid}/delete-draft")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> DeleteResourceAssignment(
        Guid id, Guid assignmentId, [FromBody] TerritoryLifecycleRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SoftDeleteTerritoryResourceAssignmentCommand(id, assignmentId, request?.Reason, request?.CorrelationId), cancellationToken));

    /// <summary>Terminates an assignment (Status=ended + ValidTo). History is preserved — this is not a delete.</summary>
    [HttpPost("{id:guid}/resource-assignments/{assignmentId:guid}/end")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> EndResourceAssignment(
        Guid id, Guid assignmentId, [FromBody] TerritoryEndAssignmentRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new EndTerritoryResourceAssignmentCommand(id, assignmentId, request?.EndDate, request?.Reason, request?.CorrelationId), cancellationToken));

    [HttpPost("{id:guid}/resource-assignments/{assignmentId:guid}/replace")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> ReplaceResourceAssignment(
        Guid id, Guid assignmentId, [FromBody] ReplaceTerritoryResourceAssignmentCommand command,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            command with { ModelId = id, AssignmentId = assignmentId }, cancellationToken));

    [HttpPost("{id:guid}/resource-assignments/{assignmentId:guid}/transfer")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> TransferResourceAssignment(
        Guid id, Guid assignmentId, [FromBody] TransferTerritoryResourceAssignmentCommand command,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            command with { ModelId = id, AssignmentId = assignmentId }, cancellationToken));

    [HttpGet("{id:guid}/resource-responsibilities/current")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> CurrentResourceResponsibilities(
        Guid id, [FromQuery] Guid? territoryId, [FromQuery] string? businessUnitScope,
        [FromQuery] string? positionCode, [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] bool? primaryOnly, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCurrentTerritoryResourceResponsibilitiesQuery(
                id, territoryId, businessUnitScope, positionCode, effectiveAt, primaryOnly ?? true), cancellationToken));

    [HttpGet("{id:guid}/resource-assignments/history")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ResourceAssignmentHistory(
        Guid id, [FromQuery] Guid? territoryId, [FromQuery] string? resourceId,
        [FromQuery] string? positionCode, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetTerritoryResourceAssignmentHistoryQuery(id, territoryId, resourceId, positionCode), cancellationToken));

    // ---- FU04B plan-vs-current visibility (READ-ONLY; no mutation surface) ----

    /// <summary>Immutable activation plan baseline. A model without a baseline returns a STATE, never a 404.</summary>
    [HttpGet("{id:guid}/resource-assignment-plan-snapshot")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ResourceAssignmentPlanSnapshot(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetTerritoryResourceAssignmentPlanSnapshotQuery(id), cancellationToken));

    /// <summary>Read-time diff between the activation baseline and the current responsibilities. Writes nothing.</summary>
    [HttpGet("{id:guid}/resource-assignment-plan-vs-current")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ResourceAssignmentPlanVsCurrent(
        Guid id, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] Guid? territoryNodeId,
        [FromQuery] string? businessUnit, [FromQuery] string? positionCode, [FromQuery] string? resourceId,
        [FromQuery] string? diffType, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetTerritoryPlanVsCurrentQuery(id, effectiveAt, territoryNodeId, businessUnit, positionCode, resourceId, diffType),
            cancellationToken));

    /// <summary>Read-only exclusivity report over the model's resource assignments. Writes nothing.</summary>
    [HttpPost("{id:guid}/resource-assignments/validate-conflicts")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ValidateResourceConflicts(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ValidateTerritoryResourceConflictsCommand(id), cancellationToken));

    // ---- FU05 AccountTerritoryAssignment (model-scoped) ----

    [HttpGet("{id:guid}/account-assignments")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> AccountAssignments(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountTerritoryAssignmentListQuery(id), cancellationToken));

    [HttpGet("{id:guid}/account-assignments/{assignmentId:guid}")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> AccountAssignmentById(Guid id, Guid assignmentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountTerritoryAssignmentByIdQuery(id, assignmentId), cancellationToken));

    [HttpPost("{id:guid}/assignment-preview/apply")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> ApplyAccountAssignments(
        Guid id, [FromBody] ApplyAccountTerritoryAssignmentsCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { ModelId = id }, cancellationToken));

    [HttpPost("{id:guid}/account-assignments/{assignmentId:guid}/end")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    public async Task<IActionResult> EndAccountAssignment(
        Guid id, Guid assignmentId, [FromBody] TerritoryEndAssignmentRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new EndAccountTerritoryAssignmentCommand(id, assignmentId, request?.EndDate, request?.Reason, request?.CorrelationId),
            cancellationToken));

    // ---- FU08 import / export ----
    //
    // These sit under the EXISTING `/api/crm/territory-models/{everything}` Gateway wildcard, so FU08 needs no
    // ocelot.json change (the same "no new Gateway route" choice MOD-0150 made). Permissions use the pack §22.5
    // fallback: model.read for export/template, model.manage for dry-run/apply, until the FU08-RBAC catalog
    // alignment adds crm.territory.export / crm.territory.import.

    /// <summary>Read-only XLSX export of one model (metadata, nodes, hierarchy, BU scopes, rules, account assignments
    /// current+history, CoverageSummary, resource assignments current+history, Plan vs Current). No TenantId column.</summary>
    [HttpGet("{id:guid}/export")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ExportWorkbook(Guid id, CancellationToken cancellationToken)
        => FileResultFrom(await _mediator.Send(new ExportTerritoryModelWorkbookQuery(id), cancellationToken));

    /// <summary>Fillable multi-sheet import template for one model.</summary>
    [HttpGet("{id:guid}/import-template")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ImportTemplate(Guid id, CancellationToken cancellationToken)
        => FileResultFrom(await _mediator.Send(new BuildTerritoryImportTemplateQuery(id), cancellationToken));

    /// <summary>
    /// Uploads a workbook. <c>dryRun</c> defaults to <b>true</b>, so a call that forgets the flag can only ever
    /// preview — writing requires the separate apply route below.
    /// </summary>
    [HttpPost("{id:guid}/import-file")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    [RequestSizeLimit(MaxImportBytes)]
    public Task<IActionResult> ImportWorkbook(
        Guid id, IFormFile? file, [FromQuery] bool dryRun = true, [FromQuery] bool strictMode = false,
        [FromQuery] string? correlationId = null, CancellationToken cancellationToken = default)
        => HandleImportAsync(id, file, dryRun, strictMode, correlationId, cancellationToken);

    /// <summary>Applies a previously previewed workbook. A separate route so the destructive call can never be
    /// reached by a stray preview request.</summary>
    [HttpPost("{id:guid}/import-file/apply")]
    [HasPermission(TerritoryPermissions.ModelManage)]
    [RequestSizeLimit(MaxImportBytes)]
    public Task<IActionResult> ApplyWorkbook(
        Guid id, IFormFile? file, [FromQuery] bool strictMode = false, [FromQuery] string? correlationId = null,
        CancellationToken cancellationToken = default)
        => HandleImportAsync(id, file, dryRun: false, strictMode, correlationId, cancellationToken);

    /// <summary>Append-only import run history of one model.</summary>
    [HttpGet("{id:guid}/import-runs")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> ImportRuns(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryImportRunsQuery(id), cancellationToken));

    private const long MaxImportBytes = 10 * 1024 * 1024;

    private async Task<IActionResult> HandleImportAsync(
        Guid id, IFormFile? file, bool dryRun, bool strictMode, string? correlationId, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return CreateActionResultInstance(
                Response<TerritoryImportPreviewDto>.Fail("Select an .xlsx file to import.", 400));
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return CreateActionResultInstance(Response<TerritoryImportPreviewDto>.Fail(
                "Only .xlsx files produced by the import template or the export are supported.", 400));
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var actor = User.Identity?.Name
                    ?? User.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                    ?? "authenticated-user";

        var command = new TerritoryImportFileCommand(
            id, buffer.ToArray(), Path.GetFileName(file.FileName), dryRun, strictMode, correlationId, actor);

        return CreateActionResultInstance(await _mediator.Send(command, cancellationToken));
    }

    private IActionResult FileResultFrom(Response<ExportFileDto> response)
        => !response.IsSuccessful || response.Data is null
            ? CreateActionResultInstance(response)
            : File(response.Data.Content, response.Data.ContentType, response.Data.FileName);

    // FU02B uses explicit POST delete-draft endpoints; there is no hard DELETE. Submit/approve/reject,
    // approval-trace, evidence-pack and coverage-rollup remain absent.
}

public sealed record TerritoryEndAssignmentRequest(DateTimeOffset? EndDate, string? Reason, string? CorrelationId);

public sealed record TerritoryLifecycleRequest(string? Reason, string? CorrelationId);

public sealed record TerritoryAssignmentPreviewRequest(Guid? RuleId, int? MaxAccounts, string? CorrelationId);
