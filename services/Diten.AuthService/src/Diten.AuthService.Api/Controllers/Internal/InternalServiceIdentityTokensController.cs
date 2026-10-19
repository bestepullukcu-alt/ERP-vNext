using System.Text.Json;
using Diten.AuthService.Api.Security;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/v1/auth/service-tokens")]
public sealed class InternalServiceIdentityTokensController : ControllerBase
{
    private const string ClientIdHeader = "X-Service-Client-Id";
    private const string ClientSecretHeader = "X-Service-Client-Secret";
    private readonly IMediator _mediator;

    public InternalServiceIdentityTokensController(IMediator mediator) => _mediator = mediator;

    [HttpPost("issue")]
    public async Task<IActionResult> Issue(CancellationToken ct)
    {
        if (!ServiceIdentityTokenTransportContract.HasNoQuery(Request.QueryString.Value)
            || !ServiceIdentityTokenTransportContract.IsSupportedContentType(Request.ContentType))
            return BadRequest(Response<ServiceIdentityTokenResponse>.Fail("Exact JSON request contract is required.", 400));

        if (Request.ContentLength is > ServiceIdentityTokenRequestParser.MaxBodyBytes)
            return StatusCode(413, Response<ServiceIdentityTokenResponse>.Fail("Request body is too large.", 413));

        var clientCodes = Request.Headers[ClientIdHeader];
        var clientSecrets = Request.Headers[ClientSecretHeader];
        if (clientCodes.Count != 1 || clientSecrets.Count != 1
            || !ServiceIdentityTokenTransportContract.IsExactValue(clientCodes[0], 128)
            || !ServiceIdentityTokenTransportContract.IsExactValue(clientSecrets[0], 512))
            return BadRequest(Response<ServiceIdentityTokenResponse>.Fail("Service client headers are required.", 400));

        var clientCode = clientCodes[0]!;
        var clientSecret = clientSecrets[0]!;

        try
        {
            var request = await ServiceIdentityTokenRequestParser.ParseAsync(Request.Body, ct);
            var response = await _mediator.Send(new IssueServiceIdentityTokenCommand(
                clientCode, clientSecret, request.TenantId, request.Audience), ct);
            ct.ThrowIfCancellationRequested();
            return StatusCode(response.StatusCode, response);
        }
        catch (JsonException)
        {
            return BadRequest(Response<ServiceIdentityTokenResponse>.Fail("Malformed service token request.", 400));
        }
        catch (ServiceIdentityRequestTooLargeException)
        {
            return StatusCode(413, Response<ServiceIdentityTokenResponse>.Fail("Request body is too large.", 413));
        }
    }

}
