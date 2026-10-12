# WP-MVP1-SYNC1 — betikler (2026-10-12)

> Kanıt: `mvp1-sync1-evidence-2026-10-12.md`. Tek seferlik kanıt betikleri, repo kodu değil (scratchpad).
>
> **Yığın.** `stack/Program.cs` = Q5 yığını (`mod-0175-q5-scripts-2026-10-09.md`) + `adapt.js` + AuthService için
> `DevSeeds__MockUsers=true`. `pm-stub.js` = PM1 betik belgesi §2 (değişmeden). Çalıştırma:
> `dotnet run -- <worktree> <çıktı> <pm-stub.js>`; durdurma: `<çıktı>/stop` (yalnız kendi süreçleri; `pw.txt` silinir).
>
> **Prob.** `node probe.js <çıktı> holds | decide 1 b | item 1 b | nosig 1 b | state 1 wait Applied | decide 2 ab | item 2 a`;
> `node wfself.js <çıktı>`. Tarayıcı adımı (B, WorkCenter onayı + imza) `nosig` ile `state` arasındadır.
>
> Şifre ve sırlar hiçbir betikte yok; yığın onları rastgele üretir.

## runtests.sh

Test koşucusu. `LANE=1`: hat mongod değişkenleri; `SHIM=1`: ara katman (yalnız ilk denemede).

```bash
#!/bin/bash
# usage: runtests.sh <worktree> <outdir> [suite...]
W=$1; O=$2; shift 2; mkdir -p "$O"
export DITEN_TEST_MONGOD="C:\Program Files\MongoDB\Server\9.0\bin\mongod.exe"
export DITEN_ITEST_MONGOD_BIN_DIR="C:\Program Files\MongoDB\Server\9.0\bin"
unset INVENTORY_TEST_MONGO
if [ "${SHIM:-0}" = "1" ]; then export DITEN_TEST_MONGOD="$(cygpath -w C:/Users/Ali/AppData/Local/Temp/claude/C--Users-Ali-erp-vnext/1840dfcc-2d32-460a-be5f-3b50272f8297/scratchpad/mongod-shim/bin/Release/net8.0/mongod.exe)"; fi
if [ "${SHIM:-0}" = "1" ] || [ "${LANE:-0}" = "1" ]; then
  # the lane mongod (scripts/test-env/mvp6-test-mongo-env.sh shape, slot 1, own)
  L=$(cat C:/Users/Ali/AppData/Local/Temp/claude/C--Users-Ali-erp-vnext/1840dfcc-2d32-460a-be5f-3b50272f8297/scratchpad/lanedb/ready)
  for v in AUTH_COMPOSITION_GUARD_MONGO_URI DITEN_PLATFORM_TEST_MONGO_URI MVP6_MOD0192_MONGO_URI MOD0183_TEST_MONGO MOD0184_TEST_MONGO MOD0185_TEST_MONGO MVP6_MOD0190_MONGO_URI RETURNS_MONGO_URI CLAIMS_TEST_MONGO; do export $v="$L"; done
fi
PF='FullyQualifiedName~ESign|FullyQualifiedName~Workflow|FullyQualifiedName~WorkAggregation|FullyQualifiedName~WorkCenter|FullyQualifiedName~ModuleRegistration|FullyQualifiedName~ModulePages|FullyQualifiedName~TimeEntry|FullyQualifiedName~Timesheet'
SUITES=${@:-platform inventory web gateway auth crm arch supplychain}
for s in $SUITES; do
  F=""
  case $s in
    platform) P=services/Diten.Platform/tests/Diten.Platform.Application.Tests; F="$PF";;
    inventory) P=services/Diten.InventoryService/tests/Diten.InventoryService.Tests;;
    web) P=frontend/Diten.Web.Tests;;
    gateway) P=gateway/Diten.ApiGateway.Tests;;
    auth) P=services/Diten.AuthService/tests/Diten.AuthService.Application.Tests;;
    crm) P=services/Diten.CrmService/tests/Diten.CrmService.Application.Tests;;
    arch) P=tests/architecture/TenantArchitecture.ArchitectureTests;;
    supplychain) P=services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests;;
  esac
  if [ ! -d "$W/$P" ]; then echo "$s: N/A (no project)" >> "$O/summary.txt"; continue; fi
  start=$(date +%s)
  if [ -n "$F" ]; then
    dotnet test "$W/$P" -c Release -nologo --filter "$F" --logger "trx;LogFileName=$s.trx" --results-directory "$O" > "$O/$s.log" 2>&1
  else
    dotnet test "$W/$P" -c Release -nologo --logger "trx;LogFileName=$s.trx" --results-directory "$O" > "$O/$s.log" 2>&1
  fi
  rc=$?
  sum=$(grep -aE "(Passed!|Failed!|Başarılı!|Başarısız!)" "$O/$s.log" | tr '\n' ' ')
  echo "$s: rc=$rc $(( $(date +%s)-start ))s $sum" >> "$O/summary.txt"
done
echo DONE >> "$O/summary.txt"
```

## buildall.sh

Derleme ve uyarı sayımı (Release, --no-incremental).

```bash
#!/bin/bash
# usage: buildall.sh <worktree> <outdir> — Release, no-incremental; one line per project: errors / warnings
W=$1; O=$2; mkdir -p "$O"; : > "$O/build-summary.txt"
cd "$W"
P=$(git ls-files "services/*/src/*.Api/*.Api.csproj" "services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj" "frontend/Diten.Web/Diten.Web.csproj" "gateway/Diten.ApiGateway/Diten.ApiGateway.csproj" \
  "services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj" \
  "services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Diten.InventoryService.Tests.csproj" \
  "frontend/Diten.Web.Tests/Diten.Web.Tests.csproj" "gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj" \
  "services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj" \
  "services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj" \
  "services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj" \
  "tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj" | sort -u)
for p in $P; do
  n=$(basename $p .csproj)
  dotnet build "$p" -c Release -nologo --no-incremental > "$O/build-$n.log" 2>&1
  rc=$?
  w=$(grep -aE "[0-9]+ (Uyarı|Warning)" "$O/build-$n.log" | tail -1 | grep -oE "[0-9]+")
  e=$(grep -aE "[0-9]+ (Hata|Error)" "$O/build-$n.log" | tail -1 | grep -oE "[0-9]+")
  echo "$n rc=$rc errors=${e:-?} warnings=${w:-?}" >> "$O/build-summary.txt"
done
echo DONE >> "$O/build-summary.txt"
```

