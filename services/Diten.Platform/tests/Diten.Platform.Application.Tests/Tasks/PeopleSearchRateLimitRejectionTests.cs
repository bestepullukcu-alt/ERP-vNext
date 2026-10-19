using System.Text.Json;
using System.Threading.RateLimiting;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-512 FIX1 — the limiter's refusal hook is shared by every policy it will ever hold; the people-search code is
/// said only for a people-search refusal. Driven through the PRODUCTION registration (AddPeopleSearchRateLimit).
/// </summary>
public sealed class PeopleSearchRateLimitRejectionTests
{
    [Theory]
    [InlineData(PeopleSearchRateLimit.PolicyName, DecisionMakerLookup.ReasonCodes.RateLimited)]
    [InlineData("some-other-policy", PeopleSearchRateLimit.GenericRateLimited)]
    public async Task A_refusal_carries_the_code_of_the_policy_that_refused(string policy, string expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPeopleSearchRateLimit();
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var http = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        http.RequestServices = services.BuildServiceProvider();
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new EnableRateLimitingAttribute(policy)), "test"));

        await options.OnRejected!(new OnRejectedContext { HttpContext = http, Lease = new RefusedLease() }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status429TooManyRequests, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(http.Response.Body);
        var code = json.RootElement.TryGetProperty("reasonCode", out var camel) ? camel.GetString() : json.RootElement.GetProperty("reason_code").GetString();
        Assert.Equal(expected, code);
    }

    private sealed class RefusedLease : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [];
        public override bool TryGetMetadata(string metadataName, out object? metadata) { metadata = null; return false; }
    }
}
