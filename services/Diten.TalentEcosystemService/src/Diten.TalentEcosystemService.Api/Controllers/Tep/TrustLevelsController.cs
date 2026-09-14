using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.TrustLevels;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Commands;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-trust-levels")]
[Authorize]
public sealed class TrustLevelsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TrustLevelsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(TrustLevelPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTrustLevelPolicyListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(TrustLevelPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTrustLevelPolicyByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(TrustLevelPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] TrustLevelPolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTrustLevelPolicyCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(TrustLevelPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TrustLevelPolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateTrustLevelPolicyCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(TrustLevelPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveTrustLevelPolicyCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(TrustLevelPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateTrustLevelPolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTrustLevelPolicyCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(TrustLevelPermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTrustLevelPolicyAuditMetadataQuery(id), ct));
}