## resolve.js

diff3 parçalarını tek tek çözer: ours / theirs / ot / to / @dosya.

```js
// usage: node resolve.js <file> <spec>   spec = comma list, one per hunk in order: ours | theirs | ot (ours then theirs) | to | @path (replacement text file)
// diff3-style markers (<<<<<<< ours / ||||||| base / ======= / >>>>>>> theirs). Keeps the file's line endings.
const fs = require('fs');
const [file, spec] = process.argv.slice(2);
const specs = spec.split(',');
let s = fs.readFileSync(file, 'utf8');
const eol = s.includes('\r\n') ? '\r\n' : '\n';
const lines = s.split(/\r?\n/);
const out = [];
let i = 0, h = 0;
while (i < lines.length) {
  if (!lines[i].startsWith('<<<<<<< ')) { out.push(lines[i++]); continue; }
  const ours = [], base = [], theirs = [];
  let cur = ours; i++;
  while (!lines[i].startsWith('>>>>>>> ')) {
    if (lines[i].startsWith('||||||| ')) cur = base;
    else if (lines[i] === '=======') cur = theirs;
    else cur.push(lines[i]);
    i++;
  }
  i++;
  const sp = specs[h++];
  if (sp === undefined) throw new Error('no spec for hunk ' + h);
  if (sp === 'ours') out.push(...ours);
  else if (sp === 'theirs') out.push(...theirs);
  else if (sp === 'ot') out.push(...ours, ...theirs);
  else if (sp === 'to') out.push(...theirs, ...ours);
  else if (sp.startsWith('@')) out.push(...fs.readFileSync(sp.slice(1), 'utf8').replace(/\r?\n$/, '').split(/\r?\n/));
  else throw new Error('bad spec ' + sp);
}
if (h !== specs.length) throw new Error(`hunks ${h} != specs ${specs.length}`);
fs.writeFileSync(file, out.join(eol));
console.log(`${file}: ${h} hunk(s) resolved`);
```

## resx.js

resx anahtar birleşimi; aynı anahtar farklı metinse main kalır ve raporlanır.

```js
// Resolves resx conflict hunks as a KEY UNION: every <data> of main ("theirs") in its order, then every key of ours main
// does not have. Same key with a different <data> block → main's kept, ours reported. Also reports keys duplicated across
// the whole file after resolution (a resx with a duplicate key fails at build).
// usage: node resx.js <file...>
const fs = require('fs');
const report = [];
for (const file of process.argv.slice(2)) {
  const raw = fs.readFileSync(file, 'utf8');
  const eol = raw.includes('\r\n') ? '\r\n' : '\n';
  const lines = raw.split(/\r?\n/);
  const out = [];
  let i = 0, hunks = 0;
  const blocks = (ls) => {
    const res = []; let cur = null;
    for (const l of ls) {
      const m = l.match(/<data name="([^"]+)"/);
      if (m) { cur = { name: m[1], lines: [l] }; res.push(cur); if (/<\/data>/.test(l)) cur = null; continue; }
      if (cur) { cur.lines.push(l); if (/<\/data>/.test(l)) cur = null; continue; }
      if (l.trim() !== '') res.push({ name: null, lines: [l] }); // comments etc.
    }
    return res;
  };
  while (i < lines.length) {
    if (!lines[i].startsWith('<<<<<<< ')) { out.push(lines[i++]); continue; }
    const ours = [], base = [], theirs = []; let cur = ours; i++;
    while (!lines[i].startsWith('>>>>>>> ')) {
      if (lines[i].startsWith('||||||| ')) cur = base; else if (lines[i] === '=======') cur = theirs; else cur.push(lines[i]);
      i++;
    }
    i++; hunks++;
    const t = blocks(theirs), o = blocks(ours);
    const tm = new Map(t.filter((b) => b.name).map((b) => [b.name, b]));
    for (const b of t) out.push(...b.lines);
    for (const b of o) {
      if (!b.name) { if (!t.some((x) => !x.name && x.lines.join() === b.lines.join())) out.push(...b.lines); continue; }
      const m = tm.get(b.name);
      if (!m) out.push(...b.lines);
      else if (m.lines.map((l) => l.trim()).join('') !== b.lines.map((l) => l.trim()).join('')) report.push({ file, key: b.name, ours: b.lines.join(' ').trim(), main: m.lines.join(' ').trim() });
    }
  }
  const text = out.join(eol);
  const names = [...text.matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]);
  const dup = names.filter((n, k) => names.indexOf(n) !== k);
  fs.writeFileSync(file, text);
  console.log(`${file}: ${hunks} hunk(s), ${names.length} keys, duplicates: ${dup.length ? dup.join(',') : 'none'}`);
}
console.log(JSON.stringify(report, null, 1));
```

## sabotage.js

K3 sabotajları (a)–(e); `ONLY=e` tek sabotaj.

