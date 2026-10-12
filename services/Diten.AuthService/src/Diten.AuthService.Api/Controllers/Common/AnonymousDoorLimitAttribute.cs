using Diten.AuthService.Application.Common;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
        var services = context.HttpContext.RequestServices;
        var doors = services.GetRequiredService<PasswordDoorRateLimiter>();
        var asking = services.GetRequiredService<ClientAddressResolver>().Identify(context.HttpContext);
        if (!doors.TryAcquireLoginDoor(Door, asking.Key, asking.Identified))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)doors.Window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.Result = new ObjectResult(Response<NoContent>.Fail(
                "Too many requests. Try again later.",
                [new ResponseError(AuthRefusalCodes.TooManyRequests)],
                StatusCodes.Status429TooManyRequests))
            { StatusCode = StatusCodes.Status429TooManyRequests };
            return;
        }

        await next();
    }
}

/// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX3 — the body limit of the anonymous doors (an address, a password, a link).</summary>
public static class AnonymousDoorBody
{
    public const long Limit = 8 * 1024;
}
