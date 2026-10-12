# WP-MVP1-FUB1 — çalışma zamanı yığını, MDM taklidi, prob ve sabotaj betikleri (2026-10-09)

> Kanıt: `mvp1-fub1-evidence-2026-10-09.md` §3–§6. Tek seferlik kanıt betikleri, repo kodu değil.
>
> **Yığın** (VIS1 harness'inden türetildi; Platform'un `MdmService__BaseUrl` değeri kendi taklidim
> `http://127.0.0.1:15059/`):
> 1. `FUB1Stack.csproj` + `Program.cs` dosyalarını boş bir klasöre koyun.
> 2. Worktree'nin AuthService, Platform, Gateway ve Web projelerini **Release** derleyin.
> 3. Önce `node --max-http-header-size=262144 mdm-stub.js <çıktı>` çalıştırın (büyük token başlıkları için; varsayılan sınır
>    431 döndürür).
> 4. Sonra `dotnet run -- <worktree> <çıktı>` çalıştırın.
>
> `<çıktı>/dbcopy.json` dosyası oluşturulunca yığın, kendi Platform DB'sindeki bir imza kaydını verilen şirketlerle kopyalar
> (`dbcopy-result.json`). Durdurmak için `<çıktı>/stop` dosyasını oluşturun; taklit kendi PID'iyle (`mdm-stub.pid`)
> kapatılır.
>
> **Prob:** `node probe.js <çıktı>`.
>
> **Sabotaj / önce-kırmızı:** `sabotage.ps1` (UTF-8 BOM'lu).
>
> **Q3 koşusu için kendi test mongod'u:** `testmongo` (`INVENTORY_TEST_MONGO` = `conn.txt`; asla 27017).

## FUB1Stack.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="EphemeralMongo.Core" Version="1.1.3" />
    <PackageReference Include="MongoDB.Driver" Version="2.27.0" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
  </ItemGroup>
</Project>
```

## Program.cs

```csharp
// WP-MVP1-FUB1 runtime stack (from the VIS1 harness; MDM = the own stub on 15059) (WP-0023-FX method). OWN mongod (replica set, random port, never 27017), OWN AuthService (15056),
// Platform (15057), Gateway (15191, TEMP copy of ocelot.json: 5057 → own Platform, 5056 → own AuthService, everything else → dead
// port 9), Web (15001, this branch, Release; its Mongo = the own mongod). Random secrets in memory; the test password is written
// ONLY to <out>/pw.txt (scratch, for the browser login) and never printed. Prepares: users (admin / A / B), the QA role, a PUBLISHED
// Workflow template E5W2-SIGN (signature step, meaning APPROVE) and READS the catalog row workflow.task.approve the Platform worker
// registered (CT-D19 — no manual registration). Then waits for <out>/stop and tears down only what it started.
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EphemeralMongo;
using MongoDB.Bson;
using MongoDB.Driver;

#pragma warning disable CS0618
BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618
var worktree = args[0];
var outDir = args[1];
Directory.CreateDirectory(outDir);
var transcript = new StringBuilder();
void Log(string line) { Console.WriteLine(line); transcript.AppendLine(line); File.WriteAllText(Path.Combine(outDir, "stack-transcript.txt"), transcript.ToString()); }

const string AuthUrl = "http://127.0.0.1:15056";
const string PlatformUrl = "http://127.0.0.1:15057";
const string GatewayUrl = "http://127.0.0.1:15191";
const string WebUrl = "http://127.0.0.1:15001";
const string DeadUrl = "http://127.0.0.1:9";
var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
var run = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
static string Random64() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
var jwt = Random64();
var internalKey = Random64();
var testPassword = "E5w!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "a1";
File.WriteAllText(Path.Combine(outDir, "pw.txt"), testPassword);

var runner = MongoRunner.Run(new MongoRunnerOptions
{
    BinaryDirectory = Environment.GetEnvironmentVariable("DITEN_ITEST_MONGOD_BIN_DIR") ?? @"C:\Program Files\MongoDB\Server\9.0\bin",
    UseSingleNodeReplicaSet = true, StandardOuputLogger = _ => { }, StandardErrorLogger = _ => { }
});
var processes = new List<Process>();
try
{
    var mongoUrl = MongoUrl.Create(runner.ConnectionString);
    if (mongoUrl.Servers.Any(s => s.Port == 27017)) throw new InvalidOperationException("refusing: runner on 27017");
    Log($"mongod (stack-owned) at port {mongoUrl.Servers.Single().Port}, replica set");
    var mongo = new MongoClient(runner.ConnectionString);
    var storageRoot = Path.Combine(outDir, "content");
    Directory.CreateDirectory(storageRoot);

    Dictionary<string, string> Common() => new()
    {
        ["JwtSettings__Secret"] = jwt, ["JwtSettings__Issuer"] = "diten-auth-service", ["JwtSettings__Audience"] = "diten-erp",
        ["Eventing__Transport"] = "InMemory", ["Observability__Seq__Url"] = DeadUrl, ["MongoDbSettings__ConnectionString"] = runner.ConnectionString,
    };
    Process Start(string name, string file, string arguments, string workDir, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo(file, arguments) { WorkingDirectory = workDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        var p = Process.Start(psi)!;
        var log = new StreamWriter(Path.Combine(outDir, $"stack-{name}.log")) { AutoFlush = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.WriteLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (log) log.WriteLine(e.Data); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        processes.Add(p);
        Log($"started {name} pid {p.Id}");
        return p;
    }
    Process StartDotnet(string name, string dll, string url, Dictionary<string, string> env, string? contentRoot = null)
    {
        env["ASPNETCORE_URLS"] = url;
        env["Kestrel__Endpoints__Http__Url"] = url;
        return Start(name, "dotnet", $"\"{dll}\" --urls {url}" + (contentRoot is null ? "" : $" --contentRoot \"{contentRoot}\""), contentRoot ?? Path.GetDirectoryName(dll)!, env);
    }
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    async Task WaitUp(string url, string what)
    {
        for (var i = 0; i < 240; i++) { try { using var r = await http.GetAsync(url); Log($"{what} answering ({(int)r.StatusCode})"); return; } catch { await Task.Delay(1000); } }
        throw new TimeoutException(what);
    }
    async Task WaitLog(string name, string needle)
    {
        var path = Path.Combine(outDir, $"stack-{name}.log");
        for (var i = 0; i < 240; i++)
        {
            string text;
            try { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); text = new StreamReader(fs).ReadToEnd(); } catch { text = ""; }
            if (text.Contains(needle)) return;
            await Task.Delay(1000);
        }
        throw new TimeoutException($"{name}: {needle}");
    }

    // ── AuthService + Platform ───────────────────────────────────────────────────────────────────────────────
    var authEnv = Common();
    authEnv["ASPNETCORE_ENVIRONMENT"] = "Development";
    authEnv["MongoDbSettings__DatabaseName"] = "itest_fub1_auth";
    authEnv["InternalEventAuth__ApiKey"] = internalKey;
    authEnv["PlatformService__InternalApiKey"] = internalKey;
    authEnv["PlatformService__BaseUrl"] = PlatformUrl;
    StartDotnet("auth", Path.Combine(worktree, @"services\Diten.AuthService\src\Diten.AuthService.Api\bin\Release\net8.0\Diten.AuthService.Api.dll"), AuthUrl, authEnv);
    await WaitUp(AuthUrl + "/health", "auth");
    await WaitLog("auth", "Seeding completed successfully.");

    var platformEnv = Common();
    platformEnv["ASPNETCORE_ENVIRONMENT"] = "Development";
    platformEnv["MongoDbSettings__DatabaseName"] = "itest_fub1_platform";
    platformEnv["BackgroundJobs__StorageDatabaseName"] = "itest_fub1_jobs";
    platformEnv["BackgroundJobs__Enabled"] = "false";
    platformEnv["Eventing__WorkerEnabled"] = "false";
    platformEnv["AuthService__BaseUrl"] = AuthUrl;
    platformEnv["AuthService__InternalApiKey"] = internalKey;
    platformEnv["ModuleRegistrationCredentials__Mdm__ActiveSecret"] = Random64();
    platformEnv["MdmService__BaseUrl"] = "http://127.0.0.1:15059/"; // the OWN MDM stub (mdm-stub.js), never the fleet's 5059
    platformEnv["WorkAggregation__RemoteProviders__0__BaseUrl"] = DeadUrl;
    platformEnv["DocumentManagement__ContentStorage__RootPath"] = storageRoot;
    platformEnv["ESign__SubjectProviders__quarantine__BaseUrl"] = DeadUrl;
    StartDotnet("platform", Path.Combine(worktree, @"services\Diten.Platform\src\Diten.Platform.API\bin\Release\net8.0\Diten.Platform.API.dll"), PlatformUrl, platformEnv);
    await WaitUp(PlatformUrl + "/health", "platform");

    // ── Gateway with a TEMP ocelot copy (the repo file is only read) ─────────────────────────────────────────
    var gwBin = Path.Combine(worktree, @"gateway\Diten.ApiGateway\bin\Release\net8.0");
    var gwRoot = Path.Combine(outDir, "gw");
    Directory.CreateDirectory(gwRoot);
    foreach (var f in Directory.GetFiles(gwBin, "appsettings*.json")) File.Copy(f, Path.Combine(gwRoot, Path.GetFileName(f)), true);
    var ocelot = JsonNode.Parse(File.ReadAllText(Path.Combine(worktree, @"gateway\Diten.ApiGateway\ocelot.json")).TrimStart('\uFEFF'))!.AsObject();
    var map = new Dictionary<int, int> { [5057] = 15057, [5056] = 15056 };
    foreach (var route in ocelot["Routes"]!.AsArray())
        foreach (var hp in route!["DownstreamHostAndPorts"]!.AsArray())
        {
            var port = hp!["Port"]!.GetValue<int>();
            hp["Host"] = "127.0.0.1";
            hp["Port"] = map.TryGetValue(port, out var own) ? own : 9;
        }
    ocelot["GlobalConfiguration"]!["BaseUrl"] = GatewayUrl;
    File.WriteAllText(Path.Combine(gwRoot, "ocelot.json"), ocelot.ToJsonString());
    var gwEnv = Common();
    gwEnv["ASPNETCORE_ENVIRONMENT"] = "Development";
    StartDotnet("gateway", Path.Combine(gwBin, "Diten.ApiGateway.dll"), GatewayUrl, gwEnv, gwRoot);
    await WaitUp(GatewayUrl + "/health", "gateway");

    // ── Web (this branch) ────────────────────────────────────────────────────────────────────────────────────
    var webBin = Path.Combine(worktree, @"frontend\Diten.Web\bin\Release\net8.0");
    StartDotnet("web", Path.Combine(webBin, "Diten.Web.dll"), WebUrl, new()
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Production", ["GatewayUrl"] = GatewayUrl, ["PlatformServiceUrl"] = PlatformUrl, ["ApiSettings__PublicApiUrl"] = DeadUrl,
        ["ConnectionStrings__MongoDb"] = runner.ConnectionString, ["DatabaseName"] = "itest_fub1_web",
        ["JwtSettings__Secret"] = jwt, ["JwtSettings__Issuer"] = "diten-auth-service", ["JwtSettings__Audience"] = "diten-erp",
        ["Platform__InternalApiKey"] = internalKey
    }, Path.Combine(worktree, @"frontend\Diten.Web"));
    await WaitUp(WebUrl + "/account/login", "web");

    // ── users + role ─────────────────────────────────────────────────────────────────────────────────────────
    var authDb = mongo.GetDatabase("itest_fub1_auth");
    var perms = authDb.GetCollection<BsonDocument>("permissions");
    for (var i = 0; i < 120 && await perms.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Key", "esign.my-samples.manage")) == 0; i++) await Task.Delay(1000);
    var users = authDb.GetCollection<BsonDocument>("users");
    var hash = BCrypt.Net.BCrypt.HashPassword(testPassword, 12);
    static Guid G(BsonValue v) => v.AsBsonBinaryData.ToGuid(v.AsBsonBinaryData.SubType == BsonBinarySubType.UuidStandard ? GuidRepresentation.Standard : GuidRepresentation.CSharpLegacy);
    async Task<Guid> PrepareUser(string email)
    {
        var doc = await users.Find(Builders<BsonDocument>.Filter.Eq("Email", email)).FirstAsync();
        await users.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("Email", email), Builders<BsonDocument>.Update
            .Set("PasswordHash", hash).Set("IsActive", true).Set("EmailConfirmed", true).Set("MustChangePassword", false).Set("FailedLoginAttempts", 0).Set("LockoutEnd", BsonNull.Value));
        return G(doc["_id"]);
    }
    var adminId = await PrepareUser("charlie.brown.def@diten.com");
    var userRoles = authDb.GetCollection<BsonDocument>("userRoles");
    var superAdmin = (await authDb.GetCollection<BsonDocument>("roles").Find(Builders<BsonDocument>.Filter.Eq("Name", "SuperAdmin")).ToListAsync()).First(r => G(r["TenantId"]) == tenantId);
    var template = (await userRoles.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()).First(ur => G(ur["RoleId"]) == G(superAdmin["_id"]));
    var clone = template.DeepClone().AsBsonDocument;
    clone["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
    clone["UserId"] = new BsonBinaryData(adminId, template["UserId"].AsBsonBinaryData.SubType == BsonBinarySubType.UuidStandard ? GuidRepresentation.Standard : GuidRepresentation.CSharpLegacy);
    await userRoles.InsertOneAsync(clone);
    var aId = await PrepareUser("john.doe.def@diten.com");
    var bId = await PrepareUser("jane.smith.def@diten.com");

    async Task<(int Status, JsonNode? Body)> Send(HttpMethod method, string url, string? token, object? body, string? key = null)
    {
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (key is not null) req.Headers.Add("Idempotency-Key", key);
        if (token is not null) req.Headers.Authorization = new("Bearer", token);
        if (body is not null) req.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonNode? node;
        try { node = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch { node = JsonValue.Create(text); }
        return ((int)res.StatusCode, node);
    }
    static string? Find(JsonNode? n, string prop)
    {
        if (n is JsonObject o)
            foreach (var kv in o)
            {
                if (string.Equals(kv.Key, prop, StringComparison.OrdinalIgnoreCase) && kv.Value is JsonValue) return kv.Value!.ToString();
                var inner = Find(kv.Value, prop); if (inner is not null) return inner;
            }
        if (n is JsonArray a) foreach (var x in a) { var inner = Find(x, prop); if (inner is not null) return inner; }
        return null;
    }
    async Task<string> Login(string email)
    {
        var (s, b) = await Send(HttpMethod.Post, AuthUrl + "/api/tenant-auth/login", null, new { email, password = testPassword, rememberMe = false });
        return (s == 200 ? Find(b, "accessToken") : null) ?? throw new InvalidOperationException($"login {email}: {s}");
    }
    var adminToken = await Login("charlie.brown.def@diten.com");
    async Task<Guid> PermId(string key) => G((await perms.Find(Builders<BsonDocument>.Filter.Eq("Key", key)).FirstAsync())["_id"]);
    string[] keys =
    [
        "platform.workflow.instances.start", "platform.workflow.instances.view", "platform.workflow.tasks.approve", "platform.workflow.tasks.reject",
        "platform.tasks.read", "platform.workflow.tasks.delegate", "esign.signatures.sign", "esign.signatures.read", "esign.my-samples.manage",
        "esign.requests.read", "esign.requests.reject", "esign.signatures.verify", "esign.signatures.export"
    ];
    var roleName = "fub1-qa-" + run.ToLowerInvariant();
    await Send(HttpMethod.Post, AuthUrl + "/api/roles", adminToken, new { name = roleName, displayName = "VIS1 QA", description = "WP-0022-VIS1 stack" });
    var (_, lb) = await Send(HttpMethod.Get, AuthUrl + "/api/roles", adminToken, null);
    var roleId = (lb?["data"] as JsonArray ?? lb as JsonArray)!.First(r => r?["name"]?.ToString() == roleName)!["id"]!.ToString();
    var granted = 0;
    foreach (var k in keys)
    {
        var (s, _) = await Send(HttpMethod.Post, $"{AuthUrl}/api/roles/{roleId}/permissions", adminToken, new { permissionId = await PermId(k) });
        if (s == 204) granted++; else Log($"grant {k}: {s}");
    }
    foreach (var id in new[] { aId, bId }) await Send(HttpMethod.Post, $"{AuthUrl}/api/users/{id}/roles", adminToken, new { roleId });
    var cId = await PrepareUser("bob.johnson.def@diten.com");
    var cRoleName = "fub1-c-" + run.ToLowerInvariant();
    await Send(HttpMethod.Post, AuthUrl + "/api/roles", adminToken, new { name = cRoleName, displayName = "VIS1 C (verify only)", description = "WP-0022-VIS1 stack" });
    var (_, lc) = await Send(HttpMethod.Get, AuthUrl + "/api/roles", adminToken, null);
    var cRoleId = (lc?["data"] as JsonArray ?? lc as JsonArray)!.First(r => r?["name"]?.ToString() == cRoleName)!["id"]!.ToString();
    var (cg, _) = await Send(HttpMethod.Post, $"{AuthUrl}/api/roles/{cRoleId}/permissions", adminToken, new { permissionId = await PermId("esign.signatures.verify") });
    var (cu, _) = await Send(HttpMethod.Post, $"{AuthUrl}/api/users/{cId}/roles", adminToken, new { roleId = cRoleId });
    Log($"user C={cId} role {cRoleName}: grant verify {cg}, assign {cu}");
    Log($"users: admin={adminId} A={aId} B={bId}; role {roleName} {granted}/{keys.Length}");

    // ── E5W2: a PUBLISHED signature template (API), and the catalog row the Platform worker registered (CT-D19) ──
    var definitionJson = "{ \"stages\": [ { \"code\": \"QA\", \"steps\": [ { \"code\": \"QA_APPROVAL\", \"name\": \"QA onayı\", \"type\": \"approval\", "
        + "\"assignment\": { \"candidatePrincipalIds\": [] }, \"requirements\": { \"commentRequired\": false, \"evidenceRequired\": false, \"signatureRequired\": true, \"signatureMeaning\": \"APPROVE\" } } ] } ] }";
    var templateCode = "E5W2-SIGN-" + run;
    var (dc, db) = await Send(HttpMethod.Post, GatewayUrl + "/api/v1/workflow/definitions", adminToken,
        new { templateCode, name = "E5W2 imzalı onay " + run, description = "WP-0022-E5W2 runtime probe", category = "probe", idempotencyKey = "e5w2-def-" + run });
    var definitionId = Find(db, "id") ?? Find(db, "definitionId");
    var (pc, pb) = await Send(HttpMethod.Post, $"{GatewayUrl}/api/v1/workflow/definitions/{definitionId}/publish", adminToken,
        new { definitionJson, schemaVersion = "workflow_schema_v1", expressionVersion = "workflow_expr_v1", publishReason = "E5W2 probe" });
    Log($"template {templateCode}: create {dc}, publish {pc} {(pc >= 300 ? pb?.ToJsonString() : "")}");
    string? catalog = null;
    for (var i = 0; i < 60 && catalog is null; i++)
    {
        var (ac, ab) = await Send(HttpMethod.Get, GatewayUrl + "/api/v1/esign/actions", adminToken, null);
        catalog = (ab?["data"] as JsonArray)?.FirstOrDefault(a => a?["actionCode"]?.ToString() == "workflow.task.approve")?.ToJsonString();
        if (catalog is null) await Task.Delay(2000);
    }
    Log("catalog workflow.task.approve (registered by the Platform startup worker): " + (catalog ?? "MISSING"));
    File.WriteAllText(Path.Combine(outDir, "stack.json"), new JsonObject
    {
        ["web"] = WebUrl, ["gateway"] = GatewayUrl, ["auth"] = AuthUrl, ["platform"] = PlatformUrl, ["tenant"] = tenantId.ToString(),
        ["admin"] = "charlie.brown.def@diten.com", ["a"] = "john.doe.def@diten.com", ["b"] = "jane.smith.def@diten.com",
        ["adminId"] = adminId.ToString(), ["c"] = "bob.johnson.def@diten.com", ["cId"] = cId.ToString(), ["aId"] = aId.ToString(), ["bId"] = bId.ToString(), ["templateCode"] = templateCode, ["definitionId"] = definitionId,
        ["mongo"] = runner.ConnectionString, ["pids"] = new JsonArray(processes.Select(p => (JsonNode)JsonValue.Create(p.Id)!).ToArray())
    }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Log("READY");
    while (!File.Exists(Path.Combine(outDir, "stop")))
    {
        var ask = Path.Combine(outDir, "dbread");
        if (File.Exists(ask))
        {
            var pdb = mongo.GetDatabase("itest_fub1_platform");
            var records = await pdb.GetCollection<BsonDocument>("esign_signature_records").Find(FilterDefinition<BsonDocument>.Empty)
                .Project(Builders<BsonDocument>.Projection.Include("SignatureNumber").Include("TenantId").Include("LegalEntityId").Include("SignerUserId")
                    .Include("Module").Include("SubjectType").Include("SubjectId")).ToListAsync();
            var exports = await pdb.GetCollection<BsonDocument>("esign_exports").Find(FilterDefinition<BsonDocument>.Empty)
                .Project(Builders<BsonDocument>.Projection.Exclude("Filter")).ToListAsync();
            var settings = new MongoDB.Bson.IO.JsonWriterSettings { OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson, Indent = true };
            File.WriteAllText(Path.Combine(outDir, "db-read.json"),
                "{ \"esign_signature_records\": " + new BsonArray(records).ToJson(settings) + ",\n  \"esign_exports\": " + new BsonArray(exports).ToJson(settings) + " }");
            File.Delete(ask);
            Log($"db read: {records.Count} records, {exports.Count} export log rows");
        }

        var copyAsk = Path.Combine(outDir, "dbcopy.json");
        if (File.Exists(copyAsk))
        {
            var copyReq = JsonNode.Parse(File.ReadAllText(copyAsk))!;
            var pdb = mongo.GetDatabase("itest_fub1_platform");
            var recs = pdb.GetCollection<BsonDocument>("esign_signature_records");
            var source = Guid.Parse(copyReq["sourceId"]!.ToString());
            var doc0 = await recs.Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(source, GuidRepresentation.Standard))).FirstAsync();
            var made = new JsonArray();
            foreach (var item in copyReq["items"]!.AsArray())
            {
                var doc = doc0.DeepClone().AsBsonDocument;
                var id = Guid.NewGuid();
                var number = "ESIG-FUB1-" + id.ToString("N")[..8].ToUpperInvariant();
                var le = Guid.Parse(item!["legalEntityId"]!.ToString());
                doc["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard);
                doc["LegalEntityId"] = new BsonBinaryData(le, GuidRepresentation.Standard);
                doc["SignerUserId"] = item["signerUserId"]!.ToString();
                doc["SignatureNumber"] = number;
                foreach (var name in doc.Names.Where(n => n.Contains("Idempotency", StringComparison.Ordinal)).ToList()) doc[name] = "copy-" + id.ToString("N");
                await recs.InsertOneAsync(doc);
                made.Add(new JsonObject { ["id"] = id.ToString("D"), ["number"] = number, ["legalEntityId"] = le.ToString("D") });
            }

            File.WriteAllText(Path.Combine(outDir, "dbcopy-result.json"), made.ToJsonString());
            File.Delete(copyAsk);
            Log($"db copy: {made.Count} company records from {source}");
        }

        await Task.Delay(2000);
    }
}
catch (Exception ex)
{
    Log("STACK ERROR: " + ex);
}
finally
{
    foreach (var p in processes) { try { if (!p.HasExited) p.Kill(entireProcessTree: true); p.WaitForExit(10000); } catch { } }
    runner.Dispose();
    File.Delete(Path.Combine(outDir, "pw.txt"));
    Log($"teardown: {processes.Count} processes stopped, stack mongod disposed, pw.txt deleted");
}
```

## mdm-stub.js

```js
// WP-MVP1-FUB1 — OWN MDM stub (port 15059, never the fleet's 5059): answers only the Legal Entity reference check Platform's
// MdmLegalEntityReferenceValidator calls (GET /api/legal-entities/{id}/lookup-validation) for the two probe companies.
// usage: node mdm-stub.js <out>  — writes <out>/mdm-stub.pid and logs every request to <out>/mdm-stub.log
const http = require('http');
const fs = require('fs');
const out = process.argv[2];
const companies = {
  '11111111-1111-4111-8111-111111111111': 'FUB1 Şirket 1',
  '22222222-2222-4222-8222-222222222222': 'FUB1 Şirket 2'
};
const log = (line) => fs.appendFileSync(out + '/mdm-stub.log', `${new Date().toISOString()} ${line}\n`);
http.createServer((req, res) => {
  const m = /^\/api\/legal-entities\/([0-9a-f-]{36})\/lookup-validation$/i.exec(req.url.split('?')[0]);
  const id = m && m[1].toLowerCase();
  if (id && companies[id]) {
    log(`200 ${req.method} ${req.url}`);
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      data: { legalEntityId: id, legalName: companies[id], displayName: companies[id], lifecycleState: 'ACTIVE', referenceable: true },
      statusCode: 200, isSuccessful: true, errors: []
    }));
    return;
  }
  log(`404 ${req.method} ${req.url}`);
  res.writeHead(404, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ data: null, statusCode: 404, isSuccessful: false, errors: ['not found'] }));
}).listen(15059, '127.0.0.1', () => { fs.writeFileSync(out + '/mdm-stub.pid', String(process.pid)); log('listening 15059'); });
```

## probe.js

```js
// WP-MVP1-FUB1 runtime probe on the OWN stack only (FUB1 harness + own MDM stub on 15059). Tokens from this stack's AuthService
// login; the sign ticket from this stack's esign/reauth with the real (test) password (<out>/pw.txt, never printed).
// usage: node probe.js <out>
//   1. the tenant has two companies (LE1, LE2 — MDM stub); the admin gives A a position in an org unit of LE1 ONLY (real API)
//   2. B signs a workflow task (tenant-level record); the harness copies it as a record of LE1 and one of LE2 (signer B)
//      — no signing provider in this stack (Platform only) produces a company record
//   3. A (read + verify, company scope = LE1) verifies: LE1 → 200, LE2 → 404, tenant-level → 200; search agrees;
//      B (signer) verifies the LE2 copy → 200
const fs = require('fs');
const out = process.argv[2];
const st = JSON.parse(fs.readFileSync(out + '/stack.json', 'utf8'));
const pw = fs.readFileSync(out + '/pw.txt', 'utf8').trim();
const LE1 = '11111111-1111-4111-8111-111111111111';
const LE2 = '22222222-2222-4222-8222-222222222222';
const results = [];
const rec = (id, what, expected, actual, ok) => { results.push({ id, what, expected, actual, ok: !!ok }); console.log(`${ok ? 'PASS' : 'FAIL'} ${id} ${what} — expected ${expected}, got ${actual}`); };
const key = (p) => `fub1-${p}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
const G = st.gateway;
const P = st.platform;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function call(method, url, token, body) {
  const headers = { 'Content-Type': 'application/json', 'X-Tenant-Id': st.tenant, 'Accept-Language': 'tr' };
  if (token) headers.Authorization = 'Bearer ' + token;
  const res = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch (_) { json = null; }
  return { status: res.status, json, text };
}
const data = (r) => (r.json && 'data' in r.json ? r.json.data : r.json);
async function login(email) {
  const r = await call('POST', st.auth + '/api/tenant-auth/login', null, { email, password: pw, rememberMe: false });
  const m = JSON.stringify(r.json || {}).match(/"accessToken":"([^"]+)"/);
  if (!m) throw new Error(`login ${email}: ${r.status}`);
  return m[1];
}

(async () => {
  const admin = await login(st.admin);
  const b = await login(st.b);

  // 1. A's position in an org unit of LE1 (platform org API, straight to the stack's own Platform)
  const code = 'FUB1-' + Date.now().toString(36).toUpperCase();
  const ou = await call('POST', `${P}/api/platform/organization-units`, admin, { code: code + '-OU', name: 'FUB1 OU (Şirket 1)', legalEntityId: LE1, parentOrganizationUnitId: null });
  const idOf = (r) => { const d = data(r); return typeof d === 'string' ? d : d && (d.id || d.organizationUnitId || d.positionId); };
  const ouId = idOf(ou);
  const pos = await call('POST', `${P}/api/platform/positions`, admin, { code: code + '-P', name: 'FUB1 QA (Şirket 1)', organizationUnitId: ouId, reportsToPositionId: null });
  const posId = idOf(pos);
  const asg = await call('POST', `${P}/api/platform/position-assignments`, admin, { positionId: posId, userId: st.aId, effectiveFrom: new Date(Date.now() - 3600000).toISOString(), effectiveTo: null });
  rec('F1', 'A gets a position in an org unit of company 1 only (org unit / position / assignment)', '2xx ×3',
    `${ou.status} / ${pos.status} / ${asg.status}`, ou.status < 300 && pos.status < 300 && asg.status < 300);
  if (!(ou.status < 300 && pos.status < 300 && asg.status < 300)) console.log(ou.text.slice(0, 300), pos.text.slice(0, 300), asg.text.slice(0, 300));
  const a = await login(st.a); // a fresh token after the assignment

  // 2. B signs a workflow task (tenant-level), the harness copies it into company 1 and company 2
  const objectId = `FUB1-${Date.now()}`;
  const started = await call('POST', G + '/api/v1/workflow/instances', a, {
    templateCode: st.templateCode, objectType: 'fub1.probe', objectId, objectRef: objectId, candidatePrincipalIds: ['user:' + st.bId],
    reasonCode: 'SUBMITTED', idempotencyKey: key('inst'), commentRequired: false, evidenceRequired: false
  });
  const instanceId = data(started).workflowInstanceId;
  const taskId = data(started).approvalTaskId;
  const ticket = data(await call('POST', G + '/api/tenant-auth/esign/reauth', b, { password: pw, intent: 'sign' })).ticket;
  const signed = await call('POST', G + '/api/v1/esign/signatures', b, {
    ticket, subject: { module: 'workflow', subjectType: 'workflow-task', subjectId: taskId }, actionCode: 'workflow.task.approve', meaningCode: 'APPROVE',
    appearance: { type: 'none' }, context: { workflowInstanceId: instanceId, workflowTaskId: taskId, stepCode: 'QA_APPROVAL', decision: 'approve' }, idempotencyKey: key('sign')
  });
  const s = data(signed);
  fs.writeFileSync(out + '/dbcopy.json', JSON.stringify({ sourceId: s.id, items: [{ legalEntityId: LE1, signerUserId: st.bId }, { legalEntityId: LE2, signerUserId: st.bId }] }));
  for (let i = 0; i < 30 && !fs.existsSync(out + '/dbcopy-result.json'); i++) await sleep(1000);
  const [c1, c2] = JSON.parse(fs.readFileSync(out + '/dbcopy-result.json', 'utf8'));
  rec('F2', 'B signs (tenant-level); copies as company 1 and company 2 records', 'sign 201, 2 copies', `sign ${signed.status} legalEntityId=${s.legalEntityId}; ${c1.number} (LE1), ${c2.number} (LE2)`,
    signed.status === 201 && s.legalEntityId === null && c1 && c2);

  // 3. verify
  const v = async (token, id) => (await call('POST', `${G}/api/v1/esign/signatures/${id}/verify`, token)).status;
  const a1 = await v(a, c1.id);
  const a2 = await v(a, c2.id);
  const a0 = await v(a, s.id);
  rec('F3', 'A (scope = company 1) verifies company 1 / company 2 / tenant-level', '200 / 404 / 200', `${a1} / ${a2} / ${a0}`, a1 === 200 && a2 === 404 && a0 === 200);
  const search = data(await call('GET', `${G}/api/v1/esign/signatures?pageSize=100`, a)).items.map((r) => r.signature.signatureNumber);
  rec('F4', 'A search agrees (same rule)', 'company 1 + tenant-level, not company 2', JSON.stringify(search),
    search.includes(c1.number) && search.includes(s.signatureNumber) && !search.includes(c2.number));
  const b2 = await v(b, c2.id);
  rec('F5', 'B (the signer) verifies the company 2 record', '200', `${b2}`, b2 === 200);
  const stubLog = fs.readFileSync(out + '/mdm-stub.log', 'utf8').split('\n').filter((l) => l.includes(LE1) || l.includes(LE2));
  rec('F6', 'the company scope came through the real chain (Platform → own MDM stub)', 'lookup-validation calls for LE1', `${stubLog.length} calls`, stubLog.some((l) => l.includes(LE1)));

  fs.writeFileSync(out + '/probe-api.json', JSON.stringify({ results, signature: s.signatureNumber, c1, c2, ouId, posId }, null, 2));
  console.log(`${results.filter((r) => r.ok).length}/${results.length} PASS`);
})().catch((e) => { console.error('PROBE ERROR', e.message); process.exit(1); });
```