```js
// WP-MVP1-SYNC1 K3 sabotages on the MERGED code: each one edit to production code, the named tests must turn red, then
// `git checkout -- <file>` restores it and `git status --porcelain` must equal the pre-run porcelain (the merge is uncommitted
// while this runs, so "restored" = the file's diff against the index is empty again).
// usage: node sabotage.js <worktree> <out>   (env: the shim / lane variables of runtests.sh SHIM=1)
const fs = require('fs');
const { execFileSync, spawnSync } = require('child_process');
const [W, out] = process.argv.slice(2);
const PT = 'services/Diten.Platform/tests/Diten.Platform.Application.Tests';
const SUP = 'services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs';
const BIND = 'services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowActorBinding.cs';
const MAP = 'services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/WorkflowInstanceBsonMap.cs';
const cases = [
  { id: 'a', what: 'the E4 signature gate call removed from the approve path', file: SUP,
    a: 'if (action == WorkflowTransitionAction.Approve && task.SignatureRequired)', b: 'if (false && action == WorkflowTransitionAction.Approve && task.SignatureRequired)',
    filter: 'FullyQualifiedName~WorkflowESignatureGateTests' },
  { id: 'b', what: 'actor binding removed — the body ActorId is trusted', file: BIND,
    a: '        if (caller.UserId == Guid.Empty)\n', b: '        if (!string.IsNullOrWhiteSpace(bodyActorId)) return (bodyActorId.Trim(), null);\n        if (caller.UserId == Guid.Empty)\n',
    filter: 'FullyQualifiedName~WorkflowActorBindingTests|FullyQualifiedName~Naming_someone_else_as_the_actor_is_403_and_changes_nothing' },
  { id: 'c', what: 'the refusal of a delegation to the starter removed', file: SUP,
    a: 'if (IsStarter(instance, delegatePrincipalId))', b: 'if (false && IsStarter(instance, delegatePrincipalId))',
    filter: 'FullyQualifiedName~Delegate_to_the_starter_is_refused' },
  { id: 'd', what: 'SoD compares the e-mail (StartedBy) instead of the user id for a NEW instance', file: SUP,
    a: 'return Guid.TryParse(actorId, out var actor) && actor == starter;', b: 'return string.Equals(instance.StartedBy, actorId, StringComparison.OrdinalIgnoreCase);',
    filter: 'FullyQualifiedName~Fx_a_tenant_user_who_started_the_workflow_cannot_approve_it|FullyQualifiedName~Whoever_started_an_instance_cannot_approve_it|FullyQualifiedName~Sync1_an_instance_whose_starter_id_was_written_as_text' },
  { id: 'e', what: '(extra) the tolerant StartedByUserId serializer removed', file: MAP,
    a: '            map.MapMember(instance => instance.StartedByUserId).SetSerializer(new LenientNullableGuidSerializer());\n', b: '',
    filter: 'FullyQualifiedName~Sync1_' }
];
const git = (...a) => execFileSync('git', ['-C', W, ...a], { encoding: 'utf8' });
const before = git('status', '--porcelain');
const results = [];
for (const c of cases.filter((x) => !process.env.ONLY || process.env.ONLY.split(",").includes(x.id))) {
  const path = `${W}/${c.file}`;
  const raw = fs.readFileSync(path, 'utf8');
  const eol = raw.includes('\r\n') ? '\r\n' : '\n';
  const A = c.a.replace(/\n/g, eol), B = c.b.replace(/\n/g, eol);
  if (raw.split(A).length !== 2) throw new Error(`${c.id}: anchor`);
  const original = raw;
  fs.writeFileSync(path, raw.replace(A, () => B));
  const run = spawnSync('dotnet', ['test', `${W}/${PT}`, '-c', 'Release', '-nologo', '--filter', c.filter], { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  const log = `${run.stdout}\n${run.stderr}`;
  fs.writeFileSync(`${out}/sabotage-${c.id}.txt`, log);
  const summary = (log.match(/(Başarısız!|Başarılı!|Failed!|Passed!)[^\n]*/) || ['(no summary)'])[0];
  const failed = [...log.matchAll(/^\s+(?:Başarısız|Failed) (Diten\.[^\s]+)/gm)].map((m) => m[1].split('.').pop());
  fs.writeFileSync(path, original); // restore byte for byte (the merge is not committed yet, so `git checkout --` would drop it)
  const restored = git('status', '--porcelain') === before && fs.readFileSync(path, 'utf8') === original;
  const red = /Başarısız!|Failed!/.test(summary) && !/error CS/.test(log);
  results.push({ id: c.id, what: c.what, summary: summary.trim(), red, failedTests: [...new Set(failed)], restoredClean: restored });
  console.log(`${c.id}: ${red ? 'RED' : 'NOT RED'} — ${summary.trim()} — ${[...new Set(failed)].join(', ')} — restored ${restored ? 'clean' : 'DIRTY'}`);
}
fs.writeFileSync(`${out}/sabotage-results${process.env.ONLY ? "-" + process.env.ONLY : ""}.json`, JSON.stringify(results, null, 2));
```

## lane/Program.cs

Hat mongod (slot 1, 31994, tek üyeli replica set, enableTestCommands=1); iş nesnesiyle birlikte kapanır.

```csharp
// WP-MVP1-SYNC1 lane mongod (scratch, not repo code): main's Platform / SupplyChain tests read DITEN_PLATFORM_TEST_MONGO_URI & co.
// from a lane-owned single-member replica set (scripts/test-env/mvp6-test-mongo-env.sh: port 30994 + 1000*slot, enableTestCommands=1,
// loopback, no credentials). This starts exactly that on slot 1 (31994, never 27017), initiates the set, writes <dir>/ready and waits
// for <dir>/stop; the mongod runs in a Job Object, so it dies with this process. usage: lane <dir> <port> <rs>
using System.Diagnostics;
using System.Runtime.InteropServices;
using MongoDB.Bson;
using MongoDB.Driver;

var dir = args[0]; var port = int.Parse(args[1]); var rs = args[2];
if (port is >= 27017 and <= 27021) throw new InvalidOperationException("refusing the operational band");
var data = Path.Combine(dir, "data"); Directory.CreateDirectory(data);
var psi = new ProcessStartInfo(@"C:\Program Files\MongoDB\Server\9.0\bin\mongod.exe") { UseShellExecute = false, RedirectStandardOutput = true };
foreach (var a in new[] { "--dbpath", data, "--port", port.ToString(), "--bind_ip", "127.0.0.1", "--replSet", rs, "--setParameter", "enableTestCommands=1" }) psi.ArgumentList.Add(a);
var job = CreateJobObject(IntPtr.Zero, null);
var info = new byte[144]; BitConverter.GetBytes(0x2000u).CopyTo(info, 16); // JOBOBJECT_EXTENDED_LIMIT_INFORMATION.BasicLimitInformation.LimitFlags (x64 offset 16)
var h = GCHandle.Alloc(info, GCHandleType.Pinned);
if (!SetInformationJobObject(job, 9, h.AddrOfPinnedObject(), (uint)info.Length)) throw new InvalidOperationException("job");
var p = Process.Start(psi)!; AssignProcessToJobObject(job, p.Handle);
p.OutputDataReceived += (_, _) => { }; p.BeginOutputReadLine();
File.WriteAllText(Path.Combine(dir, "pid.txt"), $"lane {Environment.ProcessId} mongod {p.Id}");
var client = new MongoClient($"mongodb://127.0.0.1:{port}/?directConnection=true");
for (var i = 0; ; i++)
{
    try { await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); break; }
    catch when (i < 120) { await Task.Delay(500); }
}
try
{
    await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate",
        new BsonDocument { { "_id", rs }, { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{port}" } } } } }));
}
catch (MongoCommandException e) when (e.Code == 23) { } // already initialized
var rsClient = new MongoClient($"mongodb://127.0.0.1:{port}/?replicaSet={rs}");
for (var i = 0; ; i++)
{
    try { await rsClient.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); break; }
    catch when (i < 120) { await Task.Delay(500); }
}
File.WriteAllText(Path.Combine(dir, "ready"), $"mongodb://127.0.0.1:{port}/?replicaSet={rs}&serverSelectionTimeoutMS=15000");
Console.WriteLine("READY");
while (!File.Exists(Path.Combine(dir, "stop"))) await Task.Delay(2000);
p.Kill(); p.WaitForExit(10000);
Console.WriteLine("stopped");

[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr a, string? name);
[DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int type, IntPtr info, uint length);
[DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
```

