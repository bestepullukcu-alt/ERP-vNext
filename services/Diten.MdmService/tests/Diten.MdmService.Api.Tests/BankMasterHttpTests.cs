using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.RecordCodes;
using Diten.MdmService.Infrastructure.RecordCodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0289 DL-1 — the bank master on the PRODUCTION stack (MdmHttpHost): the real controllers, JWT, [HasPermission], the
/// MediatR pipeline with its validators, handlers, repositories, indexes and class maps, over a throwaway replica-set mongod.
/// Only the record code service is the host's fake (and, in two tests, a variant with the real adapter / none at all). Each
/// test makes its own tenant.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class BankMasterHttpTests(MdmHttpHost host)
{
    private static readonly string[] All =
    [
        "mdm.banks.read", "mdm.banks.create", "mdm.banks.update", "mdm.banks.activate", "mdm.banks.deactivate",
        "mdm.banks.delete", "mdm.banks.bulk-delete"
    ];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    private HttpClient Steward(Guid? tenant = null) => host.Client(host.Token(tenant ?? _tenant, _user, All));

    private static HttpRequestMessage Request(HttpMethod method, string url, object? body = null, int? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (ifMatch is { } version) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return request;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) => await Envelope.ReadAsync(response);

    private async Task<(Guid Id, string Code, int Version)> CreateBankAsync(HttpClient client, string name = "Http Bank", string? bic = null)
    {
        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks", new { name, countryCode = "TR", bic }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await BodyAsync(response)).GetProperty("data");
        return (data.GetProperty("id").GetGuid(), data.GetProperty("code").GetString()!, data.GetProperty("version").GetInt32());
    }

    // ------------------------------------------------------------------------------------------------------- AC-1

    [Fact]
    public async Task Mod0289Dl1_AC01_Post_banks_returns_201_a_Draft_with_the_service_code_and_no_tenant_in_the_answer()
    {
        var client = Steward();
        var response = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks",
            new { name = " Http Bank ", countryCode = "tr", bic = "abcd tr22", nationalBankCode = "12-34" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await BodyAsync(response);
        var data = body.GetProperty("data");
        Assert.True(body.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal("Draft", data.GetProperty("status").GetString());
        Assert.Equal("Http Bank", data.GetProperty("name").GetString());
        Assert.Equal("TR", data.GetProperty("countryCode").GetString());
        Assert.Equal("ABCDTR22", data.GetProperty("bic").GetString());
        Assert.Equal("1234", data.GetProperty("nationalBankCode").GetString());
        Assert.StartsWith("GMG-BNK-", data.GetProperty("code").GetString());
        Assert.DoesNotContain("tenant", data.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // The tenant on the stored document is the token's, written by the server.
        var raw = await host.Database.GetCollection<BsonDocument>("mdm_banks")
            .Find(new BsonDocument("_id", new BsonBinaryData(data.GetProperty("id").GetGuid(), GuidRepresentation.Standard))).SingleAsync();
        Assert.Equal(_tenant, raw["TenantId"].AsGuid);
    }

    [Fact]
    public async Task Mod0289Dl1_AC01_A_code_or_a_tenant_in_the_body_is_a_400_not_an_override()
    {
        var client = Steward();
        var withCode = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks", new { name = "N", countryCode = "TR", code = "BNK-0001" }));
        Assert.Equal(HttpStatusCode.BadRequest, withCode.StatusCode);
        Assert.Equal("MDM_BANK_CODE_FORBIDDEN", Envelope.FirstError(await BodyAsync(withCode)));

        var withTenant = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks", new { name = "N", countryCode = "TR", tenantId = Guid.NewGuid() }));
        Assert.Equal(HttpStatusCode.BadRequest, withTenant.StatusCode);
        Assert.StartsWith("MDM_BANK_VALIDATION_FAILED", Envelope.FirstError(await BodyAsync(withTenant)));

        var inQuery = await client.GetAsync($"/api/mdm/banks?tenantId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, inQuery.StatusCode);
        // Nothing was written by any of them.
        Assert.Equal(0, await host.Database.GetCollection<BsonDocument>("mdm_banks")
            .CountDocumentsAsync(new BsonDocument("TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard))));
    }

    // ------------------------------------------------------------------------------------------------------- AC-8

    [Fact]
    public async Task Mod0289Dl1_AC08_Without_a_token_it_is_401_and_without_the_key_each_action_is_403()
    {
        var anonymous = host.Client(null);
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D")); // past the tenant gate, to the authentication
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/mdm/banks")).StatusCode);

        var owner = Steward();
        var bank = await CreateBankAsync(owner);
        var branchResponse = await owner.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "001", name = "Main" }));
        Assert.Equal(HttpStatusCode.Created, branchResponse.StatusCode);
        var branchId = (await BodyAsync(branchResponse)).GetProperty("data").GetProperty("id").GetGuid();

        // A user who holds every OTHER key of the family: each action still needs its own.
        var calls = new (string Key, Func<HttpRequestMessage> Make)[]
        {
            ("mdm.banks.read", () => Request(HttpMethod.Get, "/api/mdm/banks")),
            ("mdm.banks.read", () => Request(HttpMethod.Get, $"/api/mdm/banks/{bank.Id}")),
            ("mdm.banks.read", () => Request(HttpMethod.Get, $"/api/mdm/banks/{bank.Id}/branches")),
            ("mdm.banks.read", () => Request(HttpMethod.Get, $"/api/mdm/bank-branches/{branchId}")),
            ("mdm.banks.create", () => Request(HttpMethod.Post, "/api/mdm/banks", new { name = "N", countryCode = "TR" })),
            ("mdm.banks.create", () => Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "009", name = "N" })),
            ("mdm.banks.update", () => Request(HttpMethod.Put, $"/api/mdm/banks/{bank.Id}", new { name = "N", countryCode = "TR" }, 0)),
            ("mdm.banks.update", () => Request(HttpMethod.Put, $"/api/mdm/bank-branches/{branchId}", new { branchCode = "001", name = "N" }, 0)),
            ("mdm.banks.activate", () => Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/activate", null, 0)),
            ("mdm.banks.activate", () => Request(HttpMethod.Post, $"/api/mdm/bank-branches/{branchId}/activate", null, 0)),
            ("mdm.banks.deactivate", () => Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/deactivate", new { reasonCode = "R" }, 0)),
            ("mdm.banks.deactivate", () => Request(HttpMethod.Post, $"/api/mdm/bank-branches/{branchId}/deactivate", new { reasonCode = "R" }, 0)),
            ("mdm.banks.delete", () => Request(HttpMethod.Delete, $"/api/mdm/banks/{bank.Id}")),
            ("mdm.banks.delete", () => Request(HttpMethod.Delete, $"/api/mdm/bank-branches/{branchId}")),
            ("mdm.banks.bulk-delete", () => Request(HttpMethod.Post, "/api/mdm/banks/bulk-delete", new { ids = new[] { bank.Id } }))
        };
        foreach (var (key, make) in calls)
        {
            var without = host.Client(host.Token(_tenant, _user, All.Where(k => k != key).ToArray()));
            var denied = await without.SendAsync(make());
            Assert.True(denied.StatusCode == HttpStatusCode.Forbidden, $"{make().Method} {make().RequestUri} without {key} was {(int)denied.StatusCode}");
        }

        // The denied calls changed nothing.
        var stored = await host.Database.GetCollection<BsonDocument>("mdm_banks")
            .Find(new BsonDocument("_id", new BsonBinaryData(bank.Id, GuidRepresentation.Standard))).SingleAsync();
        Assert.False(stored["IsDeleted"].AsBoolean);
        Assert.Equal(0, stored["Version"].AsInt32);
    }

    // ------------------------------------------------------------------------------------------------------- AC-3

    [Fact]
    public async Task Mod0289Dl1_AC03_Another_tenants_token_gets_404_for_the_bank_its_branches_and_every_write()
    {
        var owner = Steward();
        var bank = await CreateBankAsync(owner, "Mine");
        var branchResponse = await owner.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "001", name = "Main" }));
        var branchId = (await BodyAsync(branchResponse)).GetProperty("data").GetProperty("id").GetGuid();
        var stranger = Steward(Guid.NewGuid());

        foreach (var request in new[]
                 {
                     Request(HttpMethod.Get, $"/api/mdm/banks/{bank.Id}"),
                     Request(HttpMethod.Get, $"/api/mdm/banks/{bank.Id}/branches"),
                     Request(HttpMethod.Get, $"/api/mdm/bank-branches/{branchId}"),
                     Request(HttpMethod.Put, $"/api/mdm/banks/{bank.Id}", new { name = "Hijack", countryCode = "TR" }, 0),
                     Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/activate", null, 0),
                     Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/deactivate", new { reasonCode = "R" }, 0),
                     Request(HttpMethod.Delete, $"/api/mdm/banks/{bank.Id}"),
                     Request(HttpMethod.Post, "/api/mdm/banks/bulk-delete", new { ids = new[] { bank.Id } }),
                     Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "002", name = "X" }),
                     Request(HttpMethod.Put, $"/api/mdm/bank-branches/{branchId}", new { branchCode = "001", name = "Hijack" }, 0),
                     Request(HttpMethod.Delete, $"/api/mdm/bank-branches/{branchId}")
                 })
        {
            var response = await stranger.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{request.Method} {request.RequestUri} was {(int)response.StatusCode}");
        }

        var list = await BodyAsync(await stranger.GetAsync("/api/mdm/banks"));
        Assert.Equal(0, list.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal("Mine", (await BodyAsync(await owner.GetAsync($"/api/mdm/banks/{bank.Id}"))).GetProperty("data").GetProperty("name").GetString());
    }

    // --------------------------------------------------------------------------------- AC-4 / AC-5 / AC-9 over HTTP

    [Fact]
    public async Task Mod0289Dl1_AC09_If_Match_is_required_a_stale_one_is_409_and_the_version_moves_by_one()
    {
        var client = Steward();
        var bank = await CreateBankAsync(client);

        var missing = await client.SendAsync(Request(HttpMethod.Put, $"/api/mdm/banks/{bank.Id}", new { name = "N", countryCode = "TR" }));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var garbage = Request(HttpMethod.Put, $"/api/mdm/banks/{bank.Id}", new { name = "N", countryCode = "TR" });
        garbage.Headers.TryAddWithoutValidation("If-Match", "abc");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(garbage)).StatusCode);

        var updated = await client.SendAsync(Request(HttpMethod.Put, $"/api/mdm/banks/{bank.Id}", new { name = "Second", countryCode = "TR" }, bank.Version));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(1, (await BodyAsync(updated)).GetProperty("data").GetProperty("version").GetInt32());

        var stale = await client.SendAsync(Request(HttpMethod.Put, $"/api/mdm/banks/{bank.Id}", new { name = "Stale", countryCode = "TR" }, bank.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("MDM_BANK_VERSION_CONFLICT", Envelope.FirstError(await BodyAsync(stale)));
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/activate", null, bank.Version))).StatusCode);
        Assert.Equal("Second", (await BodyAsync(await client.GetAsync($"/api/mdm/banks/{bank.Id}"))).GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Mod0289Dl1_AC05_Activate_deactivate_and_delete_follow_the_state_table_over_HTTP()
    {
        var client = Steward();
        var bank = await CreateBankAsync(client);
        var active = await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/activate", null, bank.Version));
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Equal("Active", (await BodyAsync(active)).GetProperty("data").GetProperty("status").GetString());

        // A reason is required to deactivate.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/deactivate", new { reasonCode = " " }, 1))).StatusCode);
        // A bank that was activated is not deleted: 409 MDM_BANK_STATE_INVALID.
        var deleteActive = await client.SendAsync(Request(HttpMethod.Delete, $"/api/mdm/banks/{bank.Id}"));
        Assert.Equal(HttpStatusCode.Conflict, deleteActive.StatusCode);
        Assert.Equal("MDM_BANK_STATE_INVALID", Envelope.FirstError(await BodyAsync(deleteActive)));

        var draft = await CreateBankAsync(client, "Draft to delete");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Request(HttpMethod.Delete, $"/api/mdm/banks/{draft.Id}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/mdm/banks/{draft.Id}")).StatusCode);
    }

    [Fact]
    public async Task Mod0289Dl1_AC04_Bulk_delete_over_HTTP_is_all_or_none()
    {
        var client = Steward();
        var a = await CreateBankAsync(client, "A");
        var b = await CreateBankAsync(client, "B");
        var active = await CreateBankAsync(client, "C");
        await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{active.Id}/activate", null, active.Version));

        var refused = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks/bulk-delete", new { ids = new[] { a.Id, b.Id, active.Id } }));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/mdm/banks/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/mdm/banks/{b.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks/bulk-delete", new { ids = new[] { a.Id, b.Id } }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/mdm/banks/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks/bulk-delete", new { ids = Array.Empty<Guid>() }))).StatusCode);
    }

    [Fact]
    public async Task Mod0289Dl1_AC02_Duplicate_BIC_and_branch_code_are_409_over_HTTP_and_a_foreign_BIC_prefix_is_400()
    {
        var client = Steward();
        var bank = await CreateBankAsync(client, "A", "ABCDTR22");
        var duplicate = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks", new { name = "B", countryCode = "TR", bic = "ABCDTR22" }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("MDM_BANK_BIC_CONFLICT", Envelope.FirstError(await BodyAsync(duplicate)));
        var wrongCountry = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks", new { name = "C", countryCode = "TR", bic = "ABCDDE22" }));
        Assert.Equal(HttpStatusCode.BadRequest, wrongCountry.StatusCode);

        var first = await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "001", name = "Main", bic = "ABCDTR22123" }));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var again = await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "0-01", name = "Again" }));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("MDM_BANK_BRANCH_CODE_CONFLICT", Envelope.FirstError(await BodyAsync(again)));
        var otherBank = await client.SendAsync(Request(HttpMethod.Post, $"/api/mdm/banks/{bank.Id}/branches", new { branchCode = "002", name = "X", bic = "ZZZZTR22123" }));
        Assert.Equal(HttpStatusCode.BadRequest, otherBank.StatusCode);
    }

    // ------------------------------------------------------------------------------------------------------ AC-59

    [Fact]
    public async Task Mod0289Dl1_AC59_The_register_searches_text_over_HTTP_and_refuses_a_hundred_and_first_character()
    {
        var client = Steward();
        await CreateBankAsync(client, "Acme.Bank");
        await CreateBankAsync(client, "AcmeXBank");
        await CreateBankAsync(client, "Odd.*Name");
        await CreateBankAsync(client, "Paren(Bank");
        var other = Steward(Guid.NewGuid());
        await CreateBankAsync(other, "Acme.Bank");

        async Task<string[]> Search(string text)
        {
            var response = await client.GetAsync($"/api/mdm/banks?search={Uri.EscapeDataString(text)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await BodyAsync(response)).GetProperty("data").GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToArray();
        }

        Assert.Equal(["Acme.Bank"], await Search("acme.bank"));
        Assert.Equal(["Acme.Bank", "AcmeXBank"], await Search("ACME"));
        Assert.Equal(["Odd.*Name"], await Search(".*"));
        Assert.Equal(["Paren(Bank"], await Search("("));
        Assert.Equal(200, (int)(await client.GetAsync($"/api/mdm/banks?search={new string('a', 100)}")).StatusCode);
        var tooLong = await client.GetAsync($"/api/mdm/banks?search={new string('a', 101)}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.StartsWith("MDM_BANK_VALIDATION_FAILED", Envelope.FirstError(await BodyAsync(tooLong)));
    }

    // ------------------------------------------------------------------------------------------------------ AC-60

    [Fact]
    public async Task Mod0289Dl1_AC60_A_code_service_that_cannot_give_a_code_makes_Create_503_403_or_422_and_writes_nothing()
    {
        var client = Steward();
        var before = host.RecordCodes.Calls.Count;
        async Task<(HttpStatusCode Status, string? Error)> TryCreate(Func<RecordCodeAllocationResult> answer)
        {
            host.RecordCodes.Answer = answer;
            try
            {
                var response = await client.SendAsync(Request(HttpMethod.Post, "/api/mdm/banks", new { name = "Never", countryCode = "TR" }));
                return (response.StatusCode, Envelope.FirstError(await BodyAsync(response)));
            }
            finally
            {
                host.RecordCodes.Answer = null;
            }
        }

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "MDM_BANK_RECORD_CODE_UNAVAILABLE"),
            await TryCreate(() => RecordCodeAllocationResult.Unavailable("RECORD_CODE_UNAVAILABLE")));
        Assert.Equal((HttpStatusCode.Forbidden, "RECORD_CODE_PERMISSION_MISSING"),
            await TryCreate(() => RecordCodeAllocationResult.PermissionMissing("RECORD_CODE_PERMISSION_MISSING")));
        Assert.Equal((HttpStatusCode.Forbidden, "LEGAL_ENTITY_READ_PERMISSION_MISSING"),
            await TryCreate(() => RecordCodeAllocationResult.PermissionMissing("LEGAL_ENTITY_READ_PERMISSION_MISSING")));
        Assert.Equal(((HttpStatusCode)422, "MDM_BANK_LEGAL_ENTITY_UNKNOWN"), await TryCreate(RecordCodeAllocationResult.LegalEntityUnknown));

        Assert.Equal(before + 4, host.RecordCodes.Calls.Count);
        Assert.Equal(0, (await BodyAsync(await client.GetAsync("/api/mdm/banks"))).GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(0, await host.Database.GetCollection<BsonDocument>("mdm_banks")
            .CountDocumentsAsync(new BsonDocument("TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard))));
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_A_composition_with_no_code_port_fails_closed_and_so_does_the_real_adapter_without_configuration()
    {
        // No adapter registered at all: Create is 503 and the collection is not written.
        await using var unbound = host.Variant(services => services.RemoveAll<IRecordCodeAllocator>());
        var token = host.Token(_tenant, _user, All);
        var client = unbound.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/mdm/banks", new { name = "Unbound", countryCode = "TR" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MDM_BANK_RECORD_CODE_UNAVAILABLE", Envelope.FirstError(await BodyAsync(response)));

        // The REAL adapter, registered as production registers it, with no configuration in the environment: same answer.
        await using var real = host.Variant(services =>
        {
            services.RemoveAll<IRecordCodeAllocator>();
            services.Configure<RecordCodeAllocatorOptions>(_ => { });
            services.AddHttpClient<IRecordCodeAllocator, PlatformRecordCodeAllocatorClient>();
        });
        var realClient = real.CreateClient();
        realClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var unconfigured = await realClient.PostAsJsonAsync("/api/mdm/banks", new { name = "Unconfigured", countryCode = "TR" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unconfigured.StatusCode);
        Assert.Equal("MDM_BANK_RECORD_CODE_UNAVAILABLE", Envelope.FirstError(await BodyAsync(unconfigured)));

        Assert.Equal(0, await host.Database.GetCollection<BsonDocument>("mdm_banks")
            .CountDocumentsAsync(new BsonDocument("TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard))));
    }
}
