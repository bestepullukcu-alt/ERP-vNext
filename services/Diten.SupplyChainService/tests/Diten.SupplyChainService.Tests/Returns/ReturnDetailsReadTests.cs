using System.Text.Json;
using Xunit;
using MongoDB.Driver;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns.Handlers.QueryHandlers;
using Diten.SupplyChainService.Application.Features.Returns.Queries;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Tests.Returns;

/// <summary>
/// MOD-0186 r10 DL-4 on production code: getReturn (§35.6, RB-8) and the customer copied from the shipment at create, never
/// taken from the request (§35.6, RB-27). Real Mongo repository through ReturnTestFixture.
/// </summary>
[Collection("Returns Mongo")]
public sealed class ReturnDetailsReadTests
{
    private const string Customer = "14900000-0000-4000-8000-000000000001";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static GetReturnByIdHandler Handler(ReturnTestFixture f) => new(f.Repository, new ReturnRequestContext { Scope = f.Scope });

    // RB-8: own company → 200 with the fields of contract item 1 and no workflow field.
    [Fact]
    public async Task GetReturn_OwnCompany_AnswersTheDetailFields_AndNoWorkflowField()
    {
        var f = await ReturnTestFixture.New(); f.CustomerRef = Customer;
        var created = await f.Create("2");
        var answer = await Handler(f).Handle(new GetReturnByIdQuery(created.ReturnId), default);
        Assert.Equal(200, answer.StatusCode);
        var json = JsonSerializer.SerializeToElement(answer.Data, Web);
        Assert.Equal(["contractVersion", "createdAt", "customerRef", "lines", "reasonCode", "returnId", "rmaNumber", "shipmentId", "status"],
            json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(Customer, json.GetProperty("customerRef").GetString());
        Assert.Equal("Requested", json.GetProperty("status").GetString());
        var line = json.GetProperty("lines")[0];
        Assert.Equal(("1", "2", "EA"), (line.GetProperty("shipmentLineNumber").GetString(), line.GetProperty("quantity").GetString(), line.GetProperty("uomId").GetString()));
        Assert.Equal(JsonValueKind.Null, line.GetProperty("inventoryPosting").ValueKind);
    }

    // RB-8: another tenant, another company and an unknown id answer the same 404 RETURN_NOT_FOUND.
    [Fact]
    public async Task GetReturn_OtherTenantOtherCompanyOrUnknown_IsOne404()
    {
        var f = await ReturnTestFixture.New();
        var created = await f.Create();
        var own = f.Scope;
        foreach (var scope in new[] { own with { TenantId = Guid.NewGuid() }, own with { LegalEntityId = Guid.NewGuid() } })
        {
            var foreign = await new GetReturnByIdHandler(f.Repository, new ReturnRequestContext { Scope = scope }).Handle(new GetReturnByIdQuery(created.ReturnId), default);
            Assert.Equal((404, "RETURN_NOT_FOUND"), (foreign.StatusCode, foreign.ErrorCode));
        }
        var unknown = await Handler(f).Handle(new GetReturnByIdQuery(Guid.NewGuid()), default);
        Assert.Equal((404, "RETURN_NOT_FOUND"), (unknown.StatusCode, unknown.ErrorCode));
    }

    // RB-27: the shipment's customerRef is copied at create and returned read-only by the create answer, its replay, the list
    // and getReturn; a shipment without one gives null (the field is left out).
    [Fact]
    public async Task Create_CopiesTheShipmentsCustomer_IntoEveryAnswer()
    {
        var f = await ReturnTestFixture.New(); f.CustomerRef = Customer;
        var created = await f.Create();
        Assert.Equal(Customer, created.CustomerRef);
        Assert.Equal(Customer, ReturnResponse.From(created).CustomerRef);
        var replay = await f.Create();
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(Customer, replay.CustomerRef);
        Assert.Equal(Customer, Assert.Single(await f.Repository.QueryAsync(f.Scope, null, null, default)).CustomerRef);
        Assert.Equal(Customer, (await Handler(f).Handle(new GetReturnByIdQuery(created.ReturnId), default)).Data!.CustomerRef);

        var old = await ReturnTestFixture.New();
        var plain = await old.Create();
        Assert.Null(plain.CustomerRef);
        Assert.DoesNotContain("customerRef", JsonSerializer.Serialize(ReturnResponse.From(plain), Web), StringComparison.Ordinal);
    }

    // RB-27: CreateReturnCommand has no customerRef; a body that carries one is refused by the allow-list (400 INVALID_REQUEST).
    [Fact]
    public void Create_BodyWithCustomerRef_IsRefusedByTheAllowList()
    {
        const string good = """{"shipmentId":"11111111-1111-1111-1111-111111111111","reasonCode":"DAMAGED","lines":[{"shipmentLineNumber":"1","quantity":"1","uomId":"EA"}]}""";
        Assert.True(ReturnWire.CreateValid(JsonDocument.Parse(good).RootElement));
        var withCustomer = good.Replace("\"reasonCode\"", $"\"customerRef\":\"{Customer}\",\"reasonCode\"");
        Assert.False(ReturnWire.CreateValid(JsonDocument.Parse(withCustomer).RootElement));
    }
}

/// <summary>
/// MOD-0186 DL-4 VER F-14: getReturn and the RB-27 refusal through the real HTTP pipeline (ReturnContextMiddleware, the
/// controller, the repository), on the shared Returns/Claims host.
/// </summary>
[Collection("Returns Claims Http")]
public sealed class ReturnDetailsHttpTests(Diten.SupplyChainService.Tests.Common.ReturnsClaimsHttpHost host)
{
    private async Task<string> CreateReturn()
    {
        var created = await host.Send("POST", "/api/shipment-bundle/returns", host.ReturnBody(), host.Token(), "dl4-" + Guid.NewGuid());
        Assert.Equal(201, created.Status);
        return created.Json!["returnId"]!.GetValue<string>();
    }

