using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PayrollSources;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/payroll-sources")]
[Authorize(Policy = "PlatformActor")]
public sealed class PayrollSourcesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PayrollSourcesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("platform.payroll-sources.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollExternalSystemProfileListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("platform.payroll-sources.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollExternalSystemProfileByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("platform.payroll-sources.create")]
    public async Task<IActionResult> Create([FromBody] PayrollExternalSystemProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePayrollExternalSystemProfileCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.payroll-sources.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PayrollExternalSystemProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePayrollExternalSystemProfileCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission("platform.payroll-sources.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchivePayrollExternalSystemProfileCommand(id), ct));

    [HttpPut("{id:guid}/contract-profile")]
    [HasPermission("platform.payroll-sources.update-contract")]
    public async Task<IActionResult> UpdateContractProfile(Guid id, [FromBody] PayrollContractProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePayrollContractProfileCommand(id, request), ct));

    [HttpGet("{id:guid}/contract-profile")]
    [HasPermission("platform.payroll-sources.read")]
    public async Task<IActionResult> GetContractProfile(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollContractProfileQuery(id), ct));

    [HttpPost("{id:guid}/employee-reference-maps")]
    [HasPermission("platform.payroll-sources.map-employee-reference")]
    public async Task<IActionResult> CreateEmployeeReferenceMap(Guid id, [FromBody] PayrollEmployeeReferenceMapRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePayrollEmployeeReferenceMapCommand(id, request), ct));

    [HttpPut("{id:guid}/employee-reference-maps/{mapId:guid}")]
    [HasPermission("platform.payroll-sources.map-employee-reference")]
    public async Task<IActionResult> UpdateEmployeeReferenceMap(Guid id, Guid mapId, [FromBody] PayrollEmployeeReferenceMapRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdatePayrollEmployeeReferenceMapCommand(id, mapId, request), ct));

    [HttpGet("{id:guid}/employee-reference-maps")]
    [HasPermission("platform.payroll-sources.read")]
    public async Task<IActionResult> GetEmployeeReferenceMaps(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollEmployeeReferenceMapsQuery(id), ct));

    [HttpPost("{id:guid}/cycle-references")]
    [HasPermission("platform.payroll-sources.record-cycle")]
    public async Task<IActionResult> RecordCycleReference(Guid id, [FromBody] PayrollCycleReferenceRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollCycleReferenceCommand(id, request), ct));

    [HttpGet("{id:guid}/cycle-references")]
    [HasPermission("platform.payroll-sources.read")]
    public async Task<IActionResult> GetCycleReferences(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollCycleReferencesQuery(id), ct));

    [HttpPost("{id:guid}/result-references")]
    [HasPermission("platform.payroll-sources.record-result")]
    public async Task<IActionResult> RecordResultReference(Guid id, [FromBody] PayrollResultReferenceRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollResultReferenceCommand(id, request), ct));

    [HttpGet("{id:guid}/result-references")]
    [HasPermission("platform.payroll-sources.read")]
    public async Task<IActionResult> GetResultReferences(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollResultReferencesQuery(id), ct));

    [HttpPost("{id:guid}/health-snapshots")]
    [HasPermission("platform.payroll-sources.record-health")]
    public async Task<IActionResult> RecordHealthSnapshot(Guid id, [FromBody] PayrollSourceHealthSnapshotRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordPayrollSourceHealthSnapshotCommand(id, request), ct));

    [HttpGet("{id:guid}/health")]
    [HasPermission("platform.payroll-sources.read-health")]
    public async Task<IActionResult> GetHealth(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPayrollSourceHealthQuery(id), ct));
}
