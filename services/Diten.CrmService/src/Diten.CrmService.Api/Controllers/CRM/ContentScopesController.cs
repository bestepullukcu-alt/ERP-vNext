using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContentComposition.ContentScopes;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.ContentScopes.ContentScopePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — reusable content-scope authoring HTTP surface. Canonical under
/// <c>/api/crm/content-composition/content-scopes</c> (the SCMM-12-API-GW ocelot route already covers
/// content-composition/*). A thin adapter over the ContentScope CQRS — no business logic here. There is <b>no delete</b>
/// endpoint: closing a scope is Archive.
/// </summary>
[Authorize]
public sealed class ContentScopesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContentScopesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/content-composition/content-scopes")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentScopesQuery(status, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/content-composition/content-scopes/{contentScopeId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid contentScopeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetContentScopeQuery(contentScopeId), cancellationToken));

    [HttpPost("api/crm/content-composition/content-scopes")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContentScopeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateContentScopeCommand(
                request.ScopeCode, request.ScopeName, request.Description, request.ProductRefs, request.MarketRefs,
                request.AudienceRefs, request.Channel, request.LanguageCode, request.PeriodFrom, request.PeriodTo,
                request.ScopeVersion, request.Status),
            cancellationToken));

    [HttpPut("api/crm/content-composition/content-scopes/{contentScopeId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(
        Guid contentScopeId, [FromBody] UpdateContentScopeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateContentScopeCommand(
                contentScopeId, request.ScopeName, request.Description, request.ProductRefs, request.MarketRefs,
                request.AudienceRefs, request.Channel, request.LanguageCode, request.PeriodFrom, request.PeriodTo,
                request.ScopeVersion, request.Status),
            cancellationToken));

    [HttpPost("api/crm/content-composition/content-scopes/{contentScopeId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(Guid contentScopeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveContentScopeCommand(contentScopeId), cancellationToken));
}