    [Fact]
    public async Task GetReturn_OwnCompany_IsTheDetail_OverHttp()
    {
        var id = await CreateReturn();
        var read = await host.Send("GET", $"/api/shipment-bundle/returns/{id}", token: host.Token(), idempotencyKey: null);
        Assert.Equal(200, read.Status);
        Assert.Equal(id, read.Json!["returnId"]!.GetValue<string>());
        Assert.Equal("damaged", read.Json!["reasonCode"]!.GetValue<string>());
        Assert.NotNull(read.Json!["createdAt"]);
        Assert.Single(read.Json!["lines"]!.AsArray());
    }

    [Fact]
    public async Task GetReturn_OtherTenantOrCompanyOrUnknown_Is404_MalformedIs400_NoReadKeyIs403()
    {
        var id = await CreateReturn();
        Assert.Equal((404, "RETURN_NOT_FOUND"), Pair(await host.Send("GET", $"/api/shipment-bundle/returns/{id}", token: host.Token(), idempotencyKey: null, legalEntity: Guid.NewGuid())));
        var foreign = Guid.NewGuid();
        Assert.Equal((404, "RETURN_NOT_FOUND"), Pair(await host.Send("GET", $"/api/shipment-bundle/returns/{id}", token: host.Token(tenant: foreign), idempotencyKey: null, tenant: foreign)));
        Assert.Equal((404, "RETURN_NOT_FOUND"), Pair(await host.Send("GET", $"/api/shipment-bundle/returns/{Guid.NewGuid()}", token: host.Token(), idempotencyKey: null)));
        Assert.Equal(400, (await host.Send("GET", "/api/shipment-bundle/returns/not-a-guid", token: host.Token(), idempotencyKey: null)).Status);
        var noRead = host.Token(permissions: Diten.SupplyChainService.Tests.Common.ReturnsClaimsHttpHost.AllPermissions.Where(p => p != "supplychain.returns.read"));
        Assert.Equal(403, (await host.Send("GET", $"/api/shipment-bundle/returns/{id}", token: noRead, idempotencyKey: null)).Status);
    }

    // RB-27: a create body that carries customerRef is refused with 400 INVALID_REQUEST and writes nothing.
    [Fact]
    public async Task Create_WithCustomerRef_Is400_AndWritesNothing()
    {
        var returns = host.Database.GetCollection<MongoDB.Bson.BsonDocument>("returns");
        var tenant = new MongoDB.Bson.BsonDocument("TenantId", host.Tenant.ToString());
        var before = await returns.CountDocumentsAsync(tenant);
        var body = host.ReturnBody(); body["customerRef"] = "14900000-0000-4000-8000-000000000001";
        var refused = await host.Send("POST", "/api/shipment-bundle/returns", body, host.Token(), "dl4-ref-" + Guid.NewGuid());
        Assert.Equal((400, "INVALID_REQUEST"), Pair(refused));
        Assert.Equal(before, await returns.CountDocumentsAsync(tenant));
    }

    private static (int, string?) Pair(Diten.SupplyChainService.Tests.Common.ReturnsClaimsHttpHost.Reply r) => (r.Status, r.Code);
}