## lane/lane.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MongoDB.Driver" Version="2.27.0" />
  </ItemGroup>
</Project>
```

## mongod-shim/Program.cs

İlk deneme: --nounixsocket süzen ara katman (temizlik hataları yüzünden bırakıldı; kanıt §6.3).

```csharp
// WP-MVP1-SYNC1 test-environment shim (scratch, not repo code). main's DisposableStandaloneMongo passes `--nounixsocket`, which
// the Windows mongod 9.0 refuses ("unrecognised option", exit 2). This shim drops that one argument and runs the real mongod as
// a child inside a Job Object with KILL_ON_JOB_CLOSE, so when the test harness kills the shim the mongod dies with it (no orphan).
// Every other argument is passed through unchanged; the exit code is the mongod's.
using System.Diagnostics;
using System.Runtime.InteropServices;

const string RealMongod = @"C:\Program Files\MongoDB\Server\9.0\bin\mongod.exe";
var psi = new ProcessStartInfo(RealMongod) { UseShellExecute = false };
foreach (var a in args.Where(a => a != "--nounixsocket")) psi.ArgumentList.Add(a);

var job = Native.CreateJobObject(IntPtr.Zero, null);
var info = new Native.JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = new() { LimitFlags = 0x2000 } };
var size = Marshal.SizeOf(info);
var ptr = Marshal.AllocHGlobal(size);
Marshal.StructureToPtr(info, ptr, false);
if (!Native.SetInformationJobObject(job, 9, ptr, (uint)size)) throw new InvalidOperationException("job");
using var child = Process.Start(psi)!;
Native.AssignProcessToJobObject(job, child.Handle);
child.WaitForExit();
return child.ExitCode;

static class Native
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateJobObject(IntPtr a, string? name);
    [DllImport("kernel32.dll")] public static extern bool SetInformationJobObject(IntPtr job, int type, IntPtr info, uint length);
    [DllImport("kernel32.dll")] public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit; public long PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass; public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS { public ulong a, b, c, d, e, f; }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit; public UIntPtr PeakProcessMemoryUsed; public UIntPtr PeakJobMemoryUsed;
    }
}
```

## stack/adapt.js

Q5 yığınını (mod-0175-q5-scripts-2026-10-09.md, Program.cs) SYNC1 yığınına çevirir. Sonra elle bir satır eklendi: `authEnv["DevSeeds__MockUsers"] = "true";` (main örnek kullanıcıları isteğe bağlı tohumluyor).

```js
// turns the Q5 stack (mod-0175-q5-scripts-2026-10-09.md, Program.cs) into the SYNC1 one (exact replacements, each anchor checked)
const fs = require('fs');
const p = __dirname + '/Program.cs';
let s = fs.readFileSync(p, 'utf8');
const rep = (a, b, all) => {
  const n = s.split(a).length - 1;
  if (all ? n < 1 : n !== 1) throw new Error(`anchor x${n}: ${a.slice(0, 90)}`);
  s = all ? s.split(a).join(b) : s.replace(a, () => b);
};

rep('// WP-0175-Q5 runtime stack (WP-0023-FX method).', '// WP-MVP1-SYNC1 runtime stack (from the WP-0175-Q5 stack; merged tree; InventoryService behind ocelot port 5067 (OD-P1); PRODUCT-MASTER = the OWN pm-stub.js of WP-0173-PM1 on 14036, not Prism).');
rep('itest_q5_', 'itest_sync1_', true);
rep('var testPassword = "Q5!"', 'var testPassword = "Sy1!"');
// OD-P1: the merged ocelot.json sends our four route families to 5067; main's /api/shipment-bundle stays on 5066 (→ dead port here)
rep('var map = new Dictionary<int, int> { [5057] = 15057, [5066] = 15176, [5056] = 15056, [5059] = 14037 };',
    'var map = new Dictionary<int, int> { [5057] = 15057, [5067] = 15176, [5056] = 15056, [5059] = 14037 };');
// PRODUCT-MASTER: own pm-stub with a one-item world
const prismStart = s.indexOf('    var prism = Directory.GetFiles(');
const prismEnd = s.indexOf('    var stub = Path.Combine(outDir, "le-stub.js");');
if (prismStart < 0 || prismEnd < prismStart) throw new Error('prism');
s = s.slice(0, prismStart) + fs.readFileSync(__dirname + '/pm-block.cs.txt', 'utf8') + s.slice(prismEnd);
rep('await WaitUp("http://127.0.0.1:14036/products/" + Item, "prism");', 'await WaitUp("http://127.0.0.1:14036/products/" + Item, "pm-stub");');
rep('var roleName = "q5-qa-"', 'var roleName = "sync1-qa-"');
rep('new { name = roleName, displayName = "Q5 QA", description = "WP-0175-Q5 stack" }', 'new { name = roleName, displayName = "SYNC1 QA", description = "WP-MVP1-SYNC1 stack" }');
rep('var cEmail = $"q5.noaccess.', 'var cEmail = $"sync1.noaccess.');
rep('string K(string p) => $"q5s-{run}-{p}-{Guid.NewGuid():N}"[..40];', 'string K(string p) => $"sy1-{run}-{p}-{Guid.NewGuid():N}"[..40];');
// the auth.users.lookup key for the people names (as FUB2)
rep('"esign.signatures.sign", "esign.signatures.read", "esign.signatures.verify", "esign.my-samples.manage"\n',
    '"esign.signatures.sign", "esign.signatures.read", "esign.signatures.verify", "esign.my-samples.manage",\n        "auth.users.lookup", "platform.workflow.tasks.delegate"\n');
