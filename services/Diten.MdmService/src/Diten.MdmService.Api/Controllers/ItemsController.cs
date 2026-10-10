using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Application.Features.ItemMaster.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

// MOD-0290-FU04 S1 (§3, §14): the item screen API. No generic `manage`, no delete (the life ends with retire). The
// keys reach Auth with the module manifest in S5 (the screen slice); until then no role holds them.
[Authorize]
[ApiController]
[Route("api/mdm/items")]
public sealed class ItemsController : CustomBaseController
{
    public const string ReadPermission = "mdm.items.read";
    public const string CreatePermission = "mdm.items.create";
    public const string UpdatePermission = "mdm.items.update";
    public const string SubmitPermission = "mdm.items.submit";
    public const string DeactivatePermission = "mdm.items.deactivate";
    public const string RetirePermission = "mdm.items.retire";
    // FU04 S2 (§14): the maker takes the activation request back. Like every item key, it reaches Auth with the module
    // manifest in S5; until then no role holds it.
    public const string WithdrawPermission = "mdm.items.withdraw";

    private readonly IMediator _mediator;

    public ItemsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ReadPermission)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? q,
        [FromQuery] string? materialType,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetItemListQuery(page, pageSize, q, materialType, status), cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetItemByIdQuery(id), cancellationToken));

    [HttpPost]
    [HasPermission(CreatePermission)]
    public async Task<IActionResult> Create([FromBody] ItemModels.CreateItemRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateItemCommand(request), cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(UpdatePermission)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] ItemModels.UpdateItemRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpdateItemCommand(id, request), cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [HasPermission(SubmitPermission)]
    public async Task<IActionResult> SubmitItem(Guid id, [FromBody] ItemModels.ItemLifecycleRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitItemCommand(id, request), cancellationToken));

    // A reactivation is an activation (§8.1): the submit permission covers it (§14).
    [HttpPost("{id:guid}/reactivation-requests")]
    [HasPermission(SubmitPermission)]
    public async Task<IActionResult> RequestReactivation(Guid id, [FromBody] ItemModels.ItemLifecycleRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new RequestItemReactivationCommand(id, request), cancellationToken));

    // FU04 S2 (§8.1): PendingActivation → Draft (Deactivated for a reactivation); only the request's maker.
    [HttpPost("{id:guid}/withdraw")]
    [HasPermission(WithdrawPermission)]
    public async Task<IActionResult> WithdrawApproval(Guid id, [FromBody] ItemModels.ItemLifecycleRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawItemApprovalCommand(id, request), cancellationToken));

    // FU04 S2 (S1 carry-over c): the operator repair of IT- codes held with no item row — the product identity family's
    // operator key (the same one the LSKU / FG admission repair uses); no screen calls it.
    [HttpPost("operator/orphan-code-repairs")]
    [HasPermission(Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.LskuIdentityLifecyclePermissions
        .RecoverLifecycleOperation)]
    public async Task<IActionResult> RepairOrphanCodes([FromQuery] int limit = 100, [FromQuery] int minimumAgeMinutes = 15,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(new RepairItemOrphanCodesCommand(limit, minimumAgeMinutes),
            cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(DeactivatePermission)]
    public async Task<IActionResult> DeactivateItem(Guid id, [FromBody] ItemModels.ItemLifecycleRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeactivateItemCommand(id, request), cancellationToken));

    [HttpPost("{id:guid}/retire")]
    [HasPermission(RetirePermission)]
    public async Task<IActionResult> RetireItem(Guid id, [FromBody] ItemModels.ItemLifecycleRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new RetireItemCommand(id, request), cancellationToken));
}
