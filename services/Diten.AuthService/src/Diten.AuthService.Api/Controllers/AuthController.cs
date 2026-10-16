using Diten.AuthService.Api.Controllers.Common;
using Diten.AuthService.Api.Models;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [AnonymousDoorLimit(Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.LoginDoor.Tenant)]
    [RequestSizeLimit(AnonymousDoorBody.Limit)] // FIX3 — an anonymous body is small
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext), request.RememberMe);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [AnonymousDoorLimit(Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.LoginDoor.Register)]
    [RequestSizeLimit(AnonymousDoorBody.Limit)] // FIX3 — an anonymous body is small
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            ResolveRequestIp(HttpContext),
            ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.AccessToken, request.RefreshToken, ResolveRequestIp(HttpContext), ResolveUserAgent(HttpContext));
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccessful)
        {
            Response.Headers.Append("X-Refresh-Error-Type", "terminal");
        }
        return CreateActionResultInstance(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var command = new LogoutCommand(request.AccessToken, request.RefreshToken, ResolveRequestIp(HttpContext));
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdString)) return CreateActionResultInstance(Response<NoContent>.Fail("Unauthorized.", 401));

        var command = new ChangePasswordCommand(userIdString, request.CurrentPassword, request.NewPassword);
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId)) 
            return CreateActionResultInstance(Response<object>.Fail("Unauthorized.", 401));

        var result = await _mediator.Send(new GetUserByIdQuery(userId), ct);
        return CreateActionResultInstance(result);
    }

    /// <summary>
    /// WP-EMAIL-LANG-01 — the signed-in TENANT user's own preferences (today: the language their mail is written in).
    /// The user is the token's <c>sub</c> and the tenant its <c>tenant_id</c>, nothing else: no id in the path or body,
    /// no administrator path. Any other actor type (a platform administrator's token) is refused, as tenant doors do.
    /// </summary>
    [HttpPut("me/preferences")]
    [Authorize]
    [RequestSizeLimit(PreferencesMaxBodyBytes)]
    public async Task<IActionResult> UpdateMyPreferences(
        [FromServices] Diten.AuthService.Infrastructure.Security.SelfServiceRateLimiter limiter,
        CancellationToken ct)
    {
        if (!string.Equals(User.FindFirst("actor_type")?.Value, TenantUserActorType, StringComparison.Ordinal))
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Only a tenant user can set their own preferences.", 403));
        }

        var subject = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var userId) || !Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var tenantId)
            || userId == Guid.Empty || tenantId == Guid.Empty)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Unauthorized.", 401));
        }

        // FIX1 — per user (and tenant): a flood of writes is refused with 429 before anything is read or written.
        if (!limiter.TryAcquirePreferences(tenantId, userId, out var retryAfter))
        {
            Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "Too many requests. Try again later.",
                [new ResponseError(Diten.AuthService.Application.Common.AuthRefusalCodes.TooManyRequests)],
                429));
        }

        // FIX3 — JSON only, as the bound body was before FIX2: anything else is 415 before a byte of it is read.
        if (!Request.HasJsonContentType())
        {
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "The body must be JSON.", [new ResponseError(PreferencesMediaTypeInvalidCode)], 415));
        }

        // FIX2 — the body is read HERE, not bound: an empty or broken body gets the same coded 400 as a wrong one, instead
        // of the framework's code-less model-binding answer.
        UpdateMyPreferencesRequest? request = null;
        var readable = false;
        try
        {
            using var document = await System.Text.Json.JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
            readable = UpdateMyPreferencesRequest.TryRead(document.RootElement, out request);
        }
        // FIX3 — InvalidOperationException: a string that is not text (a lone surrogate, "\ud800", in a value or a name).
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            readable = false;
        }
        // FIX3 — a body over PreferencesMaxBodyBytes: the server's own 413, with a code, not an unhandled exception.
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "The body is too large.", [new ResponseError(PreferencesBodyTooLargeCode)], 413));
        }

        if (!readable)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail(
                "The body must name preferredLanguage: a language, or null to clear it.",
                [new ResponseError(PreferencesBodyInvalidCode)],
                400));
        }

        var result = await _mediator.Send(
            new Diten.AuthService.Application.Features.Users.Commands.UpdateMyPreferencesCommand(
                userId, tenantId, request?.PreferredLanguage, HttpContext.TraceIdentifier),
            ct);
        return CreateActionResultInstance(result);
    }

    private const string TenantUserActorType = "tenant_user";

    /// <summary>FIX1 — <c>PUT api/auth/me/preferences</c> with a body that does not name <c>preferredLanguage</c>.</summary>
    public const string PreferencesBodyInvalidCode = "USER_PREFERENCES_BODY_INVALID";

    /// <summary>FIX3 — the same door with a body that is not JSON (415).</summary>
    public const string PreferencesMediaTypeInvalidCode = "USER_PREFERENCES_MEDIA_TYPE_INVALID";

    /// <summary>FIX3 — the same door with a body over <see cref="PreferencesMaxBodyBytes"/> (413).</summary>
    public const string PreferencesBodyTooLargeCode = "USER_PREFERENCES_BODY_TOO_LARGE";

    /// <summary>FIX3 — the largest body the door reads; a language code needs a few dozen bytes.</summary>
    internal const int PreferencesMaxBodyBytes = 4096;

    private static string ResolveRequestIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string? ResolveUserAgent(HttpContext context)
    {
        return context.Request.Headers.UserAgent.FirstOrDefault();
    }
}
