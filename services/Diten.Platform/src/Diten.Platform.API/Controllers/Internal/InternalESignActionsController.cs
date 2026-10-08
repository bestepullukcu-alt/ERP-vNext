using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.ESignature;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ESignature;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// MOD-0022 CT-D12 — <c>POST api/internal/esign/actions/register</c> (ESIGN-BUNDLE §4.23): a consumer module registers its
/// signable actions in the global catalog. S2S with the same internal-key guard every Platform internal controller uses
/// (<c>AuthService:InternalApiKey</c>, fixed-time compare, fail closed when unset). Not routed by the Gateway.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/esign/actions")]
public sealed class InternalESignActionsController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public InternalESignActionsController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterSignableActionsRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            return CreateActionResultInstance(Response<RegisterActionsResultDto>.Fail("Unauthorized.", 401));
        }

        var actions = request.Actions?
            .Select(a => new SignableActionInput(a.ActionCode, a.Names, a.DefaultRequired, a.DefaultMeaningCode, a.DefaultVisualMode))
            .ToList();
        return CreateActionResultInstance(await _mediator.Send(new RegisterSignableActionsCommand(request.Module, actions), ct));
    }

    private bool IsInternalRequestAuthorized()
    {
        var expected = _configuration["AuthService:InternalApiKey"];
        var provided = Request.Headers[InternalApiKeyHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
