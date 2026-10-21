# MVP-1 SEC-PROXY1 — scripts (WP-MVP1-SEC-PROXY1) — 2026-10-14

> Companion of `mvp1-sec-proxy1-evidence-2026-10-14.md`. Everything here ran from a scratch folder outside the repository; nothing in it is
> product code. Ports: Web 5104, gateway copy 5105, fake downstream 5106 (none in 5000–5070 / 4010).

## 1. Fake downstream (`downstream.js`, node)

```js
// WP-MVP1-SEC-PROXY1 probe — the fake downstream behind the gateway copy: records every request it receives (raw request
// target as it arrived) to downstream.log, one JSON line each, and answers 200 with what it saw.
const http = require('http');
const fs = require('fs');
const path = require('path');
const log = path.join(__dirname, 'downstream.log');
fs.writeFileSync(log, '');
http.createServer((req, res) => {
  const entry = { method: req.method, url: req.url, tenant: req.headers['x-tenant-id'] || null };
  fs.appendFileSync(log, JSON.stringify(entry) + '\n');
  res.writeHead(200, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ downstream: entry.url }));
}).listen(5106, '127.0.0.1', () => console.log('fake downstream on 127.0.0.1:5106'));
```

## 2. Gateway copy

The built `gateway/Diten.ApiGateway/bin/Debug/net8.0/Diten.ApiGateway.dll` of this worktree (gateway code unchanged), content root = `probe/gateway/`
holding copies of `appsettings.json`, `appsettings.Development.json` and `ocelot.json`; in the `ocelot.json` copy every route's
`DownstreamHostAndPorts` → `127.0.0.1:5106` (306 routes), nothing else changed:

```js
const o = JSON.parse(fs.readFileSync('gateway/Diten.ApiGateway/ocelot.json', 'utf8'));
for (const r of o.Routes) { r.DownstreamHostAndPorts = [{ Host: '127.0.0.1', Port: 5106 }]; r.DownstreamScheme = 'http'; }
fs.writeFileSync('probe/gateway/ocelot.json', JSON.stringify(o, null, 2));
```

```bash
ASPNETCORE_ENVIRONMENT=Development JwtSettings__Secret="probe-only-unused-secret-<random>" \
  dotnet gateway/Diten.ApiGateway/bin/Debug/net8.0/Diten.ApiGateway.dll --urls http://127.0.0.1:5105 --contentRoot probe/gateway
```

