using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Application.Features.Claims;
using ReturnDomain = Diten.SupplyChainService.Domain.Features.Returns;
using ClaimDomain = Diten.SupplyChainService.Domain.Features.Claims;
using ReturnInfra = Diten.SupplyChainService.Infrastructure.Features.Returns;
using ClaimInfra = Diten.SupplyChainService.Infrastructure.Features.Claims;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// The HTTP host for MOD-0186 Returns and MOD-0187 Claims. Gate 18.0 row 13 asks for SERVICE LOGS carrying
/// operation, result, correlation and replay, and that is an HTTP-level observation: the outcome lines live in
/// ReturnsController.cs:26 and ClaimsController.cs:28, which only a request reaches. Both modules' suites drive
/// their repositories directly — <c>ReturnReplayTests</c> and <c>ClaimReplayTests</c> never send a request — and
/// before this host the project had no HTTP seam for either (WebApplicationFactory appeared only for Loads,
/// Carriers, Shipments and the Sandop/Capacity host). So row 13 for these two was measurable only in a live
/// stack session, once, by hand. It is measurable here repeatably.
///
/// Unlike <see cref="SandopCapacityHttpHost"/> this host needs no composition tricks. Program.cs:76-79 already
/// registers AddReturnPersistence and AddClaimPersistence with both controllers, so the production host serves
/// these modules and nothing here changes what it composes. Two seams are replaced, and only because they
/// reach outside the process:
///   * the two reference readers, registered in production as typed HttpClients (Program.cs:77, :79), which
///     would otherwise call a real shipment service;
///   * MDM legal-entity validation, through the same StubLegalEntityValidation() the other hosts use.
///
/// The stubs are not frozen constants. A create's correlation id IS the correlation root the repository checks
/// a snapshot against — <c>CreateReturnHandler</c> passes <c>context.CorrelationId</c> as the root — so a stub
/// returning a fixed Guid would answer CORRELATION_ROOT_MISMATCH on every create. Each stub therefore reads
/// the request's own context and echoes its correlation id back as the root, which is what a real reader
/// observing the same shipment would return.
/// </summary>
public sealed class ReturnsClaimsHttpHost : WebApplicationFactory<Program>
{
    private const string Secret = "returns-claims-http-host-signing-key-0123456789abcdef";
    private const string Issuer = "returns-claims-tests";

    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid LegalEntity { get; } = Guid.NewGuid();
    public Guid Actor { get; } = Guid.NewGuid();

    /// <summary>The shipment every fixture body refers to; the stubs report it Delivered.</summary>
    public Guid Shipment { get; } = Guid.NewGuid();

    private readonly Dictionary<string, string?> _settings;

