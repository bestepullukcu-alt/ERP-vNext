using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-consent-visibility-policies")]
[Authorize]
public sealed class ConsentVisibilityPoliciesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ConsentVisibilityPoliciesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ConsentVisibilityPolicyPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetConsentVisibilityPolicyListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ConsentVisibilityPolicyPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetConsentVisibilityPolicyByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ConsentVisibilityPolicyPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] ConsentVisibilityPolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateConsentVisibilityPolicyCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(ConsentVisibilityPolicyPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ConsentVisibilityPolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateConsentVisibilityPolicyCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(ConsentVisibilityPolicyPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveConsentVisibilityPolicyCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ConsentVisibilityPolicyPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateConsentVisibilityPolicyRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateConsentVisibilityPolicyCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ConsentVisibilityPolicyPermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetConsentVisibilityPolicyAuditMetadataQuery(id), ct));
}