(`JwtSettings__Secret` only satisfies the gateway's start-up check; nothing is signed with it.)

## 3. Web probe host + driver (`WebProbe/`)

Built twice: `-p:WebProject=<base worktree 9362c0402>/frontend/Diten.Web/Diten.Web.csproj -o out-before` and
`-p:WebProject=<this worktree>/frontend/Diten.Web/Diten.Web.csproj -o out-after`; run with
`ASPNETCORE_TEST_CONTENTROOT_DITEN_WEB=<that worktree>/frontend/Diten.Web` and `dotnet out-<x>/WebProbe.dll "<label>" <downstream.log>`.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- WP-MVP1-SEC-PROXY1 probe host: the production Diten.Web (from $(WebProject)) on real Kestrel 127.0.0.1:5104 via
       WebApplicationFactory, identity injected by a test-host middleware (no token is signed). -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
    <ProjectReference Include="$(WebProject)" />
  </ItemGroup>
</Project>
```

```csharp
// WP-MVP1-SEC-PROXY1 probe driver.
//   Web (production Program, real Kestrel) 127.0.0.1:5104 → gateway copy 127.0.0.1:5105 (copied ocelot.json, downstreams → 5106)
//   → fake downstream 127.0.0.1:5106 (records the request target).
// Identity: a test-host middleware puts a principal on HttpContext.User when the request carries X-Probe-Actor; ShellAccessFilter
// keeps an authenticated principal with actor_type as it is. The access_token cookie is an opaque string (no JWT is signed); the
// gateway copy cannot validate it, so it forwards on the X-Tenant-Id the Web sends — the escape under test is the Web's URL.
// Requests are written as raw HTTP/1.1 on a socket so no client normalizes the path.
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

static class ProbeEntry
{
    public static async Task Main(string[] args)
    {
var label = args.Length > 0 ? args[0] : "run";
var downstreamLog = args.Length > 1 ? args[1] : throw new ArgumentException("downstream.log path");

using var factory = new ProbeFactory();
_ = factory.Server; // builds + starts both hosts (TestServer and Kestrel)

var rows = new (string Proxy, string Actor, string Target)[]
{
    ("Expiry", "tenant_user", "/Expiry/api/..%5Clocations?type=Warehouse"),
    ("Expiry", "tenant_user", "/Expiry/api/..%255Clocations?type=Warehouse"),
    ("Expiry", "tenant_user", "/Expiry/api/%2E%2E%5Clocations"),
    ("Expiry", "tenant_user", "/Expiry/api/..%2Flocations"),
    ("Expiry", "tenant_user", "/Expiry/api/exceptions?status=OPEN"),
    ("Quarantine", "tenant_user", "/Quarantine/api/..%5Clocations?type=Warehouse"),
    ("Quarantine", "tenant_user", "/Quarantine/api/holds?page=1"),
    ("ESignature", "tenant_user", "/ESignature/api/..%5C..%5Clocations?type=Warehouse"),
    ("ESignature", "tenant_user", "/ESignature/api/meanings?activeOnly=true"),
    ("ReferenceData", "tenant_user", "/Platform/ReferenceData/api/..%5C..%5Clocations?type=Warehouse"),
    ("ReferenceData", "tenant_user", "/Platform/ReferenceData/api/usage-registrations?set_code=uom"),
    ("Workflow", "tenant_user", "/Platform/Workflow/api/..%5C..%5Clocations?type=Warehouse"),
    ("Workflow", "tenant_user", "/Platform/Workflow/api/definitions"),
    ("Tenants", "platform_admin", "/Platform/Tenants/api/lookups/..%5Clocations?type=Warehouse"),
    ("Tenants", "platform_admin", "/Platform/Tenants/api/lookups/countries"),
};

Console.WriteLine($"## {label}");
Console.WriteLine("| proxy | request to Web :5104 | Web status | Web body (first 110) | downstream :5106 received |");
Console.WriteLine("|---|---|---|---|---|");
foreach (var (proxy, actor, target) in rows)
{
    var before = File.ReadAllLines(downstreamLog).Length;
    var (status, body) = await Raw(target, actor);
    await Task.Delay(400);
    var seen = File.ReadAllLines(downstreamLog).Skip(before).ToList();
    var received = seen.Count == 0 ? "— (nothing)" : string.Join("<br>", seen.Select(l => System.Text.Json.JsonDocument.Parse(l).RootElement.GetProperty("url").GetString()));
    var snippet = body.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
    var why = System.Text.RegularExpressions.Regex.Match(snippet, @"""code"":""(\w+)"".*""reason"":""([\w-]+)""");
    if (why.Success) snippet = why.Groups[1].Value + " / reason=" + why.Groups[2].Value;
    else if (snippet.Length > 110) snippet = snippet[..110] + "…";
    Console.WriteLine($"| {proxy} | `{target}` | {status} | `{snippet}` | {(seen.Count == 0 ? received : "`" + received + "`")} |");
}
    }

    private static async Task<(int Status, string Body)> Raw(string target, string actor)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync("127.0.0.1", 5104);
    await using var stream = tcp.GetStream();
    var request = $"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:5104\r\nAccept: application/json\r\nCookie: access_token=probe-opaque-token\r\nX-Probe-Actor: {actor}\r\nConnection: close\r\n\r\n";
    await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
    using var reader = new StreamReader(stream, Encoding.UTF8);
    var text = await reader.ReadToEndAsync();
    var statusLine = text.Split("\r\n")[0];
    var status = int.Parse(statusLine.Split(' ')[1]);
    var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
    var body = split < 0 ? "" : text[(split + 4)..];
    if (text.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
    {
        var sb = new StringBuilder();
        var rest = body;
        while (rest.Length > 0)
        {
            var nl = rest.IndexOf("\r\n", StringComparison.Ordinal);
            if (nl < 0) break;
            var size = Convert.ToInt32(rest[..nl].Trim(), 16);
            if (size == 0) break;
            sb.Append(rest.Substring(nl + 2, Math.Min(size, rest.Length - nl - 2)));
            rest = rest.Length > nl + 2 + size + 2 ? rest[(nl + 2 + size + 2)..] : "";
        }

        body = sb.ToString();
    }

    return (status, body.Trim());
}
}

sealed class ProbeFactory : WebApplicationFactory<Program>
{
    private IHost? _kestrel;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("GatewayUrl", "http://127.0.0.1:5105");
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ProbeIdentity>());
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();
        builder.ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:5104"));
        _kestrel = builder.Build();
        _kestrel.Start();
        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        _kestrel?.StopAsync().GetAwaiter().GetResult();
        _kestrel?.Dispose();
        base.Dispose(disposing);
    }
}

