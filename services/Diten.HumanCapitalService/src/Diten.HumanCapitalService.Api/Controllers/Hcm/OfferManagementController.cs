using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.OfferManagement;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Commands;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/offer-management")]
[Authorize]
public sealed class OfferManagementController : CustomBaseController
{
    private readonly IMediator _mediator;

    public OfferManagementController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(OfferManagementGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOfferReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(OfferManagementGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOfferReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(OfferManagementGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] OfferReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateOfferReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(OfferManagementGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateOfferReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(OfferManagementGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteOfferReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(OfferManagementGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOfferAuditMetadataQuery(id), ct));
}
