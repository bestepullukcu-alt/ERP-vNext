using Diten.PpmService.Application.Features.ExternalContextReferences;
using Diten.PpmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(ExternalContextProviderSecurityFilter))]
[Route("internal/v1/ppm/external-context-references")]
public sealed class InternalExternalContextReferencesController(ISender sender) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        ValidateExternalContextReferenceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ValidateExternalContextReferenceQuery(
            request.ContractName,
            request.ContractVersion,
            request.ContextKind,
            request.ContextId,
            request.AdditionalProperties is { Count: > 0 }), cancellationToken);

        if (response.IsSuccessful && response.Data is not null)
        {
            return Ok(response.Data);
        }

        return StatusCode(response.StatusCode, new { errors = response.Errors });
    }
}