## sabotage.ps1

```powershell
$ErrorActionPreference = 'Continue'
$env:DITEN_TEST_MONGOD = "C:\Program Files\MongoDB\Server\9.0\bin\mongod.exe"
$wt = "C:\Users\Ali\erp-vnext-wt-mvp1-fub1"
$web = "$wt\frontend\Diten.Web"
$webTests = "$wt\frontend\Diten.Web.Tests"
$platformTests = "$wt\services\Diten.Platform\tests\Diten.Platform.Application.Tests"
$out = "C:\Users\Ali\AppData\Local\Temp\claude\C--Users-Ali-erp-vnext\579e6d36-a23c-4122-9d9a-5cb9e96d4ad4\scratchpad\fub1"
$webFilter = 'FullyQualifiedName~InventoryProxyControllerTests'
$platformFilter = 'FullyQualifiedName~ESignatureTenantLevelVisibilityTests'

function Run($name, $tests, $filter) {
  $log = "$out\sab-$name.txt"
  dotnet test $tests --filter $filter -nologo *> $log
  $summary = (Select-String -Path $log -Pattern 'Başarılı!|Başarısız!|error CS' | Select-Object -Last 1).Line
  $failed = (Select-String -Path $log -Pattern '^\s+Başarısız Diten' | ForEach-Object { (($_.Line.Trim() -split ' \[')[0] -split '\.')[-1] }) -join '; '
  "$name => $summary"
  "   red: $failed"
}

function Patch($file, $from, $to) {
  $text = [IO.File]::ReadAllText($file)
  $n = ([regex]::Matches($text, [regex]::Escape($from))).Count
  if ($n -ne 1) { throw "anchor matches $n in $file" }
  [IO.File]::WriteAllText($file, $text.Replace($from, $to), (New-Object Text.UTF8Encoding $false))
}

# BEFORE-1: the ledger label files as they were at the base (9697fda76) — the new label test must be red
$labelFiles = @(Get-ChildItem "$web\Resources\Views\Inventory\StockLedger\StockLedgerIndex.*.resx" | ForEach-Object { $_.FullName }) +
  @("$web\Views\Inventory\StockLedger\_IndexL10n.cshtml", "$web\wwwroot\assets\js\Inventory\StockLedger\index.l10n.js")
git -C $wt checkout 9697fda76 -- $labelFiles
Run 'before-labels' $webTests $webFilter
git -C $wt checkout HEAD -- $labelFiles

# BEFORE-2: the product picker without the product read (today's text = the SKU row's name) — the picker test must be red
$picker = "$web\Models\Inventory\ProductPickerSource.cs"
Patch $picker 'var names = await ProductNamesAsync(picked.Where(r => r.SkuLevel != "Item").Select(r => r.ItemId), cancellationToken);' 'var names = new Dictionary<string, string>();'
Run 'before-picker' $webTests $webFilter
git -C $wt checkout -- $picker

# SABOTAGE (a): verify back to the VIS1 check (tenant-level only) — the out-of-scope test must be red
$handlers = "$wt\services\Diten.Platform\src\Diten.Platform.Application\Features\ESignature\Handlers\SignatureHandlers.cs"
Patch $handlers 'if (record is null || !(await _visibility.ScopeAsync(actor, ESignatureVisibilityPurpose.Read, ct)).CanSee(record))' 'if (record is null || (await _visibility.ScopeAsync(actor, ESignatureVisibilityPurpose.Read, ct)).HidesTenantLevel(record))'
Run 'sabotage-a-verify-scope' $platformTests $platformFilter
git -C $wt checkout -- $handlers

# SABOTAGE (b): the ledger list shows the raw level — the label test must be red
$ledger = "$web\wwwroot\assets\js\Inventory\StockLedger\index.js"
Patch $ledger "const skuLevelText = (level) => (level && L()['SkuLevel' + level]) || level || '';" "const skuLevelText = (level) => level || '';"
Run 'sabotage-b-raw-level' $webTests $webFilter
git -C $wt checkout -- $ledger

"restored:"
Run 'restored-web' $webTests $webFilter
Run 'restored-platform' $platformTests $platformFilter
"diff after restore (tracked files):"
git -C $wt status --porcelain
"(end)"
```

