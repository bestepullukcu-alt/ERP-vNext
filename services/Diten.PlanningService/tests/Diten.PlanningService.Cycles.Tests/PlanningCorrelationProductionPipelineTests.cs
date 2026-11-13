using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class PlanningCorrelationProductionPipelineTests
{
    [Fact]
    public async Task Publish_RejectsInvalidCorrelationBeforeAuthentication()
    {
        // WebApplicationFactory boots the real API Program, including its middleware order.
        // Program reads JWT configuration before WebApplicationFactory's app-configuration hook.
        const string secretName = "JwtSettings__Secret";
        const string mongoName = "Mongo__ConnectionString";
        var previousSecret = Environment.GetEnvironmentVariable(secretName);
        var previousMongo = Environment.GetEnvironmentVariable(mongoName);
        Environment.SetEnvironmentVariable(secretName,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        Environment.SetEnvironmentVariable(mongoName, "mongodb://127.0.0.1:31994");
        try
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var path = $"/api/v2/demand/revisions/{Guid.NewGuid():D}/publish";

            using var invalid = new HttpRequestMessage(HttpMethod.Post, path);
            invalid.Headers.TryAddWithoutValidation("X-Correlation-Id", "not-a-guid");
            using var rejected = await client.SendAsync(invalid);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

            using var valid = new HttpRequestMessage(HttpMethod.Post, path);
            valid.Headers.TryAddWithoutValidation("X-Correlation-Id", Guid.NewGuid().ToString("D"));
            using var unauthenticated = await client.SendAsync(valid);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, previousSecret);
            Environment.SetEnvironmentVariable(mongoName, previousMongo);
        }
    }
}
