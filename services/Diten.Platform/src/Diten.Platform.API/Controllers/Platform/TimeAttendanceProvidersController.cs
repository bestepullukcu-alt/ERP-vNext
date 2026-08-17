using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TimeAttendanceProviders;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/time-attendance-providers")]
[Authorize(Policy = "PlatformActor")]
public sealed class TimeAttendanceProvidersController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TimeAttendanceProvidersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("platform.time-attendance-providers.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceProviderProfileListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("platform.time-attendance-providers.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceProviderProfileByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("platform.time-attendance-providers.create")]
    public async Task<IActionResult> Create([FromBody] TimeAttendanceProviderProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTimeAttendanceProviderProfileCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.time-attendance-providers.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TimeAttendanceProviderProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateTimeAttendanceProviderProfileCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission("platform.time-attendance-providers.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveTimeAttendanceProviderProfileCommand(id), ct));

    [HttpPut("{id:guid}/contract-profile")]
    [HasPermission("platform.time-attendance-providers.update-contract")]
    public async Task<IActionResult> UpdateContractProfile(Guid id, [FromBody] TimeAttendanceContractProfileRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateTimeAttendanceContractProfileCommand(id, request), ct));

    [HttpGet("{id:guid}/contract-profile")]
    [HasPermission("platform.time-attendance-providers.read")]
    public async Task<IActionResult> GetContractProfile(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceContractProfileQuery(id), ct));

    [HttpPost("{id:guid}/employee-reference-maps")]
    [HasPermission("platform.time-attendance-providers.map-employee-reference")]
    public async Task<IActionResult> CreateEmployeeReferenceMap(Guid id, [FromBody] TimeAttendanceEmployeeReferenceMapRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTimeAttendanceEmployeeReferenceMapCommand(id, request), ct));

    [HttpPut("{id:guid}/employee-reference-maps/{mapId:guid}")]
    [HasPermission("platform.time-attendance-providers.map-employee-reference")]
    public async Task<IActionResult> UpdateEmployeeReferenceMap(Guid id, Guid mapId, [FromBody] TimeAttendanceEmployeeReferenceMapRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateTimeAttendanceEmployeeReferenceMapCommand(id, mapId, request), ct));

    [HttpGet("{id:guid}/employee-reference-maps")]
    [HasPermission("platform.time-attendance-providers.read")]
    public async Task<IActionResult> GetEmployeeReferenceMaps(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceEmployeeReferenceMapsQuery(id), ct));

    [HttpPost("{id:guid}/event-references")]
    [HasPermission("platform.time-attendance-providers.record-event")]
    public async Task<IActionResult> RecordEventReference(Guid id, [FromBody] TimeAttendanceEventReferenceRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordTimeAttendanceEventReferenceCommand(id, request), ct));

    [HttpGet("{id:guid}/event-references")]
    [HasPermission("platform.time-attendance-providers.read")]
    public async Task<IActionResult> GetEventReferences(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceEventReferencesQuery(id), ct));

    [HttpPost("{id:guid}/attendance-summary-references")]
    [HasPermission("platform.time-attendance-providers.record-summary")]
    public async Task<IActionResult> RecordAttendanceSummaryReference(Guid id, [FromBody] AttendanceSummaryReferenceRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordAttendanceSummaryReferenceCommand(id, request), ct));

    [HttpGet("{id:guid}/attendance-summary-references")]
    [HasPermission("platform.time-attendance-providers.read")]
    public async Task<IActionResult> GetAttendanceSummaryReferences(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetAttendanceSummaryReferencesQuery(id), ct));

    [HttpPost("{id:guid}/sync-checkpoints")]
    [HasPermission("platform.time-attendance-providers.record-checkpoint")]
    public async Task<IActionResult> RecordSyncCheckpoint(Guid id, [FromBody] TimeAttendanceSyncCheckpointRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordTimeAttendanceSyncCheckpointCommand(id, request), ct));

    [HttpGet("{id:guid}/sync-checkpoint")]
    [HasPermission("platform.time-attendance-providers.read-checkpoint")]
    public async Task<IActionResult> GetSyncCheckpoint(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceSyncCheckpointQuery(id), ct));

    [HttpPost("{id:guid}/health-snapshots")]
    [HasPermission("platform.time-attendance-providers.record-health")]
    public async Task<IActionResult> RecordHealthSnapshot(Guid id, [FromBody] TimeAttendanceProviderHealthSnapshotRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordTimeAttendanceProviderHealthSnapshotCommand(id, request), ct));

    [HttpGet("{id:guid}/health")]
    [HasPermission("platform.time-attendance-providers.read-health")]
    public async Task<IActionResult> GetHealth(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceProviderHealthQuery(id), ct));
}