## testmongo/Program.cs

```csharp
// WP-0173-PM2: an OWN single-node replica set for the test suites (never 27017). Writes the connection string to
// <out>/conn.txt and the mongod PID to <out>/pid.txt; waits for <out>/stop; disposes only its own process.
using System.Diagnostics;
using EphemeralMongo;
using MongoDB.Driver;

var outDir = args[0];
Directory.CreateDirectory(outDir);
var before = Process.GetProcessesByName("mongod").Select(p => p.Id).ToHashSet();
using var runner = MongoRunner.Run(new MongoRunnerOptions
{
    BinaryDirectory = @"C:\Program Files\MongoDB\Server\9.0\bin",
    UseSingleNodeReplicaSet = true, StandardOuputLogger = _ => { }, StandardErrorLogger = _ => { }
});
var url = MongoUrl.Create(runner.ConnectionString);
if (url.Servers.Any(s => s.Port == 27017)) throw new InvalidOperationException("refusing: runner on 27017");
var own = Process.GetProcessesByName("mongod").Select(p => p.Id).Where(id => !before.Contains(id)).ToList();
File.WriteAllText(Path.Combine(outDir, "conn.txt"), runner.ConnectionString);
File.WriteAllText(Path.Combine(outDir, "pid.txt"), string.Join(",", own));
Console.WriteLine("READY " + string.Join(",", url.Servers.Select(s => s.Port)) + " pid " + string.Join(",", own));
while (!File.Exists(Path.Combine(outDir, "stop"))) Thread.Sleep(500);
Console.WriteLine("stopping");
```
