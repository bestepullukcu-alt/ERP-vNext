using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport;
using Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Commands;
using Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/candidate-career-passport")]
[Authorize]
public sealed class CandidateCareerPassportController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CandidateCareerPassportController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(CandidateCareerPassportGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateCareerPassportReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CandidateCareerPassportGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateCareerPassportReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(CandidateCareerPassportGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] CandidateCareerPassportReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateCandidateCareerPassportReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(CandidateCareerPassportGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateCandidateCareerPassportReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(CandidateCareerPassportGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteCandidateCareerPassportReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(CandidateCareerPassportGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateCareerPassportAuditMetadataQuery(id), ct));
}
