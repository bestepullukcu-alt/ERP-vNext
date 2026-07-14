using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;
using Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU05 — thin controller for generating Access Matrix policies from register access profiles. Dry-run is
/// read-only (access.view); apply mutates policies (access.manage). TenantId is always server-side (never client).
/// </summary>
[ApiController]
[Route("api/v1/document-management/access-profile-policy-templates")]
[Authorize]
public sealed class DocumentManagementAccessProfileTemplatesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementAccessProfileTemplatesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("dry-run")]
    [HasPermission(AccessProfileTemplatePermissions.View)]
    public async Task<IActionResult> DryRun([FromBody] AccessProfileTemplateGenerationRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new DryRunAccessProfileTemplatesCommand(ToApplicationRequest(request, dryRun: true), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("apply")]
    [HasPermission(AccessProfileTemplatePermissions.Manage)]
    public async Task<IActionResult> Apply([FromBody] AccessProfileTemplateGenerationRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ApplyAccessProfileTemplatesCommand(ToApplicationRequest(request, dryRun: false), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private static AccessProfileTemplateRequest ToApplicationRequest(AccessProfileTemplateGenerationRequest request, bool dryRun) =>
        new(
            request.BaselineReleaseId,
            request.Scope == AccessProfileTemplateScope.Instance ? AccessProfileTemplateScope.Instance : AccessProfileTemplateScope.Definition,
            request.IncludeProfiles,
            request.ExcludeProfiles,
            request.ApplyReadOnlyStatusFolderRules,
            dryRun);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