sealed class ProbeIdentity : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, inner) =>
        {
            var actor = context.Request.Headers["X-Probe-Actor"].ToString();
            if (!string.IsNullOrEmpty(actor))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("tenantId", "6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"),
                    new Claim("sub", "0d9c8b7a-6f5e-4d3c-2b1a-0f9e8d7c6b5a"),
                    new Claim("legal_entity_id", "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d"),
                    new Claim("actor_type", actor),
                    new Claim("permission", "Platform.BusinessReferenceData.Read")
                ], "probe"));
            }

            await inner();
        });
        next(app);
    };
}
```

## 4. Raw probe output

### BEFORE - base 9362c0402
| proxy | request to Web :5104 | Web status | Web body (first 110) | downstream :5106 received |
|---|---|---|---|---|
| Expiry | `/Expiry/api/..%5Clocations?type=Warehouse` | 200 | `{"downstream":"/api/locations?type=Warehouse"}` | `/api/locations?type=Warehouse` |
| Expiry | `/Expiry/api/..%255Clocations?type=Warehouse` | 200 | `{"downstream":"/api/expiry/..%5Clocations?type=Warehouse"}` | `/api/expiry/..%5Clocations?type=Warehouse` |
| Expiry | `/Expiry/api/%2E%2E%5Clocations` | 200 | `{"downstream":"/api/locations"}` | `/api/locations` |
| Expiry | `/Expiry/api/..%2Flocations` | 200 | `{"downstream":"/api/expiry/..%2Flocations"}` | `/api/expiry/..%2Flocations` |
| Expiry | `/Expiry/api/exceptions?status=OPEN` | 200 | `{"downstream":"/api/expiry/exceptions?status=OPEN"}` | `/api/expiry/exceptions?status=OPEN` |
| Quarantine | `/Quarantine/api/..%5Clocations?type=Warehouse` | 200 | `{"downstream":"/api/locations?type=Warehouse"}` | `/api/locations?type=Warehouse` |
| Quarantine | `/Quarantine/api/holds?page=1` | 200 | `{"downstream":"/api/quarantine/holds?page=1"}` | `/api/quarantine/holds?page=1` |
| ESignature | `/ESignature/api/..%5C..%5Clocations?type=Warehouse` | 200 | `{"downstream":"/api/locations?type=Warehouse"}` | `/api/locations?type=Warehouse` |
| ESignature | `/ESignature/api/meanings?activeOnly=true` | 200 | `{"downstream":"/api/v1/esign/meanings?activeOnly=true"}` | `/api/v1/esign/meanings?activeOnly=true` |
| ReferenceData | `/Platform/ReferenceData/api/..%5C..%5Clocations?type=Warehouse` | 200 | `{"downstream":"/api/locations?type=Warehouse"}` | `/api/locations?type=Warehouse` |
| ReferenceData | `/Platform/ReferenceData/api/usage-registrations?set_code=uom` | 200 | `{"downstream":"/api/v1/reference-data/usage-registrations?set_code=uom"}` | `/api/v1/reference-data/usage-registrations?set_code=uom` |
| Workflow | `/Platform/Workflow/api/..%5C..%5Clocations?type=Warehouse` | 200 | `{"downstream":"/api/locations?type=Warehouse"}` | `/api/locations?type=Warehouse` |
| Workflow | `/Platform/Workflow/api/definitions` | 200 | `{"downstream":"/api/v1/workflow/definitions"}` | `/api/v1/workflow/definitions` |
| Tenants | `/Platform/Tenants/api/lookups/..%5Clocations?type=Warehouse` | 200 | `{"downstream":"/api/locations?type=Warehouse"}` | `/api/locations?type=Warehouse` |
| Tenants | `/Platform/Tenants/api/lookups/countries` | 200 | `{"downstream":"/api/lookups/countries"}` | `/api/lookups/countries` |

### AFTER - fix/mvp1-sec-proxy1 (working tree)
| proxy | request to Web :5104 | Web status | Web body (first 110) | downstream :5106 received |
|---|---|---|---|---|
| Expiry | `/Expiry/api/..%5Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| Expiry | `/Expiry/api/..%255Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| Expiry | `/Expiry/api/%2E%2E%5Clocations` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| Expiry | `/Expiry/api/..%2Flocations` | 400 | `INVALID_PROXY_PATH / reason=dot-segment` | - (nothing) |
| Expiry | `/Expiry/api/exceptions?status=OPEN` | 200 | `{"downstream":"/api/expiry/exceptions?status=OPEN"}` | `/api/expiry/exceptions?status=OPEN` |
| Quarantine | `/Quarantine/api/..%5Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| Quarantine | `/Quarantine/api/holds?page=1` | 200 | `{"downstream":"/api/quarantine/holds?page=1"}` | `/api/quarantine/holds?page=1` |
| ESignature | `/ESignature/api/..%5C..%5Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| ESignature | `/ESignature/api/meanings?activeOnly=true` | 200 | `{"downstream":"/api/v1/esign/meanings?activeOnly=true"}` | `/api/v1/esign/meanings?activeOnly=true` |
| ReferenceData | `/Platform/ReferenceData/api/..%5C..%5Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| ReferenceData | `/Platform/ReferenceData/api/usage-registrations?set_code=uom` | 200 | `{"downstream":"/api/v1/reference-data/usage-registrations?set_code=uom"}` | `/api/v1/reference-data/usage-registrations?set_code=uom` |
| Workflow | `/Platform/Workflow/api/..%5C..%5Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| Workflow | `/Platform/Workflow/api/definitions` | 200 | `{"downstream":"/api/v1/workflow/definitions"}` | `/api/v1/workflow/definitions` |
| Tenants | `/Platform/Tenants/api/lookups/..%5Clocations?type=Warehouse` | 400 | `INVALID_PROXY_PATH / reason=backslash` | - (nothing) |
| Tenants | `/Platform/Tenants/api/lookups/countries` | 200 | `{"downstream":"/api/lookups/countries"}` | `/api/lookups/countries` |

Downstream log after the AFTER run (6 lines — the legitimate requests only):

```text
{"method":"GET","url":"/api/expiry/exceptions?status=OPEN","tenant":"6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"}
{"method":"GET","url":"/api/quarantine/holds?page=1","tenant":"6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"}
{"method":"GET","url":"/api/v1/esign/meanings?activeOnly=true","tenant":"6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"}
{"method":"GET","url":"/api/v1/reference-data/usage-registrations?set_code=uom","tenant":"6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"}
{"method":"GET","url":"/api/v1/workflow/definitions","tenant":"6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b"}
{"method":"GET","url":"/api/lookups/countries","tenant":null}
```

## 5. Sabotage tool (`sabotage-proxy.js`) and its output

```js
// WP-MVP1-SEC-PROXY1 sabotage tool: apply → build → run the Security filter (red expected) → restore (mtime stamped) → build → green.
// Usage (from the worktree root): node sabotage-proxy.js
const fs = require('fs');
const { execSync } = require('child_process');
const crypto = require('crypto');

