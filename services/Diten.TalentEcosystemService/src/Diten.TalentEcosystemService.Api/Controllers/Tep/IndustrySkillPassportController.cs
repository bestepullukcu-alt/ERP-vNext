using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/industry-skill-passport")]
[Authorize]
public sealed class IndustrySkillPassportController : CustomBaseController
{
    private readonly IMediator _mediator;

    public IndustrySkillPassportController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(IndustrySkillPassportGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustrySkillPassportReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(IndustrySkillPassportGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustrySkillPassportReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(IndustrySkillPassportGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] IndustrySkillPassportReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateIndustrySkillPassportReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(IndustrySkillPassportGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateIndustrySkillPassportReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(IndustrySkillPassportGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteIndustrySkillPassportReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(IndustrySkillPassportGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustrySkillPassportAuditMetadataQuery(id), ct));
}
