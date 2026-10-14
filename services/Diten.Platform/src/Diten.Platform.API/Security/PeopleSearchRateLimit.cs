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
    public const int PermitsPerMinute = 60;   // ATT-FIX1 (CT decision): 30 was tight for setting up an 8-person meeting

    /// <summary>The code a refusal by any OTHER policy carries — never the people-search one.</summary>
    public const string GenericRateLimited = "RATE_LIMITED";

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
            // OnRejected is ONE hook for every policy this limiter will ever hold, so the code is chosen from the
            // policy that refused (BL-512 FIX1): a second policy added later must not answer "too many searches".
            options.OnRejected = async (rejected, ct) =>
            {
                rejected.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                var refusing = rejected.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                var (message, code) = refusing == PolicyName
                    ? ("Too many searches; wait a moment and try again.", DecisionMakerLookup.ReasonCodes.RateLimited)
                    : ("Too many requests; wait a moment and try again.", GenericRateLimited);
                await rejected.HttpContext.Response.WriteAsJsonAsync(
                    Response<NoContent>.Fail(message, StatusCodes.Status429TooManyRequests, code), ct);
            };
        });

    /// <summary>The signed-in user (token subject); an unauthenticated caller never reaches the endpoint, but would share one bucket.</summary>
    internal static string PartitionKey(HttpContext context) =>
        context.User.FindFirst("sub")?.Value
        ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? "anonymous";
}