// two holds: hold 1 = B approves through WorkCenter (signature), hold 2 = the starter's own approval (A is the only candidate)
rep('    var holdLot = await Lot("Q5-KRN-" + run, future, release: true);       // Karar Ver (partial release)\n    await Receive(holdLot, "1200", "QUARANTINE");\n',
    '    var holdLot = await Lot("SY1-KRN-" + run, future, release: true);      // hold 1: B approves in WorkCenter with a signature\n    await Receive(holdLot, "1200", "QUARANTINE");\n'
  + '    var holdLot2 = await Lot("SY1-KRN2-" + run, future, release: true);    // hold 2: A (the starter) is the only candidate\n    await Receive(holdLot2, "300", "QUARANTINE");\n');
const cut0 = s.indexOf('    var blockLot = await Lot("Q5-BLK-"');
const cut1 = s.indexOf('    await Settings(new { lotReleaseRequiresApproval = true });');
if (cut0 < 0 || cut1 < cut0) throw new Error('cut');
s = s.slice(0, cut0) + s.slice(cut1);
rep('["holdLot"] = holdLot, ["blockLot"] = blockLot, ["retestLot"] = retestLot, ["draftLot"] = draftLot, ["run"] = run,',
    '["holdLot"] = holdLot, ["holdLot2"] = holdLot2, ["item"] = Item, ["sku"] = Sku, ["run"] = run,');
fs.writeFileSync(p, s);
console.log('adapted');
```

## stack/pm-block.cs.txt

FUB2 betik belgesindeki pm-block (değişmeden).

```csharp
    var world = Path.Combine(outDir, "pm-world.json");
    File.WriteAllText(world, $$"""
        { "products": { "{{Item}}": { "itemId": "{{Item}}", "itemKind": "GlobalProduct", "canonicalCode": "GP-000000000001", "name": "Amoksisilin 500mg Kapsül", "lifecycleStatus": "Active", "lotControlled": true, "serialControlled": true } },
          "skus": { "{{Sku}}": { "skuId": "{{Sku}}", "skuLevel": "Lsku", "itemId": "{{Item}}", "itemKind": "GlobalProduct", "canonicalCode": "LSKU-TR-000000000002", "name": "Amoksisilin 500mg 16 Kapsül (TR)", "lifecycleStatus": "Active", "baseUomId": "EA", "lotControlled": true, "serialControlled": false, "stockable": true, "conversions": [] } } }
        """);
    Start("pm-stub", "node", $"--max-http-header-size=65536 \"{args[2]}\" \"{world}\"", outDir, new()); // the OWN PM1 stub on 14036
