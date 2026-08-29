using System.Security.Claims;
using Diten.MdmService.Application.Features.Brand;
using Diten.MdmService.Application.Features.Brand.Commands;
using Diten.MdmService.Application.Features.Brand.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0290-FU02 — Brand master. MOD-0290 is the Source of Truth; CRM / Knowledge / Campaign / Frequency
/// consume BrandId by reference only.
///
/// There is deliberately NO DELETE action: FU01 §3 forbids hard delete, so the verb does not exist at the
/// controller, the command layer, or the gateway route. Archiving is a POST to <c>{brandId}/archive</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mdm/brands")]
public sealed class BrandsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public BrandsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetList([FromQuery] GetBrandListQuery query, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(query, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{brandId:guid}")]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetById(Guid brandId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBrandByIdQuery(brandId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Read-only brand → products relation for the Brand detail Products tab.</summary>
    [HttpGet("{brandId:guid}/products")]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetProducts(Guid brandId, [FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBrandProductsQuery(brandId, includeArchived), cancellationToken);
        return CreateActionResultInstance(response);
    }

    // TenantId is absent from BrandWriteRequest by design and is resolved from the tenant context, so a caller
    // cannot influence it even by sending one.
    [HttpPost]
    [HasPermission("mdm.brands.create")]
    public async Task<IActionResult> Create([FromBody] BrandWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new CreateBrandCommand(request, Actor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{brandId:guid}")]
    [HasPermission("mdm.brands.update")]
    public async Task<IActionResult> Update(Guid brandId, [FromBody] BrandWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UpdateBrandCommand(brandId, request, Actor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{brandId:guid}/archive")]
    [HasPermission("mdm.brands.archive")]
    public async Task<IActionResult> Archive(Guid brandId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ArchiveBrandCommand(brandId, Actor), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Audit identity only — authorization is enforced by [HasPermission], never by this value.</summary>
    private string? Actor =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.Identity?.Name;
}
