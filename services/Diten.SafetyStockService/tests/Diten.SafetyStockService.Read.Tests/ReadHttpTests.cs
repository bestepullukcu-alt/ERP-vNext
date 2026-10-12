using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.SafetyStockService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.SafetyStockService.Read.Tests;

public sealed class ReadHttpTests
{
    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private const string Route = "/api/v1/safety-stock/policies";

    static ReadHttpTests() => Environment.SetEnvironmentVariable("JwtSettings__Secret", Secret);

    [Fact]
    public async Task DirectHttp_RequiresAuthenticationAndSeparatePermissions()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);

        using var wrongPermission = Request(Route, Token("safety-stock.evaluations.read"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(wrongPermission)).StatusCode);

        using var evaluation = Request($"{Route}/{Guid.NewGuid():D}/evaluations/{Guid.NewGuid():D}", Token("safety-stock.evaluations.read"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(evaluation)).StatusCode);
    }

    [Fact]
    public async Task DirectHttp_AllFourRoutesFailClosedWithoutPolicyStore()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var policy = Guid.NewGuid();
        foreach (var path in new[]
        {
            Route,
            $"{Route}/{policy:D}",
            $"{Route}/{policy:D}/revisions/{Guid.NewGuid():D}"
        })
        {
            using var request = Request(path, Token("safety-stock.policies.read"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(request)).StatusCode);
        }

        using var missingCompany = new HttpRequestMessage(HttpMethod.Get, Route);
        missingCompany.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("safety-stock.policies.read"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(missingCompany)).StatusCode);
    }

    private static WebApplicationFactory<Program> Factory()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILegalEntityReferenceValidator>();
                services.AddScoped<ILegalEntityReferenceValidator, ValidReference>();
            });
        });

    private static HttpRequestMessage Request(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Legal-Entity-Id", Guid.NewGuid().ToString("D"));
        return request;
    }

    private static string Token(string permission)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "Diten.AuthService",
            audience: "DitenERP",
            claims:
            [
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("permission", permission)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        Assert.Equal(3, token.Split('.').Length);
        Assert.True(new JwtSecurityTokenHandler().CanReadToken(token));
        return token;
    }

    private sealed class ValidReference : ILegalEntityReferenceValidator
    {
        public Task<ReferenceCheck> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken)
            => Task.FromResult(ReferenceCheck.Valid);
    }
}
