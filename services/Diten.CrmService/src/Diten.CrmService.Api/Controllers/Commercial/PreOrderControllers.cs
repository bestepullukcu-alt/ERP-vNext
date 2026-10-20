using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.Commercial;

// WP-VW-W6 W6-BE-a — MOD-0168 §15: /api/commercial/pre-orders/** (its own boundary, K-O1; the Gateway routes it). TenantId
// is server resolved; every endpoint carries one commercial.* key (§14); the read scope (own / team / approver / read-all)
// and the per-order rules (owner, approver, rep's manager) are the handlers' (404 outside the scope).

[Authorize]
public sealed class PreOrdersController : CustomBaseController
{
    private const string Base = "api/commercial/pre-orders";
    private readonly IMediator _mediator;

    public PreOrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base)]
    [HasPermission(PreOrderPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? repUserId,
        [FromQuery] Guid? pharmacyAccountId, [FromQuery] Guid? depotAccountId, [FromQuery] Guid? skuId, [FromQuery] Guid? legalEntityId,
        [FromQuery] string? countryCode, [FromQuery] bool awaitingMe, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPreOrderListQuery(status, from, to, repUserId, pharmacyAccountId, depotAccountId, skuId,
            legalEntityId, countryCode, awaitingMe, search, page, pageSize), ct));

    [HttpPost(Base)]
    [HasPermission(PreOrderPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] PreOrderInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreatePreOrderCommand(input), ct));

    [HttpGet(Base + "/context")]
    [HasPermission(PreOrderPermissions.Create)]
    public async Task<IActionResult> Context(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPreOrderContextQuery(), ct));

    [HttpPost(Base + "/preview")]
    [HasPermission(PreOrderPermissions.Create)]
    public async Task<IActionResult> Preview([FromBody] PreOrderPreviewInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new PreviewPreOrderQuery(input), ct));

    [HttpGet(Base + "/export")]
    [HasPermission(PreOrderPermissions.Export)]
    public async Task<IActionResult> Export([FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? repUserId,
        [FromQuery] Guid? pharmacyAccountId, [FromQuery] Guid? depotAccountId, [FromQuery] Guid? skuId, [FromQuery] Guid? legalEntityId,
        [FromQuery] string? countryCode, [FromQuery] bool awaitingMe, [FromQuery] string? search, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPreOrderExportQuery(new GetPreOrderListQuery(status, from, to, repUserId, pharmacyAccountId, depotAccountId,
            skuId, legalEntityId, countryCode, awaitingMe, search, null, null)), ct);
        return response.IsSuccessful && response.Data is { } file
            ? File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName)
            : CreateActionResultInstance(response);
    }

    [HttpGet(Base + "/{id:guid}")]
    [HasPermission(PreOrderPermissions.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPreOrderByIdQuery(id), ct));

    [HttpPut(Base + "/{id:guid}")]
    [HasPermission(PreOrderPermissions.Create)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PreOrderInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdatePreOrderCommand(id, input), ct));

    [HttpPost(Base + "/{id:guid}/discard")]
    [HasPermission(PreOrderPermissions.Create)]
    public async Task<IActionResult> Discard(Guid id, [FromBody] PreOrderVersionInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DiscardPreOrderCommand(id, input ?? new PreOrderVersionInput()), ct));

    [HttpPost(Base + "/{id:guid}/signature")]
    [HasPermission(PreOrderPermissions.Create)]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> Signature(Guid id, [FromBody] PreOrderSignatureInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CapturePreOrderSignatureCommand(id, input), ct));

    [HttpPost(Base + "/{id:guid}/submit")]
    [HasPermission(PreOrderPermissions.Submit)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] PreOrderVersionInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SubmitPreOrderCommand(id, input ?? new PreOrderVersionInput()), ct));

    [HttpPost(Base + "/{id:guid}/decision")]
    [HasPermission(PreOrderPermissions.Approve)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] PreOrderDecisionInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DecidePreOrderCommand(id, input), ct));

    [HttpPost(Base + "/{id:guid}/cancel")]
    [HasPermission(PreOrderPermissions.Cancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] PreOrderCancelInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CancelPreOrderCommand(id, input), ct));

    [HttpPost(Base + "/{id:guid}/copy")]
    [HasPermission(PreOrderPermissions.Create)]
    public async Task<IActionResult> Copy(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CopyPreOrderCommand(id), ct));

    [HttpPost(Base + "/{id:guid}/retry-transmission")]
    [HasPermission(PreOrderPermissions.RetryTransmission)]
    public async Task<IActionResult> RetryTransmission(Guid id, [FromBody] PreOrderVersionInput? input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new TransmitPreOrderCommand(id, input ?? new PreOrderVersionInput()), ct));

    [HttpPost(Base + "/{id:guid}/depot-response")]
    [HasPermission(PreOrderPermissions.RecordDepotResponse)]
    public async Task<IActionResult> DepotResponse(Guid id, [FromBody] PreOrderDepotResponseInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new RecordPreOrderDepotResponseCommand(id, input), ct));

    /// <summary>Screen C: the pharmacy's last six months (the handler admits the order's approver or read-all).</summary>
    [HttpGet(Base + "/{id:guid}/pharmacy-history")]
    [HasPermission(PreOrderPermissions.Read)]
    public async Task<IActionResult> PharmacyHistory(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPharmacyOrderHistoryQuery(id), ct));
}

[Authorize]
public sealed class PreOrderSettingsController : CustomBaseController
{
    private const string Base = "api/commercial/pre-order-settings";
    private readonly IMediator _mediator;

    public PreOrderSettingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base)]
    [HasPermission(PreOrderPermissions.SettingsRead)]
    public async Task<IActionResult> Get(CancellationToken ct) => CreateActionResultInstance(await _mediator.Send(new GetPreOrderSettingsQuery(), ct));

    [HttpPut(Base)]
    [HasPermission(PreOrderPermissions.SettingsUpdate)]
    public async Task<IActionResult> Update([FromBody] PreOrderSettingsInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdatePreOrderSettingsCommand(input), ct));
}

/// <summary>The e-mail link lands here AFTER sign-in (D4: no decision without a session); the decision itself goes to
/// <c>POST pre-orders/{id}/decision</c> with the link token.</summary>
[Authorize]
public sealed class PreOrderApprovalLinksController : CustomBaseController
{
    private const string Base = "api/commercial/pre-order-approval-links";
    private readonly IMediator _mediator;

    public PreOrderApprovalLinksController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base + "/{token}")]
    [HasPermission(PreOrderPermissions.Approve)]
    public async Task<IActionResult> Resolve(string token, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ResolvePreOrderApprovalLinkQuery(token), ct));
}