```

## stack/probe.js

API probu (H0, D, I, NOSIG, S).

```js
// WP-MVP1-SYNC1 runtime probe on the OWN stack only (stack/Program.cs: own mongod, AuthService, Platform, Gateway, InventoryService,
// Web, pm-stub 14036, LE stub 14037). Tokens from this stack's AuthService login; sign tickets from this stack's esign/reauth with the
// test password (<out>/pw.txt, never printed).
// usage: node probe.js <out> <phase> [args]
//   holds                 — the two intake holds → <out>/holds.json
//   decide <n> <a|b>      — admin sets the approver candidate (user a|b); A drafts, signs (MOD-0022) and submits a release decision on hold n
//   item <n> <a|b>        — the approval work item of decision n as seen by a|b (WorkCenter projection: approve action + step context)
//   nosig <n> <a|b>       — WorkCenter dispatch approve WITHOUT a signature
//   sig <n> <a|b>         — signs the workflow task (workflow / workflow-task, the step's meaning) and dispatches approve WITH it
//   direct <n> <a|b>      — the direct task API approve (POST /api/v1/workflow/tasks/{id}/approve), with a signature
//   state <n>             — decision + hold state (waits up to 60 s for Applied when asked with "wait")
const fs = require('fs');
const [out, phase, arg, who, extra] = process.argv.slice(2);
const st = JSON.parse(fs.readFileSync(out + '/stack.json', 'utf8'));
const pw = fs.readFileSync(out + '/pw.txt', 'utf8').trim();
const G = st.gateway;
const Q = G + '/api/quarantine';
const resultsFile = out + '/probe-api.json';
const results = fs.existsSync(resultsFile) ? JSON.parse(fs.readFileSync(resultsFile, 'utf8')) : [];
const rec = (id, what, expected, actual, ok) => {
  results.push({ id, phase, what, expected, actual, ok: !!ok, at: new Date().toISOString() });
  fs.writeFileSync(resultsFile, JSON.stringify(results, null, 1));
  console.log(`${ok ? 'PASS' : 'FAIL'} ${id} ${what} — expected ${expected}, got ${actual}`);
};
const png = (() => {
  const zlib = require('zlib');
  const w = 200, h = 60, raw = Buffer.alloc((w * 4 + 1) * h, 255);
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; for (let x = 20; x < 180; x++) if (Math.abs(y - (30 + 15 * Math.sin(x / 15))) < 2) raw.fill(0, y * (w * 4 + 1) + 1 + x * 4, y * (w * 4 + 1) + 1 + x * 4 + 3); }
  const crcT = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; return c >>> 0; });
  const crc = (b) => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]).toString('base64');
})();
const key = (p) => `sy1-${p}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;

async function call(method, url, token, body, extraHeaders = {}) {
  const headers = { 'Content-Type': 'application/json', 'X-Tenant-Id': st.tenant, 'Accept-Language': 'tr', ...extraHeaders };
  if (url.includes('/api/quarantine') || url.includes('/api/inventory')) headers['X-Legal-Entity-Id'] = st.legalEntity;
  if (token) headers.Authorization = 'Bearer ' + token;
  const res = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch (_) { json = null; }
  return { status: res.status, json, text, etag: res.headers.get('etag') };
}
const data = (r) => (r.json && typeof r.json === 'object' && 'data' in r.json && ('isSuccessful' in r.json || 'succeeded' in r.json) ? r.json.data : r.json);
const code = (r) => r.json?.reason_code || r.json?.reasonCode || r.json?.error?.code || r.json?.errorCodes?.[0]?.code || '';
const deep = (n, k) => {
  if (!n || typeof n !== 'object') return undefined;
  if (Array.isArray(n)) { for (const x of n) { const v = deep(x, k); if (v !== undefined) return v; } return undefined; }
  for (const [kk, v] of Object.entries(n)) { if (kk.toLowerCase() === k.toLowerCase() && (typeof v !== 'object' || v === null)) return v; }
  for (const v of Object.values(n)) { const r = deep(v, k); if (r !== undefined) return r; }
  return undefined;
};
async function login(email) {
  const r = await call('POST', st.auth + '/api/tenant-auth/login', null, { email, password: pw, rememberMe: false });
  const m = JSON.stringify(r.json || {}).match(/"accessToken":"([^"]+)"/);
  if (!m) throw new Error(`login ${email}: ${r.status}`);
  return m[1];
}
const userOf = (w) => (w === 'b' ? { email: st.b, id: st.bId } : { email: st.a, id: st.aId });
const ticket = async (token) => data(await call('POST', G + '/api/tenant-auth/esign/reauth', token, { password: pw, intent: 'sign' }))?.ticket;
const decisionsFile = out + '/decisions.json';
const decisions = fs.existsSync(decisionsFile) ? JSON.parse(fs.readFileSync(decisionsFile, 'utf8')) : {};

async function decisionOf(token, n) {
  const d = decisions[n];
  const r = await call('GET', `${Q}/decisions/${d.decisionId}`, token);
  return r.json;
}
async function itemOf(token, n) {
  const dec = await decisionOf(token, n);
  const instanceId = deep(dec?.workflow, 'instanceId') || deep(dec?.workflow, 'workflowInstanceId') || deep(dec, 'workflowInstanceId');
  const mine = await call('GET', G + '/api/v1/work-items/mine?pageSize=100', token);
  const items = data(mine)?.items || data(mine) || [];
  const list = Array.isArray(items) ? items : [];
  const item = list.find((i) => i.source?.objectId === decisions[n].decisionId);
  const inst = instanceId ? await call("GET", `${G}/api/v1/workflow/instances/${instanceId}`, token) : null;
  const stepCode = deep(data(inst || {}), "stepCode") || null;
  return { dec, instanceId, item, stepCode, count: list.length, mineStatus: mine.status };
}

(async () => {
  const a = await login(st.a);
  if (phase === 'holds') {
    const r = await call('GET', `${Q}/holds?page=1&pageSize=50`, a);
    const items = r.json.items;
    const h1 = items.find((h) => h.lot?.lotId === st.holdLot);
    const h2 = items.find((h) => h.lot?.lotId === st.holdLot2);
    fs.writeFileSync(out + '/holds.json', JSON.stringify({ 1: h1.holdId, 2: h2.holdId, n1: h1.holdNumber, n2: h2.holdNumber }));
    rec('H0', 'the two intake holds of the stack', '2 holds', `${h1?.holdNumber} / ${h2?.holdNumber}`, h1 && h2);
  } else if (phase === 'decide') {
    const n = Number(arg);
    const admin = await login(st.admin);
    const cand = userOf(who);
    const g = await call('GET', Q + '/settings', admin);
    const s = await call('PUT', Q + '/settings', admin, { approverCandidates: who === 'ab' ? ['user:' + st.aId, 'user:' + st.bId] : ['user:' + cand.id] }, { 'If-Match': g.etag || 'W/"0"' });
    const holds = JSON.parse(fs.readFileSync(out + '/holds.json', 'utf8'));
    const hold = await call('GET', `${Q}/holds/${holds[n]}`, a);
    const dk = key('dec');
    const draft = await call('POST', `${Q}/decisions`, a, { kind: 'HoldDecision', targetId: holds[n], lines: [{ lineNo: 1, decision: 'Release', quantity: hold.json.quantity }],
      reason: 'SYNC1 probe — analysis complete, release', coaChecked: true, idempotencyKey: dk }, { 'Idempotency-Key': dk });
    const decisionId = draft.json?.decisionId;
    const signed = await call('POST', G + '/api/v1/esign/signatures', a, {
      ticket: await ticket(a), subject: { module: 'quarantine', subjectType: 'quarantine.decision', subjectId: decisionId }, actionCode: 'quarantine.hold.release',
      meaningCode: 'RELEASE', appearance: { type: 'drawn', drawing: { pngBase64: png } }, idempotencyKey: key('sign')
    });
    const sk = key('sub');
    const submitted = await call('POST', `${Q}/decisions/${decisionId}/submit`, a, { signatureId: data(signed)?.id, idempotencyKey: sk }, { 'Idempotency-Key': sk });
    decisions[n] = { decisionId, decisionNumber: draft.json?.decisionNumber, candidate: who };
    fs.writeFileSync(decisionsFile, JSON.stringify(decisions, null, 1));
    rec(`D${n}`, `approver candidate = ${who.toUpperCase()}; A drafts, signs (MOD-0022) and submits a release decision on hold ${n}`, 'settings 200, draft 201, sign 201, submit 202',
      `${s.status}, ${draft.status} ${draft.json?.decisionNumber || code(draft)}, ${signed.status} ${data(signed)?.signatureNumber || code(signed)}, ${submitted.status} ${submitted.json?.state || code(submitted)}`,
      s.status === 200 && draft.status === 201 && signed.status === 201 && submitted.status === 202);
  } else if (phase === 'item') {
    const n = Number(arg);
    const token = await login(userOf(who).email);
    const { instanceId, item, count, mineStatus } = await itemOf(token, n);
    fs.writeFileSync(out + `/item-${n}-${who}.json`, JSON.stringify(item || null, null, 1));
    const approve = item?.actions?.find((x) => x.code === 'approve');
    const reject = item?.actions?.find((x) => x.code === 'reject');
    const summary = item && {
      approve: approve && { enabled: approve.enabled, disabledReasonCode: approve.disabledReasonCode, signatureRequired: approve.signatureRequired,
        signatureMeaning: approve.signatureMeaning, signatureActionCode: approve.signatureActionCode, acceptsNote: approve.acceptsNote },
      reject: reject && { acceptsNote: reject.acceptsNote, requiresReason: reject.requiresReason },
      stepName: item.stepName?.text, title: item.title?.text, tags: item.tags, delegate: item.actions?.find((x) => x.code === 'delegate') && { requiresTargetPerson: item.actions.find((x) => x.code === 'delegate').requiresTargetPerson, excluded: item.actions.find((x) => x.code === 'delegate').excludedTargetPrincipalIds }, candidates: JSON.stringify(item).match(/"candidate[A-Za-z]*":\s*(\[[^\]]*\]|"[^"]*")/g)
    };
    fs.writeFileSync(out + `/item-${n}-${who}-summary.json`, JSON.stringify({ instanceId, summary }, null, 1));
    console.log(JSON.stringify({ mineStatus, count, instanceId, itemId: item?.id, summary }, null, 1));
    if (who === 'b') {
      rec(`I${n}b`, `B's WorkCenter item for decision ${n}: approve says signature required (E4/E5W2) and accepts a note (main WCN-APPROVAL-UX)`,
        'approve enabled, signatureRequired true, meaning set, actionCode workflow.task.approve',
        JSON.stringify(summary?.approve), approve?.enabled === true && approve?.signatureRequired === true && !!approve?.signatureMeaning && approve?.signatureActionCode === 'workflow.task.approve');
    } else {
      rec(`I${n}a`, `A (the starter, the only candidate) sees approve CLOSED with SELF_APPROVAL_NOT_ALLOWED (main B2 follow-up)`, 'approve enabled false, SELF_APPROVAL_NOT_ALLOWED',
        JSON.stringify(summary?.approve), approve?.enabled === false && approve?.disabledReasonCode === 'SELF_APPROVAL_NOT_ALLOWED');
    }
  } else if (phase === 'nosig' || phase === 'sig' || phase === 'direct') {
    const n = Number(arg);
    const token = await login(userOf(who).email);
    const { instanceId, item, stepCode } = await itemOf(token, n);
    const approve = item?.actions?.find((x) => x.code === 'approve');
    let signatureId = null, signedStatus = '-';
    if (phase !== 'nosig') {
      const signed = await call('POST', G + '/api/v1/esign/signatures', token, {
        ticket: await ticket(token), subject: { module: 'workflow', subjectType: 'workflow-task', subjectId: item.id },
        actionCode: approve?.signatureActionCode || 'workflow.task.approve', meaningCode: approve?.signatureMeaning || 'APPROVE',
        appearance: { type: 'drawn', drawing: { pngBase64: png } },
        context: { workflowInstanceId: instanceId, workflowTaskId: item.id, stepCode, decision: 'approve' }, idempotencyKey: key('wfsign')
      });
      signatureId = data(signed)?.id;
      signedStatus = `${signed.status} ${data(signed)?.signatureNumber || code(signed) + ' ' + signed.text.slice(0, 200)}`;
    }
    let r;
    if (phase === 'direct') {
      r = await call('POST', `${G}/api/v1/workflow/tasks/${item.id}/approve`, token, { reasonCode: 'APPROVED', idempotencyKey: key('dir'), comment: 'SYNC1 direct', evidenceRef: null, signatureId });
    } else {
      const payload = { expectedVersion: Number(item.concurrency?.token ?? 0), reason: 'SYNC1 probe' };
      if (signatureId) payload.signatureId = signatureId;
      r = await call('POST', `${G}/api/v1/work-items/${item.id}/actions/approve`, token, { providerCode: item.source?.providerCode || 'workflow', payload });
    }
    const id = `${phase.toUpperCase()}${n}${who}`;
    const exp = who === 'a' ? '409 SOD_VIOLATION' : phase === 'nosig' ? '409 ESIGN_SIGNATURE_REQUIRED' : '200';
    const got = `${r.status} ${code(r)}`.trim();
    rec(id, `${who.toUpperCase()} approves decision ${n}'s task via ${phase === 'direct' ? 'the direct task API' : 'WorkCenter dispatch'} ${phase === 'nosig' ? 'WITHOUT' : 'WITH'} a signature (sign: ${signedStatus})`,
      exp, got + (r.status >= 400 ? ' ' + r.text.slice(0, 160) : ''), exp === '200' ? r.status === 200 : got === exp);
  } else if (phase === 'state') {
    const n = Number(arg);
    const holds = JSON.parse(fs.readFileSync(out + '/holds.json', 'utf8'));
    let dec, hold;
    for (let i = 0; i < (who === 'wait' ? 30 : 1); i++) {
      dec = await decisionOf(a, n);
      hold = (await call('GET', `${Q}/holds/${holds[n]}`, a)).json;
      if (dec?.state === 'Applied') break;
      await new Promise((x) => setTimeout(x, 2000));
    }
    console.log(JSON.stringify({ decision: { number: dec?.decisionNumber, state: dec?.state, workflow: dec?.workflow, applyError: dec?.applyError }, hold: { status: hold?.status, activeDecision: hold?.activeDecision } }, null, 1));
    if (extra) rec(`S${n}`, `decision ${n} after the WorkCenter outcome`, extra, `${dec?.state} / hold ${hold?.status}`, dec?.state === extra);
  }
})().catch((e) => { console.error('PROBE ERROR', e); process.exit(1); });
```

## stack/wfself.js

Workflow seviyesinde başlatanın kendi onayı (W0–W5).

```js
// WP-MVP1-SYNC1 probe W — the starter's own approval on the OWN stack. MOD-0175 never assigns its decider (the quarantine
// port removes the decider from the candidates), so the Workflow-level SoD is probed directly: A starts an instance of the
// published INV-QUARANTINE-RELEASE template with A as the ONLY candidate, then A tries to approve it through WorkCenter
// dispatch and the direct task API (both with a signature). Main's B2 follow-up: A's WorkCenter item shows approve closed.
// usage: node wfself.js <out>
const fs = require('fs');
const out = process.argv[2];
const st = JSON.parse(fs.readFileSync(out + '/stack.json', 'utf8'));
const pw = fs.readFileSync(out + '/pw.txt', 'utf8').trim();
const G = st.gateway;
const resultsFile = out + '/probe-api.json';
const results = fs.existsSync(resultsFile) ? JSON.parse(fs.readFileSync(resultsFile, 'utf8')) : [];
const rec = (id, what, expected, actual, ok) => {
  results.push({ id, phase: 'wfself', what, expected, actual, ok: !!ok, at: new Date().toISOString() });
  fs.writeFileSync(resultsFile, JSON.stringify(results, null, 1));
  console.log(`${ok ? 'PASS' : 'FAIL'} ${id} ${what} — expected ${expected}, got ${actual}`);
};
const key = (p) => `sy1-${p}-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
async function call(method, url, token, body) {
  const headers = { 'Content-Type': 'application/json', 'X-Tenant-Id': st.tenant, 'Accept-Language': 'tr' };
  if (token) headers.Authorization = 'Bearer ' + token;
  const res = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch (_) { json = null; }
  return { status: res.status, json, text };
}
const data = (r) => (r.json && typeof r.json === 'object' && 'data' in r.json ? r.json.data : r.json);
const code = (r) => r.json?.reason_code || r.json?.reasonCode || '';
async function login(email) {
  const r = await call('POST', st.auth + '/api/tenant-auth/login', null, { email, password: pw, rememberMe: false });
  return JSON.stringify(r.json || {}).match(/"accessToken":"([^"]+)"/)[1];
}
(async () => {
  const admin = await login(st.admin);
  const a = await login(st.a);
  const defs = data(await call('GET', G + '/api/v1/workflow/definitions', admin));
  const list = Array.isArray(defs) ? defs : defs?.items || [];
  const tpl = list.find((d) => (d.templateCode || d.code) === 'INV-QUARANTINE-RELEASE');
  const start = await call('POST', G + '/api/v1/workflow/instances', a, {
    templateId: tpl.id, objectType: 'sync1.probe', objectId: key('obj'), candidatePrincipalIds: ['user:' + st.aId],
    idempotencyKey: key('start'), commentRequired: false, evidenceRequired: false
  });
  const s = data(start);
  rec('W0', 'A starts an instance of the published template with A as the only candidate', '200/201', `${start.status} ${code(start)}`, start.status === 200 || start.status === 201);
  const instanceId = s?.workflowInstanceId, taskId = s?.approvalTaskId;

  const mine = data(await call('GET', G + '/api/v1/work-items/mine?pageSize=100', a));
  const item = (mine?.items || []).find((i) => i.id === taskId);
  const approve = item?.actions?.find((x) => x.code === 'approve');
  const delegate = item?.actions?.find((x) => x.code === 'delegate');
  rec('W1', "A's WorkCenter item: approve CLOSED with SELF_APPROVAL_NOT_ALLOWED (main B2 follow-up), signature still flagged",
    'enabled false, SELF_APPROVAL_NOT_ALLOWED', JSON.stringify(approve && { enabled: approve.enabled, disabledReasonCode: approve.disabledReasonCode, signatureRequired: approve.signatureRequired }),
    approve?.enabled === false && approve?.disabledReasonCode === 'SELF_APPROVAL_NOT_ALLOWED');

  const ticket = data(await call('POST', G + '/api/tenant-auth/esign/reauth', a, { password: pw, intent: 'sign' }))?.ticket;
  const signed = await call('POST', G + '/api/v1/esign/signatures', a, {
    ticket, subject: { module: 'workflow', subjectType: 'workflow-task', subjectId: taskId }, actionCode: 'workflow.task.approve', meaningCode: 'APPROVE',
    appearance: { type: 'none' }, context: { workflowInstanceId: instanceId, workflowTaskId: taskId, decision: 'approve' }, idempotencyKey: key('sig')
  });
  const signatureId = data(signed)?.id;
  const dispatched = await call('POST', `${G}/api/v1/work-items/${taskId}/actions/approve`, a,
    { providerCode: 'workflow', payload: { expectedVersion: Number(item?.concurrency?.token ?? 1), reason: 'SYNC1 self', signatureId } });
  rec('W2', `A (the starter) approves through WorkCenter dispatch with a signature (sign ${signed.status} ${data(signed)?.signatureNumber || code(signed)})`, '409 SOD_VIOLATION',
    `${dispatched.status} ${code(dispatched)}`, dispatched.status === 409 && code(dispatched) === 'SOD_VIOLATION');
  const direct = await call('POST', `${G}/api/v1/workflow/tasks/${taskId}/approve`, a, { reasonCode: 'APPROVED', idempotencyKey: key('dir'), comment: null, evidenceRef: null, signatureId });
  rec('W3', 'A (the starter) approves through the direct task API with a signature', '409 SOD_VIOLATION', `${direct.status} ${code(direct)}`, direct.status === 409 && code(direct) === 'SOD_VIOLATION');
  const named = await call('POST', `${G}/api/v1/workflow/tasks/${taskId}/approve`, a, { actorId: st.bId, reasonCode: 'APPROVED', idempotencyKey: key('imp'), comment: null, evidenceRef: null, signatureId });
  rec('W4', 'A names B as the actor in the body (actor binding, FX + main B4)', '403 WORKFLOW_ACTOR_MISMATCH', `${named.status} ${code(named)}`, named.status === 403 && code(named) === 'WORKFLOW_ACTOR_MISMATCH');
  const inst = data(await call('GET', `${G}/api/v1/workflow/instances/${instanceId}`, admin));
  rec('W5', 'the instance is untouched', 'Active', inst?.status, inst?.status === 'Active' || inst?.status === 0);
})().catch((e) => { console.error('PROBE ERROR', e); process.exit(1); });
```

