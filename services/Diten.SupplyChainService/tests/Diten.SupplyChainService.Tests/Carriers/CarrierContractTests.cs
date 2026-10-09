using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Diten.SupplyChainService.Domain.Features.Carriers;
using Diten.SupplyChainService.Persistence.Features.Carriers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Diten.SupplyChainService.Tests.Common;
namespace Diten.SupplyChainService.Tests.Carriers;
public sealed class CarrierContractTests
{
    private const string Database = "diten_mod0184_tests";
    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Connection => Environment.GetEnvironmentVariable("MOD0184_TEST_MONGO") ?? throw new InvalidOperationException("Isolated Mongo required.");
    private readonly Guid _tenant = Guid.NewGuid(), _le = Guid.NewGuid(), _actor = Guid.NewGuid();
    private sealed class Probe : ICarrierCommitProbe
    {
        public int RepositoryCalls;
        public string? FailPhase;
        public bool Unknown;
        public Task AtAsync(string phase, CancellationToken ct)
        {
            if (phase == FailPhase)
            {
                FailPhase = null;
                if (Unknown) throw new CarrierPersistenceUnavailableException();
                throw new IOException("Injected Carrier write failure.");
            }
            return Task.CompletedTask;
        }
    }
    private sealed class CountingRepository(ICarrierRepository inner, Probe probe) : ICarrierRepository
    {
        public Task<IReadOnlyList<Carrier>> QueryAsync(CarrierScope scope, CarrierStatus? status, CancellationToken ct)
        { Interlocked.Increment(ref probe.RepositoryCalls); return inner.QueryAsync(scope,status,ct); }
        public Task<CarrierMutationResult> CreateAsync(CarrierScope scope,string key,string fingerprint,Guid correlation,string code,string name,IReadOnlyList<string> modes,string? reference,CancellationToken ct)
        { Interlocked.Increment(ref probe.RepositoryCalls); return inner.CreateAsync(scope,key,fingerprint,correlation,code,name,modes,reference,ct); }
        public Task<CarrierMutationResult> ChangeStatusAsync(CarrierScope scope,Guid id,string key,string fingerprint,Guid correlation,CarrierStatus target,string reason,CancellationToken ct)
        { Interlocked.Increment(ref probe.RepositoryCalls); return inner.ChangeStatusAsync(scope,id,key,fingerprint,correlation,target,reason,ct); }
    }
    private sealed class Factory(Probe probe) : WebApplicationFactory<Program>
    {
        private static Dictionary<string, string?> Settings => new() { ["Mongo:ConnectionString"] = Connection, ["Mongo:DatabaseName"] = Database,
            ["JwtSettings:Secret"] = Secret, ["JwtSettings:Issuer"] = "carrier-tests", ["JwtSettings:Audience"] = "carrier-tests" };
        protected override IHost CreateHost(IHostBuilder builder)
        { builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings)); return base.CreateHost(builder); }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings));
            builder.ConfigureServices(s => {
                s.StubLegalEntityValidation();
                s.Replace(ServiceDescriptor.Singleton<ICarrierCommitProbe>(probe));
                s.Replace(ServiceDescriptor.Scoped<ICarrierRepository>(sp => new CountingRepository(new CarrierRepository(sp.GetRequiredService<IMongoDatabase>(), probe), probe)));
            });
        }
    }
    private string Token(Guid tenant, Guid le, string[]? permissions = null, string? badClaim = null, Guid? actor = null)
    {
        var claims = new List<Claim> { new("tenant_id", tenant.ToString()), new("legal_entity_id", le.ToString()), new("sub", (actor ?? _actor).ToString()) };
        if (badClaim is not null) claims.Add(new(badClaim, Guid.NewGuid().ToString()));
        claims.AddRange((permissions ?? ["read", "create", "status.change"]).Select(p => new Claim("permission", "supplychain.carriers." + p)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("carrier-tests", "carrier-tests", claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));
    }
    private static JsonObject Body(string code = "C") => new() { ["carrierCode"] = code, ["displayName"] = "Carrier", ["supportedModes"] = new JsonArray("Road") };
    private static JsonObject Change(string status) => new() { ["targetStatus"] = status, ["reasonCode"] = "" };
    private async Task<(int Status, JsonObject Body, string Correlation)> Send(HttpClient client, string method = "POST", string path = "", JsonObject? body = null,
        string key = "k", Guid? tenant = null, Guid? le = null, string[]? permissions = null, string? correlation = null,
        Action<HttpRequestMessage>? alter = null, string? badClaim = null, Guid? actor = null)
    {
        var t = tenant ?? _tenant; var l = le ?? _le;
        using var r = new HttpRequestMessage(new HttpMethod(method), "/api/shipment-bundle/carriers" + path);
        r.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token(t, l, permissions, badClaim, actor));
        r.Headers.TryAddWithoutValidation("X-Tenant-Id", t.ToString()); r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", l.ToString());
        r.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation ?? Guid.NewGuid().ToString()); r.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (body is not null) r.Content = JsonContent.Create(body);
        alter?.Invoke(r);
        using var response = await client.SendAsync(r);
        var parsed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var trace = response.Headers.GetValues("X-Correlation-Id").Single();
        if ((int)response.StatusCode >= 400)
        {
            Assert.Equal(trace, parsed["error"]!["correlationId"]!.GetValue<string>());
            Assert.Equal(new[] { "code", "correlationId", "message" }, parsed["error"]!.AsObject().Select(x => x.Key).Order().ToArray());
        }
        return ((int)response.StatusCode, parsed, trace);
    }
    private static IMongoDatabase Db(Factory f) => f.Services.GetRequiredService<IMongoDatabase>();
    private BsonDocument Scope() => new() { { "TenantId", _tenant.ToString() }, { "LegalEntityId", _le.ToString() } };
    private async Task<long> Count(Factory f, string collection) => await Db(f).GetCollection<BsonDocument>(collection).CountDocumentsAsync(Scope());
    private async Task<string[]> Snapshot(Factory f)
    {
        var result = new List<string>();
        foreach (var name in new[] { "carriers", "carrier_idempotency", "carrier_audit" })
            result.Add(new BsonArray(await Db(f).GetCollection<BsonDocument>(name).Find(Scope()).Sort(new BsonDocument("_id",1)).ToListAsync()).ToJson());
        return result.ToArray();
    }
    [Fact]
    public async Task ExactCreateListAndHistoricalReplay()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var root = Guid.NewGuid().ToString();
        var body = Body(" C "); body["displayName"] = " "; body["supportedModes"] = new JsonArray("Road", "Road");
        var first = await Send(c, body: body, correlation: root); Assert.Equal(201, first.Status);
        Assert.Equal(new[] { "carrierCode", "carrierId", "contractVersion", "idempotentReplay", "status" }, first.Body.Select(x=>x.Key).Order().ToArray());
        var id = first.Body["carrierId"]!.GetValue<string>();
        Assert.Equal(200, (await Send(c, path: $"/{id}/status", body: Change("Suspended"), key: "status")).Status);
        body["externalReference"] = null;
        var replay = await Send(c, body: body, actor: Guid.NewGuid(), correlation: Guid.Empty.ToString());
        Assert.Equal(201, replay.Status); Assert.True(replay.Body["idempotentReplay"]!.GetValue<bool>()); Assert.Equal("Active", replay.Body["status"]!.GetValue<string>());
        Assert.Equal(Guid.Empty.ToString(), replay.Correlation); Assert.Equal(2, await Count(app,"carrier_audit"));
        var audit = await Db(app).GetCollection<BsonDocument>("carrier_audit").Find((FilterDefinition<BsonDocument>)Scope() & new BsonDocument("Operation","createCarrier")).SingleAsync();
        Assert.Equal(root, audit["CorrelationId"].AsString); Assert.Equal(_actor.ToString(), audit["ActorId"].AsString);
        var list = await Send(c,"GET"); Assert.Equal("Suspended", list.Body["items"]![0]!["status"]!.GetValue<string>());
        body["externalReference"] = ""; Assert.Equal(409,(await Send(c,body:body)).Status);
    }
    [Theory]
    [InlineData("Active","Active",422)] [InlineData("Active","Suspended",200)] [InlineData("Active","Retired",200)]
    [InlineData("Suspended","Active",200)] [InlineData("Suspended","Suspended",422)] [InlineData("Suspended","Retired",200)]
    [InlineData("Retired","Active",422)] [InlineData("Retired","Suspended",422)] [InlineData("Retired","Retired",422)]
    public async Task AllLifecyclePairs(string from,string to,int expected)
    {
        using var app=new Factory(new());using var c=app.CreateClient();var created=await Send(c,body:Body());var id=created.Body["carrierId"]!.GetValue<string>();
        if(from!="Active") Assert.Equal(200,(await Send(c,path:$"/{id}/status",body:Change(from),key:"setup")).Status);
        var before=await Snapshot(app);var auditBefore=await Count(app,"carrier_audit");
        var result=await Send(c,path:$"/{id}/status",body:Change(to),key:"test");Assert.Equal(expected,result.Status);
        if(expected==422) Assert.Equal(before,await Snapshot(app)); else Assert.Equal(auditBefore+1,await Count(app,"carrier_audit"));
    }
    [Theory]
    [InlineData("entity")] [InlineData("receipt")] [InlineData("audit")] [InlineData("beforeCommit")]
    public async Task EveryWriteFaultRollsBackCreateAndStatus(string phase)
    {
        var probe=new Probe{FailPhase=phase};using var app=new Factory(probe);using var c=app.CreateClient();var before=await Snapshot(app);
        Assert.Equal(500,(await Send(c,body:Body())).Status);Assert.Equal(before,await Snapshot(app));
        var created=await Send(c,body:Body());Assert.Equal(201,created.Status);var id=created.Body["carrierId"]!.GetValue<string>();
        before=await Snapshot(app);probe.FailPhase=phase;
        Assert.Equal(500,(await Send(c,path:$"/{id}/status",body:Change("Suspended"),key:"s")).Status);Assert.Equal(before,await Snapshot(app));
        Assert.Equal(200,(await Send(c,path:$"/{id}/status",body:Change("Suspended"),key:"s")).Status);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AfterCommitFailureRecoversByReceipt(bool unknown)
    {
        var probe=new Probe{FailPhase="afterCommit",Unknown=unknown};using var app=new Factory(probe);using var c=app.CreateClient();
        Assert.Equal(unknown?503:500,(await Send(c,body:Body())).Status);Assert.Equal(1,await Count(app,"carrier_audit"));
        var replay=await Send(c,body:Body());Assert.Equal(201,replay.Status);Assert.True(replay.Body["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(1,await Count(app,"carriers"));Assert.Equal(1,await Count(app,"carrier_idempotency"));
    }
    [Fact]
    public async Task SimultaneousReplayAndCodeUniquenessHaveSingleWinners()
    {
        using var app=new Factory(new());using var c=app.CreateClient();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks=Enumerable.Range(0,8).Select(async _=>{await gate.Task;return await Send(c,body:Body());}).ToArray();gate.SetResult();
        var results=await Task.WhenAll(tasks);Assert.All(results,x=>Assert.Equal(201,x.Status));Assert.Single(results,x=>!x.Body["idempotentReplay"]!.GetValue<bool>());
        var id=results[0].Body["carrierId"]!.GetValue<string>();
        var changed=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>Send(c,path:$"/{id}/status",body:Change("Retired"),key:"s"+i)));
        Assert.Single(changed,x=>x.Status==200);Assert.Equal(7,changed.Count(x=>x.Status==422));
        var codes=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>Send(c,body:Body("same-code"),key:"c"+i)));
        Assert.Single(codes,x=>x.Status==201);Assert.Equal(7,codes.Count(x=>x.Status==409));Assert.Equal(3,await Count(app,"carrier_audit"));
    }
    [Fact]
    public async Task TwoByTwoScopesDeletedReservationsAndReceiptVisibility()
    {
        using var app=new Factory(new());using var c=app.CreateClient();var t2=Guid.NewGuid();var l2=Guid.NewGuid();var ids=new List<string>();
        foreach(var t in new[]{_tenant,t2}) foreach(var l in new[]{_le,l2})
        { var r=await Send(c,body:Body(),tenant:t,le:l);Assert.Equal(201,r.Status);ids.Add(r.Body["carrierId"]!.GetValue<string>());Assert.Equal(1,(await Send(c,"GET",tenant:t,le:l)).Body["total"]!.GetValue<int>()); }
        Assert.Equal(4,ids.Distinct().Count());
        Assert.Equal(404,(await Send(c,path:$"/{ids[0]}/status",body:Change("Suspended"),tenant:t2,key:"s")).Status);
        Assert.Equal(404,(await Send(c,path:$"/{ids[0]}/status",body:Change("Suspended"),le:l2,key:"s")).Status);
        await Db(app).GetCollection<BsonDocument>("carriers").UpdateOneAsync(Scope(),new BsonDocument("$set",new BsonDocument{{"IsDeleted",true},{"DeletedAt",DateTimeOffset.UtcNow.ToString("O")}}));
        Assert.Equal(0,(await Send(c,"GET")).Body["total"]!.GetValue<int>());Assert.Equal(409,(await Send(c,body:Body(),key:"new")).Status);
        Assert.Equal(201,(await Send(c,body:Body())).Status);Assert.Equal(404,(await Send(c,path:$"/{ids[0]}/status",body:Change("Suspended"),key:"s")).Status);
        Assert.Equal(201,(await Send(c,body:Body("c"),key:"lowercase")).Status);
    }
    [Theory]
    // R-2 dropped legal_entity_id from the signed trio — the scope arrives in X-Legal-Entity-Id — so a duplicate
    // of that claim is no longer a scope conflict. DuplicateLegalEntityClaimIsIgnored below asserts the new truth.
    [InlineData("tenant_id")] [InlineData("sub")]
    public async Task DuplicateClaimsFailClosed(string claim)
    {
        using var app=new Factory(new());using var c=app.CreateClient();Assert.Equal(claim == "sub" ? 401 : 403,(await Send(c,body:Body(),badClaim:claim,correlation:"bad")).Status);Assert.Equal(0,await Count(app,"carriers"));
    }
    // R-2's positive counterpart: a second, conflicting legal_entity_id claim changes nothing, because the
    // middleware never reads it. The scope the request is served under is the header's.
    [Fact]
    public async Task DuplicateLegalEntityClaimIsIgnored()
    {
        using var app=new Factory(new());using var c=app.CreateClient();
        Assert.Equal(201,(await Send(c,body:Body(),badClaim:"legal_entity_id")).Status);
        Assert.Equal(1,await Count(app,"carriers"));
    }
    [Theory]
    [InlineData("read")] [InlineData("create")] [InlineData("status.change")]
    public async Task OperationPermissionsAreIndependent(string grant)
    {
        using var app=new Factory(new());using var c=app.CreateClient();
        Assert.Equal(grant=="read"?200:403,(await Send(c,"GET",permissions:[grant])).Status);
        Assert.Equal(grant=="create"?201:403,(await Send(c,body:Body(),permissions:[grant])).Status);
        Assert.Equal(grant=="status.change"?404:403,(await Send(c,path:$"/{Guid.NewGuid()}/status",body:Change("Suspended"),permissions:[grant])).Status);
    }
    [Fact]
    public async Task OrderedHeaderBodyAndReplayValidation()
    {
        using var app=new Factory(new());using var c=app.CreateClient();
        Assert.Equal(401,(await Send(c,body:Body(),correlation:"bad",alter:r=>r.Headers.Remove("Authorization"))).Status);
        Assert.Equal(403,(await Send(c,body:Body(),permissions:[],correlation:"bad")).Status);
        var invalid=await Send(c,body:Body(),correlation:"bad",key:"");Assert.Equal(400,invalid.Status);Assert.StartsWith("X-Correlation-Id",invalid.Body["error"]!["message"]!.GetValue<string>());
        var badTenant=await Send(c,body:Body(),key:"",alter:r=>{r.Headers.Remove("X-Tenant-Id");r.Headers.Add("X-Tenant-Id","bad");});Assert.StartsWith("X-Tenant-Id",badTenant.Body["error"]!["message"]!.GetValue<string>());
        var mismatch=await Send(c,body:new JsonObject(),alter:r=>{r.Headers.Remove("X-Tenant-Id");r.Headers.Add("X-Tenant-Id",Guid.NewGuid().ToString());});Assert.Equal(404,mismatch.Status);
        Assert.Equal(201,(await Send(c,body:Body())).Status);Assert.Equal(400,(await Send(c,body:new JsonObject())).Status);
        Assert.Equal(415,(await Send(c,body:Body(),alter:r=>r.Content=new StringContent("{}",Encoding.UTF8,"text/plain"))).Status);
        Assert.Equal(400,(await Send(c,path:"/not-a-uuid/status",body:Change("Suspended"))).Status);
        Assert.Equal(400,(await Send(c,"GET",path:"?status=active")).Status);Assert.Equal(400,(await Send(c,"GET",path:"?tenantId=x")).Status);
        Assert.Equal(200,(await Send(c,"GET",path:"?unknown=x")).Status);
    }
    [Fact]
    public async Task HeaderBoundariesNilCorrelationAndSchemaStrings()
    {
        using var app=new Factory(new());using var c=app.CreateClient();
        foreach(var key in new[]{"x",new string('x',128),new string(' ',1),"a,b",string.Concat(Enumerable.Repeat("😀",128))})
            Assert.Equal(201,(await Send(c,body:Body(Guid.NewGuid().ToString()),key:key,correlation:Guid.Empty.ToString())).Status);
        Assert.Equal(400,(await Send(c,body:Body(),key:new string('x',129))).Status);
        Assert.Equal(400,(await Send(c,body:Body(),key:string.Concat(Enumerable.Repeat("😀",129)))).Status);
        Assert.Equal(400,(await Send(c,body:Body(),alter:r=>r.Headers.TryAddWithoutValidation("Idempotency-Key","other"))).Status);
        Assert.Equal(400,(await Send(c,body:Body(),alter:r=>r.Headers.TryAddWithoutValidation("X-Correlation-Id",Guid.NewGuid().ToString()))).Status);
        foreach(var field in new[]{"carrierCode","displayName","supportedModes"})
        {var b=Body();b.Remove(field);Assert.Equal(400,(await Send(c,body:b)).Status);b=Body();b[field]=null;Assert.Equal(400,(await Send(c,body:b)).Status);}
        var unknown=Body();unknown["tenantId"]=_tenant.ToString();Assert.Equal(400,(await Send(c,body:unknown)).Status);
        var empty=Body("");Assert.Equal(400,(await Send(c,body:empty)).Status);
        var duplicate=Body(" ");duplicate["displayName"]=" ";duplicate["supportedModes"]=new JsonArray("Road","Road");Assert.Equal(201,(await Send(c,body:duplicate,key:"blank-code")).Status);
    }

    [Fact]
    public async Task StatusReplayFingerprintAndDeletedHistoricalReceipt()
    {
        using var app=new Factory(new());using var c=app.CreateClient();var create=await Send(c,body:Body());var id=create.Body["carrierId"]!.GetValue<string>();
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks=Enumerable.Range(0,8).Select(async _=>{await gate.Task;return await Send(c,path:$"/{id}/status",body:Change("Suspended"),key:"same");}).ToArray();
        gate.SetResult();var results=await Task.WhenAll(tasks);Assert.All(results,r=>Assert.Equal(200,r.Status));Assert.Single(results,r=>!r.Body["idempotentReplay"]!.GetValue<bool>());
        var different=Change("Suspended");different["reasonCode"]="different";Assert.Equal(409,(await Send(c,path:$"/{id}/status",body:different,key:"same")).Status);
        Assert.Equal(403,(await Send(c,path:$"/{id}/status",body:Change("Suspended"),key:"same",permissions:[])).Status);
        var invalid=Change("Suspended");invalid.Remove("reasonCode");Assert.Equal(400,(await Send(c,path:$"/{id}/status",body:invalid,key:"same")).Status);
        await Db(app).GetCollection<BsonDocument>("carriers").UpdateOneAsync(Scope(),new BsonDocument("$set",new BsonDocument("IsDeleted",true)));
        var before=await Snapshot(app);Assert.Equal(200,(await Send(c,path:$"/{id}/status",body:Change("Suspended"),key:"same")).Status);
        Assert.Equal(404,(await Send(c,path:$"/{id}/status",body:Change("Active"),key:"fresh")).Status);Assert.Equal(before,await Snapshot(app));
    }
    [Fact]
    public async Task CanonicalFingerprintPreservesModeOrderDuplicatesAndStrings()
    {
        using var app=new Factory(new());using var c=app.CreateClient();var body=Body();body["supportedModes"]=new JsonArray("Road","Sea");
        Assert.Equal(201,(await Send(c,body:body)).Status);
        var reordered=new JsonObject { ["supportedModes"]=new JsonArray("Road","Sea"),["displayName"]="Carrier",["carrierCode"]="C",["externalReference"]=null };
        Assert.True((await Send(c,body:reordered)).Body["idempotentReplay"]!.GetValue<bool>());
        body["supportedModes"]=new JsonArray("Sea","Road");Assert.Equal(409,(await Send(c,body:body)).Status);
        body["supportedModes"]=new JsonArray("Road","Sea","Sea");Assert.Equal(409,(await Send(c,body:body)).Status);
        body["supportedModes"]=new JsonArray("Road","Sea");body["carrierCode"]=" C";Assert.Equal(409,(await Send(c,body:body)).Status);
        Assert.Equal(1,await Count(app,"carrier_audit"));
    }
    [Fact]
    public async Task SignedDuplicateJsonContextMemberIsRejected()
    {
        using var app=new Factory(new());using var c=app.CreateClient();
        // legal_entity_id is deliberately absent: R-2 removed it from UniqueSignedContextFields, so a duplicate
        // member for it is ignored rather than rejected.
        foreach(var name in new[]{"tenant_id","sub"})
        {
            var parts=Token(_tenant,_le).Split('.');var payload=Base64UrlEncoder.Decode(parts[1]);
            payload=payload[..^1]+",\""+name+"\":\""+Guid.NewGuid()+"\"}";
            var signed=parts[0]+"."+Base64UrlEncoder.Encode(payload);
            var token=signed+"."+Base64UrlEncoder.Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret),Encoding.UTF8.GetBytes(signed)));
            var result=await Send(c,body:Body(),alter:r=>{r.Headers.Remove("Authorization");r.Headers.Add("Authorization","Bearer "+token);});
            Assert.Equal(403,result.Status);
        }
        Assert.Equal(0,await Count(app,"carriers"));
    }

    [Fact]
    public async Task ConcurrentDifferentPayloadUsesFirstCommittedReceipt()
    {
        using var app=new Factory(new());using var c=app.CreateClient();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks=Enumerable.Range(0,6).Select(async i=>{await gate.Task;return await Send(c,body:Body("Code"+i));}).ToArray();gate.SetResult();
        var results=await Task.WhenAll(tasks);Assert.Single(results,r=>r.Status==201);Assert.Equal(5,results.Count(r=>r.Status==409));
        Assert.Equal(1,await Count(app,"carriers"));Assert.Equal(1,await Count(app,"carrier_idempotency"));Assert.Equal(1,await Count(app,"carrier_audit"));
    }
    [Fact]
    public async Task IndexCreationFailureIsNotIgnored()
    {
        using var app=new Factory(new());using var c=app.CreateClient();
        var database=Db(app).Client.GetDatabase("diten_mod0184_index_failure_tests");var rows=database.GetCollection<BsonDocument>("carriers");
        foreach(var ignored in new[]{1,2}) await rows.InsertOneAsync(new BsonDocument { { "TenantId",_tenant.ToString() }, { "LegalEntityId",_le.ToString() }, { "CarrierCode","duplicate-index-fixture" } });
        await Assert.ThrowsAnyAsync<MongoException>(()=>new CarrierSchema(database).StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task BarrierSynchronizedDistinctTargetsEqualValidSerialHistory()
    {
        using var app=new Factory(new());using var c=app.CreateClient();var create=await Send(c,body:Body());var id=create.Body["carrierId"]!.GetValue<string>();
        var ready=new CountdownEvent(2);var go=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(int Status,JsonObject Body,string Correlation)> Attempt(string target)
        { ready.Signal();await go.Task;return await Send(c,path:$"/{id}/status",body:Change(target),key:target); }
        var suspended=Attempt("Suspended");var retired=Attempt("Retired");Assert.Equal(0,ready.CurrentCount);go.SetResult();
        var results=await Task.WhenAll(suspended,retired);Assert.Equal(200,results[1].Status);Assert.Contains(results[0].Status,new[]{200,422});
        var entity=await Db(app).GetCollection<BsonDocument>("carriers").Find(Scope()).SingleAsync();Assert.Equal("Retired",entity["Status"].AsString);
        var audits=await Db(app).GetCollection<BsonDocument>("carrier_audit").Find(Scope()).Sort(new BsonDocument("Version",1)).ToListAsync();
        var expected=results[0].Status==200?new[]{"Active","Suspended","Retired"}:new[]{"Active","Retired"};
        Assert.Equal(expected,audits.Select(a=>a["ToStatus"].AsString).ToArray());Assert.Equal(expected.Length,entity["Version"].AsInt32);
        Assert.Equal(expected.Length,await Count(app,"carrier_idempotency"));
    }

    [Fact]
    public async Task ValidationStagesOneThroughFiveDoNotCallRepository()
    {
        var probe=new Probe();using var app=new Factory(probe);using var c=app.CreateClient();
        Assert.Equal(401,(await Send(c,body:Body(),correlation:"bad",alter:r=>r.Headers.Remove("Authorization"))).Status);
        Assert.Equal(403,(await Send(c,body:Body(),permissions:[],correlation:"bad")).Status);
        Assert.Equal(400,(await Send(c,body:Body(),correlation:"bad")).Status);
        Assert.Equal(400,(await Send(c,body:Body(),key:"")).Status);
        // The claim-versus-header mismatch stage is gone: after R-2 an unknown legal entity is MDM's answer, not a
        // local comparison. LegalEntityMiddlewareWiringTests asserts Carriers' 404 on NotReferenceable without
        // needing an MDM; this suite's host stubs the validator, so the line that used to live here is there now.
        Assert.Equal(400,(await Send(c,path:"/bad/status",body:Change("Active"))).Status);
        Assert.Equal(400,(await Send(c,"GET",path:"?status=bad")).Status);
        Assert.Equal(415,(await Send(c,body:Body(),alter:r=>r.Content=new StringContent("{}",Encoding.UTF8,"text/plain"))).Status);
        Assert.Equal(400,(await Send(c,body:new JsonObject())).Status);
        Assert.Equal(0,probe.RepositoryCalls);
        Assert.Equal(201,(await Send(c,body:Body())).Status);Assert.Equal(1,probe.RepositoryCalls);
    }
}