const helper = 'frontend/Diten.Web/Services/Proxy/ProxyPath.cs';
const workflow = 'frontend/Diten.Web/Controllers/WorkflowController.cs';
const T = 'Diten.Web.Services.Proxy.ProxyPath';
const sabotages = [
  { id: 's1', what: 'the `..` / `.` segment check removed (helper)', file: helper,
    from: '            if (DotSegment(form)) return Reasons.DotSegment;\n', to: '' },
  { id: 's2', what: 'the decode loop reduced to ONE round (helper)', file: helper,
    from: 'for (var round = 1; round <= MaxDecodeRounds; round++)', to: 'for (var round = 1; round <= 1; round++)' },
  { id: 's3', what: 'Workflow taken off the helper (the old `{prefix}/{rest}` concatenation back)', file: workflow,
    from: `        if (!${T}.TryBuild("/api/v1/workflow", relativePath, out var targetPath, out var refused)) return ${T}.Refusal(refused); // WP-MVP1-SEC-PROXY1`,
    to: `        var targetPath = string.IsNullOrWhiteSpace(relativePath)\r\n            ? "/api/v1/workflow"\r\n            : $"/api/v1/workflow/{relativePath.TrimStart('/')}";` },
  { id: 's4', what: 'the `\\` check removed (helper)', file: helper,
    from: "            if (form.Contains('\\\\')) return Reasons.Backslash;\n", to: '' },
];

