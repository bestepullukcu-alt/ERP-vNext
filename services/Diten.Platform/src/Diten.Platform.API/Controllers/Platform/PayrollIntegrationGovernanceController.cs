using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/platform/payroll-integration-governance")]
[Authorize(Policy = "PlatformActor")]
public sealed class PayrollIntegrationGovernanceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PayrollIntegrationGovernanceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("runs")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetRuns(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationRunListQuery(), ct));

    [HttpGet("runs/{id:guid}")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetRunById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationRunByIdQuery(id), ct));

    [HttpPost("runs")]
    [HasPermission("platform.payroll-integration-governance.create-run")]
    public async Task<IActionResult> CreateRun([FromBody] PayrollIntegrationRunRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePayrollIntegrationRunCommand(request), ct));

    [HttpPatch("runs/{id:guid}/status")]
    [HasPermission("platform.payroll-integration-governance.update-run-status")]
    public async Task<IActionResult> UpdateRunStatus(Guid id, [FromBody] PayrollIntegrationRunStatusRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePayrollIntegrationRunStatusCommand(id, request), ct));

    [HttpPatch("runs/{id:guid}/archive")]
    [HasPermission("platform.payroll-integration-governance.update-run-status")]
    public async Task<IActionResult> ArchiveRun(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchivePayrollIntegrationRunCommand(id), ct));

    [HttpPost("runs/{id:guid}/source-links")]
    [HasPermission("platform.payroll-integration-governance.link-source")]
    public async Task<IActionResult> RecordSourceLink(Guid id, [FromBody] PayrollIntegrationSourceLinkRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollIntegrationSourceLinkCommand(id, request), ct));

    [HttpGet("runs/{id:guid}/source-links")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetSourceLinks(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationSourceLinksQuery(id), ct));

    [HttpPut("runs/{id:guid}/mapping-controls/{controlId:guid}")]
    [HasPermission("platform.payroll-integration-governance.manage-mapping")]
    public async Task<IActionResult> UpdateMappingControl(Guid id, Guid controlId, [FromBody] PayrollIntegrationMappingControlRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePayrollIntegrationMappingControlCommand(id, controlId, request), ct));

    [HttpGet("runs/{id:guid}/mapping-controls")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetMappingControls(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationMappingControlsQuery(id), ct));

    [HttpPost("runs/{id:guid}/reconciliation-controls")]
    [HasPermission("platform.payroll-integration-governance.record-reconciliation")]
    public async Task<IActionResult> RecordReconciliationControl(Guid id, [FromBody] PayrollReconciliationControlRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollReconciliationControlCommand(id, request), ct));

    [HttpGet("runs/{id:guid}/reconciliation-controls")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetReconciliationControls(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollReconciliationControlsQuery(id), ct));

    [HttpPost("runs/{id:guid}/exceptions")]
    [HasPermission("platform.payroll-integration-governance.create-exception")]
    public async Task<IActionResult> CreateException(Guid id, [FromBody] PayrollIntegrationExceptionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePayrollIntegrationExceptionCommand(id, request), ct));

    [HttpPatch("runs/{id:guid}/exceptions/{exceptionId:guid}/resolve")]
    [HasPermission("platform.payroll-integration-governance.resolve-exception")]
    public async Task<IActionResult> ResolveException(Guid id, Guid exceptionId, [FromBody] PayrollIntegrationExceptionResolutionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ResolvePayrollIntegrationExceptionCommand(id, exceptionId, request), ct));

    [HttpGet("runs/{id:guid}/exceptions")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetExceptions(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationExceptionsQuery(id), ct));

    [HttpPost("runs/{id:guid}/replay-requests")]
    [HasPermission("platform.payroll-integration-governance.request-replay")]
    public async Task<IActionResult> RequestReplay(Guid id, [FromBody] PayrollIntegrationRetryReplayRequestModel request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RequestPayrollIntegrationReplayCommand(id, request), ct));

    [HttpPost("runs/{id:guid}/evidence-export-references")]
    [HasPermission("platform.payroll-integration-governance.record-evidence-export")]
    public async Task<IActionResult> RecordEvidenceExportReference(Guid id, [FromBody] PayrollIntegrationEvidenceExportReferenceRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollIntegrationEvidenceExportReferenceCommand(id, request), ct));

    [HttpGet("runs/{id:guid}/evidence-export-references")]
    [HasPermission("platform.payroll-integration-governance.read")]
    public async Task<IActionResult> GetEvidenceExportReferences(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationEvidenceExportsQuery(id), ct));

    [HttpPost("runs/{id:guid}/health-snapshots")]
    [HasPermission("platform.payroll-integration-governance.record-health")]
    public async Task<IActionResult> RecordHealthSnapshot(Guid id, [FromBody] PayrollIntegrationHealthSnapshotRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollIntegrationHealthSnapshotCommand(request with { RunId = id }), ct));

    [HttpGet("runs/{id:guid}/health")]
    [HasPermission("platform.payroll-integration-governance.read-health")]
    public async Task<IActionResult> GetHealth(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollIntegrationHealthQuery(id), ct));
}