    public ReturnsClaimsHttpHost()
    {
        // No default: 27017-27021 is the operational band, so the suite fails closed without a lane URI.
        var mongo = Environment.GetEnvironmentVariable("RETURNS_MONGO_URI")
                    ?? Environment.GetEnvironmentVariable("CLAIMS_TEST_MONGO")
                    ?? throw new InvalidOperationException(
                        "Set RETURNS_MONGO_URI (or CLAIMS_TEST_MONGO) to the lane's loopback replica set; "
                        + "scripts/test-env/mvp6-test-mongo-env.sh prints it. There is no fallback port.");
        _settings = new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = mongo,
            ["Mongo:DatabaseName"] = "DitenSupplyChain_Mod0186_0187_Http_Test",
            ["JwtSettings:Secret"] = Secret,
            ["JwtSettings:Issuer"] = Issuer,
            ["JwtSettings:Audience"] = Issuer
        };
        // Each instance invents its own tenant and legal entity, so runs do not collide and the database need
        // not be dropped between them. Nothing outside this host's own database is touched.
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(_settings));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(_settings));
        builder.ConfigureServices(s =>
        {
            s.StubLegalEntityValidation();
            // Replace, not add: the production registration is a typed HttpClient and would win otherwise.
            s.RemoveAll<IReturnReferenceReader>();
            s.RemoveAll<IClaimReferenceReader>();
            s.AddScoped<IReturnReferenceReader>(p => new StubReturnReferenceReader(
                p.GetRequiredService<ReturnRequestContext>()));
            s.AddScoped<IClaimReferenceReader>(p => new StubClaimReferenceReader(
                p.GetRequiredService<ClaimRequestContext>()));
        });
    }

    /// <summary>The service's own database, so a test reads back what the request wrote.</summary>
    public MongoDB.Driver.IMongoDatabase Database => Services.GetRequiredService<MongoDB.Driver.IMongoDatabase>();

    private sealed class StubReturnReferenceReader(ReturnRequestContext context) : IReturnReferenceReader
    {
        public Task<ReturnDomain.ReturnReferenceSnapshot> ObserveAsync(ReturnDomain.ReturnOrder order, CancellationToken ct) =>
            Task.FromResult(new ReturnDomain.ReturnReferenceSnapshot(order.ShipmentId, "Delivered",
                context.CorrelationId, DateTimeOffset.UtcNow,
                [new ReturnDomain.ReturnSourceLine("1", "10", "EA", null, null)]));
    }

    private sealed class StubClaimReferenceReader(ClaimRequestContext context) : IClaimReferenceReader
    {
        public Task<ClaimDomain.ClaimReferenceSnapshot> ObserveAsync(ClaimDomain.Claim claim, CancellationToken ct) =>
            Task.FromResult(new ClaimDomain.ClaimReferenceSnapshot(claim.ShipmentId, "Delivered",
                claim.CarrierId, claim.CarrierId is null ? null : "Active",
                context.CorrelationId, DateTimeOffset.UtcNow));
    }

    public string Token(Guid? tenant = null, Guid? legalEntity = null, IEnumerable<string>? permissions = null, Guid? actor = null)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", (tenant ?? Tenant).ToString()),
            new("legal_entity_id", (legalEntity ?? LegalEntity).ToString()),
            new("sub", (actor ?? Actor).ToString())
        };
        claims.AddRange((permissions ?? AllPermissions).Select(p => new Claim("permission", p)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Issuer, claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));
    }

    /// <summary>
    /// Returns gates each transition on a permission of its own, not on the generic transition permission:
    /// ReturnPermissions.ForTarget maps Authorized/Rejected to …authorize, InTransit to …transit, and so on.
    /// Measured by omission — a token holding only …transition answered 403 on a transition to Authorized —
    /// so the per-target keys are enumerated from ForTarget rather than assumed.
    /// </summary>
    private static readonly string[] ReturnTargets =
        ["Authorized", "Rejected", "InTransit", "Cancelled", "Received", "Dispositioned", "Closed"];

    public static IReadOnlyList<string> AllPermissions =>
    [
        ReturnInfra.ReturnPermissions.Read, ReturnInfra.ReturnPermissions.Create,
        ReturnInfra.ReturnPermissions.Transition,
        .. ReturnTargets.Select(ReturnInfra.ReturnPermissions.ForTarget).OfType<string>().Distinct(),
        ClaimInfra.ClaimPermissions.Read, ClaimInfra.ClaimPermissions.Create,
        ClaimInfra.ClaimPermissions.Investigate, ClaimInfra.ClaimPermissions.Decide,
        ClaimInfra.ClaimPermissions.Settle
    ];

    /// <summary>A return create body for <see cref="Shipment"/>, matching ReturnWire.CreateValid.</summary>
    public JsonObject ReturnBody(string quantity = "1") => new()
    {
        ["shipmentId"] = Shipment.ToString(),
        ["reasonCode"] = "damaged",
        ["lines"] = new JsonArray(new JsonObject
        {
            ["shipmentLineNumber"] = "1", ["quantity"] = quantity, ["uomId"] = "EA"
        })
    };

    /// <summary>A claim create body for <see cref="Shipment"/>, matching ClaimWire's create validation.</summary>
    public JsonObject ClaimBody(string amount = "100.00") => new()
    {
        ["shipmentId"] = Shipment.ToString(),
        ["reasonCode"] = "damaged",
        ["claimedAmount"] = amount,
        ["currency"] = "CHF"
    };

    public sealed record Reply(int Status, string? Body, string? Correlation)
    {
        public JsonNode? Json => string.IsNullOrWhiteSpace(Body) ? null : JsonNode.Parse(Body);
        // Both modules nest the code: {"error":{"code":…},"contractVersion":"v1"}.
        public string? Code => Json?["error"]?["code"]?.GetValue<string>();
        public bool Replay => Json?["idempotentReplay"]?.GetValue<bool>() ?? false;
    }

    public async Task<Reply> Send(string method, string path, JsonNode? body = null, string? token = null,
        string? idempotencyKey = "k", string? correlation = null,
        Guid? tenant = null, Guid? legalEntity = null)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", (tenant ?? Tenant).ToString());
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", (legalEntity ?? LegalEntity).ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation ?? Guid.NewGuid().ToString());
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var header = response.Headers.TryGetValues("X-Correlation-Id", out var values) ? values.FirstOrDefault() : null;
        return new Reply((int)response.StatusCode, text, header);
    }
}

[CollectionDefinition("Returns Claims Http")]
public sealed class ReturnsClaimsHttpCollection : ICollectionFixture<ReturnsClaimsHttpHost>;
