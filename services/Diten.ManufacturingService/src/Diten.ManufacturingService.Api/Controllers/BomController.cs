using Diten.ManufacturingService.Application.Features.Boms;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Features.Boms.Queries;
using Diten.ManufacturingService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManufacturingService.Api.Controllers;

/// <summary>
/// MOD-0193 BOM &amp; Routings API — <c>bom.openapi.yaml</c> (x-owner MOD-0193). Frozen v1.0.0: getCurrentBom,
/// getBomVersion, explodeBom. Additive v1.1.0: liste, taslak oluştur/düzenle/sil, yürürlüğe al, geçmiş. Tenant + LE
/// server-resolved (TenantResolutionMiddleware); gövdede yok. Her uç <c>[HasPermission("manufacturing.bom.*")]</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/bom")]
public sealed class BomController(IMediator mediator) : CustomBaseController
{
    [HttpGet("versions")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] Guid? itemId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        CreateActionResultInstance(await mediator.Send(new GetBomListQuery(itemId, status, page, pageSize), ct));

    [HttpPost("versions")]
    [HasPermission(BomPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateBomDraftRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new CreateBomDraftCommand(body), ct));

    /// <summary>getBomVersion (frozen).</summary>
    [HttpGet("version/{bomVersionId:guid}")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> GetVersion(Guid bomVersionId, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new GetBomVersionQuery(bomVersionId), ct));

    [HttpPut("version/{bomVersionId:guid}")]
    [HasPermission(BomPermissions.Update)]
    public async Task<IActionResult> Update(Guid bomVersionId, [FromBody] UpdateBomDraftRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new UpdateBomDraftCommand(bomVersionId, body), ct));

    [HttpPost("version/{bomVersionId:guid}/release")]
    [HasPermission(BomPermissions.Release)]
    public async Task<IActionResult> Release(Guid bomVersionId, [FromBody] ReleaseBomVersionRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new ReleaseBomVersionCommand(bomVersionId, body), ct));

    [HttpDelete("version/{bomVersionId:guid}")]
    [HasPermission(BomPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid bomVersionId, [FromQuery] int rowVersion, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new DeleteBomDraftCommand(bomVersionId, rowVersion), ct));

    [HttpGet("version/{bomVersionId:guid}/history")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> History(Guid bomVersionId, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new GetBomHistoryQuery(bomVersionId), ct));

    /// <summary>explodeBom (frozen) — POST ama salt okuma.</summary>
    [HttpPost("explode")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> Explode([FromBody] ExplodeBomRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new ExplodeBomQuery(body), ct));

    /// <summary>getCurrentBom (frozen). Sabit yollardan (versions, version/…, explode) sonra eşleşir: itemId bir uuid.</summary>
    [HttpGet("{itemId:guid}/current")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> GetCurrent(Guid itemId, [FromQuery] DateOnly? asOfDate, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new GetCurrentBomQuery(itemId, asOfDate), ct));
}
