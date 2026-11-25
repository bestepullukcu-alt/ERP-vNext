using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace Diten.ProcurementService.Api.Tests.TestInfrastructure;

/// <summary>
/// MOD-0140 DL-3 — the REAL Procurement host (<c>Program.cs</c>: its DI, the store switch, BL-501 registration, the
/// authorization pipeline, the tenant middleware) over the test replica set (<see cref="ProcurementMongoReplicaSet"/>).
/// <c>Suppliers:Model</c> is the constructor argument. Tokens are signed with a per-run key; the central-audit dispatcher
/// and module self-registration are off (no Platform). Pattern: Manufacturing <c>BomApiFactory</c>.
/// </summary>
public sealed class ProcurementApiFactory(ProcurementMongoReplicaSet mongo, string supplierModel) : WebApplicationFactory<Program>
{
    private const string Issuer = "procurement-tests";
    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private Dictionary<string, string?> Settings => new()
    {
        ["Mongo:ConnectionString"] = mongo.ConnectionString,
        ["Mongo:DatabaseName"] = ProcurementMongoReplicaSet.DatabaseName,
        ["JwtSettings:Secret"] = Secret,
        ["JwtSettings:Issuer"] = Issuer,
        ["JwtSettings:Audience"] = Issuer,
        ["Suppliers:Model"] = supplierModel,
        ["CentralAudit:DispatcherEnabled"] = "false",
        ["PlatformRegistration:InternalApiKey"] = "",
        ["PlatformRegistration:BaseUrl"] = "",
        ["TenantResolution:DevBypassEnabled"] = "false"
    };

    public IMongoDatabase Database => mongo.Client.GetDatabase(ProcurementMongoReplicaSet.DatabaseName);

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings));
    }

    /// <summary>A client acting as <paramref name="user"/> of <paramref name="company"/> in <paramref name="tenant"/> with exactly these permission claims.</summary>
    public HttpClient ClientFor(Guid tenant, Guid company, Guid user, IEnumerable<string> permissions, bool superAdmin = false)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenant.ToString()),
            new("sub", user.ToString()),
            new("name", "Procurement Tester")
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        if (superAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
        }

        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Issuer, claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        client.DefaultRequestHeaders.Add("X-Legal-Entity-Id", company.ToString());
        return client;
    }
}
