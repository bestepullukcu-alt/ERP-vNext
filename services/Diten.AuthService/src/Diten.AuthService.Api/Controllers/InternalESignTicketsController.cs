using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ESignReauth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

/// <summary>
/// MOD-0022 E1 — internal S2S consume of an e-signature re-authentication ticket (ESIGN-BUNDLE §4.3). Same guard as
/// every other <c>/internal/*</c> route (<c>X-Internal-Api-Key</c>, <see cref="IInternalEventAuthService"/>); the
/// /internal surface is not routed by the Gateway. The ticket is bound to user + tenant + intent and consumed once,
/// atomically. Carries no user authority of its own: it only answers "was this password re-entered, and when".
/// </summary>
[Route("internal/esign-tickets")]
public sealed class InternalESignTicketsController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string DefaultConsumer = "platform";

    private readonly IMediator _mediator;
    private readonly IInternalEventAuthService _internalAuth;

    public InternalESignTicketsController(IMediator mediator, IInternalEventAuthService internalAuth)
    {
        _mediator = mediator;
        _internalAuth = internalAuth;
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeESignTicketRequest request, CancellationToken ct)
    {
        if (!_internalAuth.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        var consumer = string.IsNullOrWhiteSpace(request.ConsumedBy) ? DefaultConsumer : request.ConsumedBy.Trim();
        var command = new ConsumeESignTicketCommand(request.Ticket, request.TenantId, request.UserId, request.Intent, consumer);
        return CreateActionResultInstance(await _mediator.Send(command, ct));
    }
}
