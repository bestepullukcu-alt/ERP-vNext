using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/access-governance/legal-entity-scope")]
public sealed class InternalLegalEntityScopeResolutionController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ITrustedLegalEntityScopeRequestExecutor _executor;

    public InternalLegalEntityScopeResolutionController(
        IMediator mediator,
        ITrustedLegalEntityScopeRequestExecutor executor)
    {
        _mediator = mediator;
        _executor = executor;
    }

    [HttpPost("resolve")]
    public Task<IActionResult> Resolve(CancellationToken cancellationToken) =>
        _executor.ExecuteAsync(
            HttpContext,
            cancellationToken,
            async (tenantId, subjectId, request, token) => CreateActionResultInstance(
                await _mediator.Send(
                    new ResolveTrustedLegalEntityScopeQuery(
                        tenantId,
                        subjectId,
                        request.ModuleCode,
                        request.PermissionKey),
                    token)),
            Failure);

    private IActionResult Failure(int statusCode, string code) =>
        CreateActionResultInstance(
            Response<TrustedLegalEntityScopeResolution>.Fail(
                code,
                statusCode,
                code,
                HttpContext.TraceIdentifier));
}
