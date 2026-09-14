using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Commands;
using Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/professional-reputation-ledger")]
[Authorize]
public sealed class ProfessionalReputationLedgerController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ProfessionalReputationLedgerController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ProfessionalReputationLedgerGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetProfessionalReputationLedgerReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ProfessionalReputationLedgerGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetProfessionalReputationLedgerReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ProfessionalReputationLedgerGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] ProfessionalReputationLedgerReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateProfessionalReputationLedgerReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ProfessionalReputationLedgerGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateProfessionalReputationLedgerReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(ProfessionalReputationLedgerGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteProfessionalReputationLedgerReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ProfessionalReputationLedgerGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetProfessionalReputationLedgerAuditMetadataQuery(id), ct));
}