const sh = (cmd) => { try { return execSync(cmd, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 1 << 28 }); } catch (e) { return (e.stdout || '') + (e.stderr || ''); } };
const sha = (f) => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const build = () => { const o = sh('dotnet build frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -nologo -v q'); if (!/\s0 Hata/.test(o)) throw new Error('build failed\n' + o.slice(-3000)); };
const run = () => {
  const o = sh('dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj --no-build -nologo --filter "FullyQualifiedName~Diten.Web.Tests.Security"');
  const sum = (o.match(/(Başarılı!|Başarısız!).*?Toplam:\s+\d+/) || ['?'])[0];
  const failed = [...o.matchAll(/^\s+Başarısız (\S+?)(\(|\s\[)/gm)].map(m => m[1].replace('Diten.Web.Tests.Security.', ''));
  const groups = {};
  for (const f of failed) groups[f] = (groups[f] || 0) + 1;
  return { sum, failedCount: failed.length, groups };
};

const diffBefore = sh('git diff');
const shaBefore = { [helper]: sha(helper), [workflow]: sha(workflow) };
for (const s of sabotages) {
  const original = fs.readFileSync(s.file, 'utf8');
  if (original.split(s.from).length !== 2) throw new Error(`${s.id}: anchor not found exactly once in ${s.file}`);
  fs.writeFileSync(s.file + '.sabotage-bak', original);
  fs.writeFileSync(s.file, original.replace(s.from, () => s.to));
  let red;
  try { build(); red = run(); }
  finally {
    fs.writeFileSync(s.file, original);
    const now = new Date(); fs.utimesSync(s.file, now, now);
    fs.unlinkSync(s.file + '.sabotage-bak');
  }
  build();
  const green = run();
  console.log(`\n### ${s.id} — ${s.what}`);
  console.log(`red:      ${red.sum}`);
  for (const [k, v] of Object.entries(red.groups)) console.log(`   ${v} × ${k}`);
  console.log(`restored: ${green.sum}`);
}
const diffAfter = sh('git diff');
console.log(`\nSHA-256 helper/workflow unchanged: ${sha(helper) === shaBefore[helper] && sha(workflow) === shaBefore[workflow]}`);
console.log(`git diff identical before/after the loop: ${diffBefore === diffAfter}`);
console.log(`*.sabotage-bak left: ${fs.existsSync(helper + '.sabotage-bak') || fs.existsSync(workflow + '.sabotage-bak')}`);
```

```text

### s1 — the `..` / `.` segment check removed (helper)
red:      Başarısız! - Başarısız:    95, Başarılı:   636, Atlanan:     0, Toplam:   731
   6 × ProxyPathTests.An_unsafe_rest_is_refused_with_its_reason
   88 × ProxyBoundaryTests.An_attack_path_is_refused_400_and_never_reaches_the_gateway
   1 × ProxyPathTests.The_decode_loop_peels_up_to_the_bound_and_refuses_beyond_it
restored: Başarılı!  - Başarısız:     0, Başarılı:   731, Atlanan:     0, Toplam:   731

### s2 — the decode loop reduced to ONE round (helper)
red:      Başarısız! - Başarısız:    57, Başarılı:   674, Atlanan:     0, Toplam:   731
   1 × ProxyPathTests.An_unsafe_rest_is_refused_with_its_reason
   1 × ProxyPathTests.The_decode_loop_peels_up_to_the_bound_and_refuses_beyond_it
   55 × ProxyBoundaryTests.An_attack_path_is_refused_400_and_never_reaches_the_gateway
restored: Başarılı!  - Başarısız:     0, Başarılı:   731, Atlanan:     0, Toplam:   731

### s3 — Workflow taken off the helper (the old `{prefix}/{rest}` concatenation back)
red:      Başarısız! - Başarısız:    97, Başarılı:   634, Atlanan:     0, Toplam:   731
   96 × ProxyBoundaryTests.An_attack_path_is_refused_400_and_never_reaches_the_gateway
   1 × ProxyPathArchitectureGuardTests.Every_catch_all_action_builds_its_gateway_path_through_ProxyPath_TryBuild
restored: Başarılı!  - Başarısız:     0, Başarılı:   731, Atlanan:     0, Toplam:   731

### s4 — the `\` check removed (helper)
red:      Başarısız! - Başarısız:    48, Başarılı:   683, Atlanan:     0, Toplam:   731
   4 × ProxyPathTests.An_unsafe_rest_is_refused_with_its_reason
   44 × ProxyBoundaryTests.An_attack_path_is_refused_400_and_never_reaches_the_gateway
restored: Başarılı!  - Başarısız:     0, Başarılı:   731, Atlanan:     0, Toplam:   731

SHA-256 helper/workflow unchanged: true
git diff identical before/after the loop: true
*.sabotage-bak left: false
```
