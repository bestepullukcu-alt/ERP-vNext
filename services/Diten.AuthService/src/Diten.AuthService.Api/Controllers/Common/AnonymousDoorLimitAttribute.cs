using Diten.AuthService.Application.Common;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Diten.AuthService.Api.Controllers.Common;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX3 items 2 and 5 — THE ANONYMOUS SIGN-IN AND REGISTRATION DOORS ARE COUNTED FIRST. A resource
/// filter: it runs before the body is bound or validated and before the action reads the store or verifies a password, so a
/// flood costs a counter, not a BCrypt verification (~250 ms) each. Per client when clients can be told apart, otherwise one
/// coarse ceiling for everybody (<see cref="PasswordDoorRateLimiter.TryAcquireLoginDoor"/> — never per address, which would
/// let anyone lock a real user out). The refusal is the doors' coded 429 (<c>AUTH_TOO_MANY_REQUESTS</c>), with Retry-After.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AnonymousDoorLimitAttribute(PasswordDoorRateLimiter.LoginDoor door) : Attribute, IAsyncResourceFilter
{
    public PasswordDoorRateLimiter.LoginDoor Door { get; } = door;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;
        var doors = services.GetRequiredService<PasswordDoorRateLimiter>();
        var asking = services.GetRequiredService<ClientAddressResolver>().Identify(http);
        // FIX4 item 1 — the tenant the request names (resolved by the tenant middleware from the request, nothing read).
        var tenantContext = services.GetService<Diten.AuthService.Application.Common.ITenantContext>();
        Guid? tenantId = tenantContext is { IsResolved: true } ? tenantContext.TenantId : null;

        var verdict = doors.TryAcquireLoginDoor(Door, asking.Key, asking.Identified, tenantId);
        if (!verdict.Allowed)
        {
            context.Result = TooManyRequests(http, verdict.RetryAfter);
            return;
        }

        // FIX5 addendum item 1 — NO CPU slot here: the slot is the password hasher's own (PasswordWorkGate), taken only for
        // the BCrypt call. Held here it covered the body read, the Platform call and the store too (a trickled body held
        // it ~34 s). A 429 from that gate (the coded refusal the pipeline returns) says when to try again: set as the response
        // starts (the result has been written by the time next() returns).
        http.Response.OnStarting(() =>
        {
            if (http.Response.StatusCode == StatusCodes.Status429TooManyRequests && !http.Response.Headers.ContainsKey("Retry-After"))
                http.Response.Headers.RetryAfter = "1";
            return Task.CompletedTask;
        });
        var executed = await next();

        // FIX4 item 4, FIX5 addendum item 2 — only a WRONG CREDENTIAL uses up the client's guessing count: the 401 that says
        // "invalid credentials" or "invalid verification code" — never "sign-in unavailable", "e-mail sign-in off" or
        // "deactivated" (a Platform outage must not burn an office's allowance).
        if (asking.Identified && Door != PasswordDoorRateLimiter.LoginDoor.Register && IsWrongCredential(executed.Result))
        {
            doors.RecordLoginFailure(Door, asking.Key);
        }
    }

    private static bool IsWrongCredential(IActionResult? result)
    {
        if (result is not ObjectResult { StatusCode: StatusCodes.Status401Unauthorized, Value: { } value }) return false;
        var codes = value.GetType().GetProperty(nameof(Response<NoContent>.ErrorCodes))?.GetValue(value) as IEnumerable<ResponseError>;
        return codes?.Any(code => code.Code is AuthRefusalCodes.InvalidCredentials or AuthRefusalCodes.InvalidVerificationCode) == true;
    }

    private static ObjectResult TooManyRequests(HttpContext http, TimeSpan retryAfter)
    {
        http.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new ObjectResult(Response<NoContent>.Fail(
            "Too many requests. Try again later.",
            [new ResponseError(AuthRefusalCodes.TooManyRequests)],
            StatusCodes.Status429TooManyRequests))
        { StatusCode = StatusCodes.Status429TooManyRequests };
    }
}

/// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX3 — the body limit of the anonymous doors (an address, a password, a link).</summary>
public static class AnonymousDoorBody
{
    public const long Limit = 8 * 1024;
}
