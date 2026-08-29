using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

public sealed class Mod0029Fu36RegistrationRouteCoverageTests
{
    [Theory]
    [InlineData("/api/v1/document-management/controlled-document-registrations", "POST")]
    [InlineData("/api/v1/document-management/controlled-document-registrations/00000000-0000-0000-0000-000000000001", "GET")]
    [InlineData("/api/v1/document-management/controlled-document-registrations/00000000-0000-0000-0000-000000000001/retry", "POST")]
    [InlineData("/api/v1/document-management/controlled-documents/00000000-0000-0000-0000-000000000001/master-register", "GET")]
    public void Existing_document_management_catch_all_covers_fu36_routes(string path, string method)
    {
        var config = JsonSerializer.Deserialize<FileConfiguration>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var route = config.Routes.Single(x =>
            x.UpstreamPathTemplate == "/api/v1/document-management/{everything}");

        Assert.Contains(method, route.UpstreamHttpMethod);
        Assert.StartsWith("/api/v1/document-management/", path, StringComparison.Ordinal);
        Assert.Equal("/api/v1/document-management/{everything}", route.DownstreamPathTemplate);
        Assert.All(route.DownstreamHostAndPorts, x => Assert.Equal(5057, x.Port));
    }
}
