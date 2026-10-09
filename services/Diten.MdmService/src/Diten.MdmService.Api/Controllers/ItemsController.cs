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
