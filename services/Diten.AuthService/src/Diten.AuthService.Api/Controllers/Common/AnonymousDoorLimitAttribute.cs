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

        // FIX4 item 1 — the CPU guard: a verification slot, or a short wait in the queue, or a 429.
        using var slot = await doors.AcquireVerificationAsync(http.RequestAborted);
        if (!slot.IsAcquired)
        {
            context.Result = TooManyRequests(http, TimeSpan.FromSeconds(1));
            return;
        }

        var executed = await next();

        // FIX4 item 4 — only a WRONG attempt uses up the client's guessing count (a 401 from the door).
        if (asking.Identified && Door != PasswordDoorRateLimiter.LoginDoor.Register
            && (executed.Result as IStatusCodeActionResult)?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            doors.RecordLoginFailure(Door, asking.Key);
        }
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
