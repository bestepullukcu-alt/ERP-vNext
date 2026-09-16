using Diten.ProcurementService.Application.Features.Supplier.Queries;
using Diten.ProcurementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ProcurementService.Api.Controllers;

/// <summary>
/// Supplier read surface (MOD-0140). FAZ 1 scaffold: yalnız tenant+LE filtreli list + by-id (MediatR üzerinden),
/// dikey kablonun derlenip çalıştığını kanıtlar. Create/Update/Onboarding CQRS + validators FAZ 2 slice 1'e ait.
/// </summary>
[Authorize]
[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SuppliersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetSupplierListQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("procurement.suppliers.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetSupplierByIdQuery(id), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
