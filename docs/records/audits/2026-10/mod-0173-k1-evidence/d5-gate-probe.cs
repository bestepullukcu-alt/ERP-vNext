// WP-0173-K1 D5 — FU-S2-01 startup gate, measured on the REAL service process (built Diten.InventoryService.Api.dll).
//   (a) test database (DB-010 name diten_inventory_s1_tests) + one unscoped AVAILABLE balance row → WARNING with the
//       count, the service keeps starting and listens. The probe's own marker row is removed afterwards (by _id only).
//   (b) separate temporary NON-test database diten_inventory_k1_gate_probe (must not exist before; the probe refuses
//       to touch it otherwise) + one unscoped row → LogCritical, the process exits non-zero, the row is still there.
//       The probe then drops ONLY that database, which it created.
// Usage: dotnet run -- <service bin dir> <evidence dir>. The JWT secret is minted per run and never written out.
using System.Diagnostics;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

var serviceBin = args[0];
var evidence = args[1];
const string Mongo = "mongodb://127.0.0.1:27017";
const string TestDb = "diten_inventory_s1_tests";
const string TempDb = "diten_inventory_k1_gate_probe";
const int Port = 5091;
var client = new MongoClient(Mongo);
var result = new Dictionary<string, object?>();

BsonDocument Unscoped() => new()
{
    { "_id", "k1-gate-probe-" + Guid.NewGuid().ToString("N") },
    { "ItemId", Guid.NewGuid().ToString() }, { "SkuId", Guid.NewGuid().ToString() }, { "SkuLevel", "Gsku" },
    { "StockStatus", "Available" }, { "OnHand", new BsonDecimal128(1m) }, { "IsDeleted", false }
};

async Task<(int? ExitCode, List<string> Lines, int Pid)> RunService(string database, string logName, Func<List<string>, bool> done, int seconds)
{
    var psi = new ProcessStartInfo("dotnet", $"\"{Path.Combine(serviceBin, "Diten.InventoryService.Api.dll")}\"")
    {
        WorkingDirectory = serviceBin, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
    };
    psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Mock";
    psi.Environment["Mongo__DatabaseName"] = database;
    psi.Environment["JwtSettings__Secret"] = "k1-gate-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    psi.Environment["Kestrel__Endpoints__Http__Url"] = $"http://localhost:{Port}";
    psi.Environment["Inventory__Reservations__SweeperEnabled"] = "false";
    var lines = new List<string>();
    using var p = Process.Start(psi)!;
    p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
    p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    var deadline = DateTime.UtcNow.AddSeconds(seconds);
    while (DateTime.UtcNow < deadline && !p.HasExited)
    {
        lock (lines) if (done(lines)) break;
        await Task.Delay(250);
    }

    int? exit = null;
    if (p.HasExited) { p.WaitForExit(); exit = p.ExitCode; }
    else { p.Kill(entireProcessTree: true); p.WaitForExit(); }
    File.WriteAllLines(Path.Combine(evidence, logName), lines);
    return (exit, lines, p.Id);
}

int Count(List<string> lines, string text) => lines.Count(l => l.Contains(text, StringComparison.Ordinal));

// ── (a) test database ───────────────────────────────────────────────────────────────────────────────────
var testBalances = client.GetDatabase(TestDb).GetCollection<BsonDocument>("inventory_balances");
var marker = Unscoped();
await testBalances.InsertOneAsync(marker);
try
{
    var a = await RunService(TestDb, "d5a-service-testdb.log", l => l.Any(x => x.Contains("Now listening")), 60);
    result["a"] = new Dictionary<string, object?>
    {
        ["database"] = TestDb,
        ["serviceExitedOnItsOwn"] = a.ExitCode.HasValue,
        ["warningLines"] = a.Lines.Where(x => x.Contains("never guessed, never deleted")).ToList(),
        ["warnLevelHeaders"] = Count(a.Lines, "warn: Diten.InventoryService.Persistence.InventoryIndexInitializerHostedService"),
        ["criticalLines"] = Count(a.Lines, "crit:"),
        ["listening"] = a.Lines.Any(x => x.Contains("Now listening")),
        ["pid"] = a.Pid
    };
}
finally
{
    var deleted = await testBalances.DeleteOneAsync(new BsonDocument("_id", marker["_id"]));
    result["a_markerRemoved"] = deleted.DeletedCount;
}

// ── (b) temporary non-test database ─────────────────────────────────────────────────────────────────────
var before = (await client.ListDatabaseNamesAsync()).ToList();
if (before.Contains(TempDb))
{
    throw new InvalidOperationException($"{TempDb} already exists — not created by this probe; refusing to touch it.");
}

var tempBalances = client.GetDatabase(TempDb).GetCollection<BsonDocument>("inventory_balances");
await tempBalances.InsertOneAsync(Unscoped());
var b = await RunService(TempDb, "d5b-service-nontestdb.log", _ => false, 60);
var survived = await tempBalances.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
result["b"] = new Dictionary<string, object?>
{
    ["database"] = TempDb,
    ["existedBeforeProbe"] = false,
    ["serviceExitedOnItsOwn"] = b.ExitCode.HasValue,
    ["exitCode"] = b.ExitCode,
    ["warningLines"] = b.Lines.Where(x => x.Contains("never guessed, never deleted")).ToList(),
    ["criticalHeaders"] = Count(b.Lines, "crit: Diten.InventoryService.Persistence.InventoryIndexInitializerHostedService"),
    ["criticalLines"] = b.Lines.Where(x => x.Contains("refusing to start") || x.Contains("unscoped SKU-position group")).ToList(),
    ["listening"] = b.Lines.Any(x => x.Contains("Now listening")),
    ["unscopedRowsStillPresentAfterRefusal"] = survived,
    ["pid"] = b.Pid
};
await client.DropDatabaseAsync(TempDb);
result["b_tempDatabaseDropped"] = !(await client.ListDatabaseNamesAsync()).ToList().Contains(TempDb);
result["remainingServiceProcesses"] = Process.GetProcessesByName("dotnet").Select(p => p.Id).Where(id => id == b.Pid).ToList();

var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(evidence, "d5-gate-probe.json"), json);
Console.WriteLine(json);
