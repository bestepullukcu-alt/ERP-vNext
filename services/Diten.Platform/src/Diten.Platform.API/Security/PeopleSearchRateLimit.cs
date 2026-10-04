using System.Threading.RateLimiting;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Microsoft.AspNetCore.RateLimiting;

namespace Diten.Platform.API.Security;

/// <summary>
/// BL-512 — the people search is bounded per PERSON: 30 searches a minute per signed-in user, so a search box cannot be
/// turned into a directory scraper by firing many short queries. Measured before this was written: neither the Platform
/// nor the gateway had any request limiter (no <c>AddRateLimiter</c> anywhere).
///
/// <para>ASP.NET Core's own limiter, one NAMED policy (<see cref="PolicyName"/>) applied only where an endpoint asks for it
/// (<c>[EnableRateLimiting]</c>) — there is no global limiter, so no other endpoint changes. A refusal is 429 with the
/// usual response envelope and <see cref="DecisionMakerLookup.ReasonCodes.RateLimited"/>, so the screen can say it.</para>
/// </summary>
public static class PeopleSearchRateLimit
{
    public const string PolicyName = "people-search";
    public const int PermitsPerMinute = 30;

    public static IServiceCollection AddPeopleSearchRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (rejected, ct) =>
            {
                rejected.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await rejected.HttpContext.Response.WriteAsJsonAsync(
                    Response<NoContent>.Fail("Too many searches; wait a moment and try again.", StatusCodes.Status429TooManyRequests,
                        DecisionMakerLookup.ReasonCodes.RateLimited),
                    ct);
            };
        });

    /// <summary>The signed-in user (token subject); an unauthenticated caller never reaches the endpoint, but would share one bucket.</summary>
    internal static string PartitionKey(HttpContext context) =>
        context.User.FindFirst("sub")?.Value
        ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? "anonymous";
}
