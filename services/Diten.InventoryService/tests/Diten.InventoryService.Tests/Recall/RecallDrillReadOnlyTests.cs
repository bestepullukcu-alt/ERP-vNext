using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Features.Recall.Drills;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Trace;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.Architecture;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R5 KR-G (pack §8.10) — the READ-ONLY guarantee of the drills, measured twice:
/// <list type="number">
/// <item>structure — no type of <c>…Recall.Drills</c> references a writer outside the drill store (constructors, fields, signatures AND the IL
/// of every method body, compiler-generated state machines and closures included), and the drill store / candidate reader hold nothing else;</item>
/// <item>data — a full drill (plan, Auto start, three steps, prepare, sign) leaves the document count of every non-drill collection of the
/// tenant unchanged; the only growth is the drill collections, ONE trace snapshot of purpose Drill, the counter and the audit rows.</item>
/// </list>
/// </summary>
public sealed class RecallDrillReadOnlyTests : IClassFixture<RecallDrillApiFactory>
{
    private readonly RecallDrillApiFactory _factory;

    public RecallDrillReadOnlyTests(RecallDrillApiFactory factory) => _factory = factory;

    /// <summary>
    /// The writers a drill must never reach — the pack §8.10 names reconciled with the measured code (R5 evidence §3):
    /// <c>IRecallCaseRepository</c> write methods + <c>IRecallLotBlockRepository</c> = <see cref="IRecallRepository"/> / <see cref="IRecallWriteSession"/>
    /// (cases, scope items, lot blocks, quarantine requests, notifications, consumptions, return links live there);
    /// <c>IInventoryLedgerRepository.PostAsync</c> = <see cref="Diten.InventoryService.Domain.Repositories.IInventoryLedgerRepository"/>; <c>IInternalMovementPostingService</c> =
    /// the class <see cref="Application.Features.Inventory.Services.InternalMovementPostingService"/>; "any 0175 command" = every type of
    /// <c>…Features.Quarantine.Commands</c> and the MediatR senders a handler could dispatch one with; plus the lot store, the case trace
    /// service (it holds the case repository) and raw Mongo handles.
    /// </summary>
    internal static readonly Type[] Forbidden =
    [
        typeof(IRecallQuarantineRequester),
        typeof(Application.Features.Recall.Quarantine.RecallQuarantineDispatcher),
        typeof(IRecallRepository),
        typeof(IRecallWriteSession),
        typeof(Diten.InventoryService.Domain.Repositories.IInventoryLedgerRepository),
        typeof(Application.Features.Inventory.Services.InternalMovementPostingService),
        typeof(Diten.InventoryService.Domain.Traceability.Repositories.ILotRepository),
        typeof(RecallTraceService),
        typeof(MediatR.IMediator),
        typeof(MediatR.ISender),
        typeof(MediatR.IPublisher),
        typeof(IMongoDatabase),
        typeof(IMongoClient)
    ];

    private const string QuarantineCommandsNamespace = "Diten.InventoryService.Application.Features.Quarantine.Commands";

    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    internal static List<Type> DrillTypes()
        => typeof(StartRecallDrillHandler).Assembly.GetTypes().Where(t => t.Namespace == typeof(StartRecallDrillHandler).Namespace).ToList();

    /// <summary>Every type a drill type names: base / interfaces, constructor parameters, fields, properties, method signatures, generic arguments and the IL operands.</summary>
    internal static IEnumerable<(Type Owner, string Where, Type Referenced)> References(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        IEnumerable<Type> Expand(Type t)
        {
            yield return t;
            if (t.HasElementType) foreach (var e in Expand(t.GetElementType()!)) yield return e;
            if (t.IsGenericType) foreach (var g in t.GetGenericArguments().SelectMany(Expand)) yield return g;
        }

        foreach (var i in type.GetInterfaces().Append(type.BaseType).OfType<Type>()) foreach (var e in Expand(i)) yield return (type, "base", e);
        foreach (var f in type.GetFields(all)) foreach (var e in Expand(f.FieldType)) yield return (type, "field " + f.Name, e);
        foreach (var p in type.GetProperties(all)) foreach (var e in Expand(p.PropertyType)) yield return (type, "property " + p.Name, e);
        foreach (var m in type.GetConstructors(all).Cast<MethodBase>().Concat(type.GetMethods(all)))
        {
            foreach (var e in m.GetParameters().SelectMany(p => Expand(p.ParameterType))) yield return (type, "parameter of " + m.Name, e);
            if (m is MethodInfo mi) foreach (var e in Expand(mi.ReturnType)) yield return (type, "return of " + m.Name, e);
            foreach (var member in IlMembers(m))
            {
                var referenced = member switch { Type t => t, MethodBase mb => mb.DeclaringType, FieldInfo fi => fi.DeclaringType, _ => null };
                if (referenced is not null) foreach (var e in Expand(referenced)) yield return (type, "IL of " + m.Name, e);
                if (member is FieldInfo field) foreach (var e in Expand(field.FieldType)) yield return (type, "IL of " + m.Name, e);
                if (member is MethodInfo called) foreach (var e in called.GetParameters().Select(p => p.ParameterType).Append(called.ReturnType).SelectMany(Expand))
                    yield return (type, "IL of " + m.Name, e);
            }
        }
    }

    private static IEnumerable<MemberInfo> IlMembers(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        var module = method.Module;
        var typeArgs = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var i = 0;
        while (i < il.Length)
        {
            short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
            i += il[i] == 0xFE ? 2 : 1;
            if (!OpCodesByValue.TryGetValue(value, out var op)) yield break;
            switch (op.OperandType)
            {
                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok:
                    MemberInfo? member = null;
                    try { member = module.ResolveMember(BitConverter.ToInt32(il, i), typeArgs, methodArgs); }
                    catch (Exception ex) when (ex is ArgumentException or BadImageFormatException) { }
                    if (member is not null) yield return member;
                    i += 4;
                    break;
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI8 or OperandType.InlineR: i += 8; break;
                case OperandType.InlineSwitch: i += 4 + BitConverter.ToInt32(il, i) * 4; break;
                default: i += 4; break; // InlineBrTarget, InlineI, InlineSig, InlineString, ShortInlineR
            }
        }
    }

    [Fact]
    public void No_drill_type_references_a_writer_outside_the_drill_store()
    {
        var types = DrillTypes();
        Assert.True(types.Count >= 20, $"the drill namespace has {types.Count} types"); // handlers, models, ports + their state machines / closures
        Assert.All(types.Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal)), t =>
            Assert.Equal("Diten.InventoryService.Application.Features.Recall.Drills", t.Namespace));

        // K3 sabotage (a): inject IRecallQuarantineRequester into a drill handler → named here
        var violations = types.SelectMany(References)
            .Where(r => Forbidden.Contains(r.Referenced.IsGenericType ? r.Referenced.GetGenericTypeDefinition() : r.Referenced)
                        || r.Referenced.Namespace == QuarantineCommandsNamespace
                        || r.Referenced.IsAssignableTo(typeof(IRecallWriteSession)))
            .Select(r => $"{r.Owner.FullName} ({r.Where}) → {r.Referenced.FullName}").Distinct().ToList();
        Assert.True(violations.Count == 0, "drill types reach a writer:\n" + string.Join("\n", violations));

        // the drill handlers' dependencies are exactly these (read ports + the drill store + the e-sign port + the support)
        var allowed = new HashSet<Type>
        {
            typeof(IRecallDrillRepository), typeof(IRecallDrillCandidates), typeof(IRecallDrillRandom), typeof(IRecallSignature), typeof(RecallTraceEngine),
            typeof(Application.Features.Recall.Services.RecallSupport)
        };
        var dependencies = types.Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => (t.Name, p.ParameterType))).ToList();
        Assert.All(dependencies, d => Assert.True(allowed.Contains(d.ParameterType), $"{d.Name} depends on {d.ParameterType.FullName}"));
        Assert.Equal(10, dependencies.Select(d => d.Name).Distinct().Count());
    }

    [Fact]
    public void The_drill_store_can_write_only_drill_records_a_drill_snapshot_the_counter_and_audit_rows()
    {
        // the write session's surface, pinned (no case, scope item, block, request, notification, consumption, return link, movement)
        Assert.Equal(
            ["InsertAuditAsync", "InsertDrillSnapshotAsync", "InsertEventAsync", "InsertPlanAsync", "InsertRunAsync", "NextPlanCodeAsync", "NextRunNumberAsync",
             "ReplacePlanAsync", "ReplaceRunAsync"],
            typeof(IRecallDrillWriteSession).GetMethods().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(IRecallDrillRepository).GetMethods(), m => m.ReturnType.ToString().Contains("IRecallWriteSession", StringComparison.Ordinal));

        // the store holds collections of exactly these documents; the candidate reader holds read handles only and never writes
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.Equal(
            ["BsonDocument", "RecallAuditRecord", "RecallDrillEvent", "RecallDrillPlan", "RecallDrillRun", "RecallSettings", "RecallTraceSnapshot"],
            typeof(RecallDrillRepository).GetFields(fields).Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(IMongoCollection<>))
                .Select(f => f.FieldType.GetGenericArguments()[0].Name).OrderBy(n => n, StringComparer.Ordinal));
        var body = SourceScan.Production("Diten.InventoryService.Persistence").Single(f => f.RelativePath.EndsWith("RecallDrillCandidates.cs", StringComparison.Ordinal)).Body;
        Assert.DoesNotMatch(@"\.(Insert|Update|Replace|Delete|BulkWrite|FindOneAnd)\w*\(", body);
        var store = SourceScan.Production("Diten.InventoryService.Persistence").Single(f => f.RelativePath.EndsWith("RecallDrillRepository.cs", StringComparison.Ordinal)).Body;
        Assert.DoesNotMatch(@"_(events|audit|snapshots)\.(Update|Replace|Delete|FindOneAnd|BulkWrite)", store); // insert-only beside the two version-guarded heads
        Assert.Matches(new Regex(@"Purpose != RecallSnapshotPurpose\.Drill"), store);
    }

    [Fact]
    public async Task A_full_drill_changes_no_document_count_outside_the_drill_records()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        await w.FinishedGoodAsync();

        // explicit exceptions (pack §8.10 + WP-0177-R5 item 7): the drill collections, the Drill snapshot, the counter, the audit rows —
        // and (CT decision 2026-10-14, BL-501) the shared central-audit outbox: an AUDIT write, one row per drill command, named here
        // and nothing else added.
        string[] exceptions =
        [
            RecallCollections.DrillPlans, RecallCollections.DrillRuns, RecallCollections.DrillEvents, RecallCollections.TraceSnapshots,
            RecallCollections.Counters, Diten.InventoryService.Persistence.InventoryCollections.Audit,
            Diten.BuildingBlocks.CentralAudit.CentralAuditOutboxRecord.CollectionName
        ];
        var before = await CountsAsync(w);
        var snapshotsBefore = w.Rows(RecallCollections.TraceSnapshots).Count;

        // K3 sabotage (c): write a case scope item in the Scope step → recall_scope_items grows and this is red
        var plan = await w.PlanAsync();
        var run = await w.StartOkAsync(DrillWorld.PlanId(plan));
        run = await w.StepsAsync(run);
        var prepared = await w.PrepareOkAsync(run);
        var (status, done, _) = await w.ResultAsync(DrillWorld.RunId(run), RecallScenario.ETag(prepared), await w.SignAsync(DrillWorld.RunId(run)));
        Assert.True(status == 200, done.ToString());
        Assert.Equal("Completed", done.GetProperty("status").GetString());

        var after = await CountsAsync(w);
        var changed = after.Keys.Union(before.Keys).Where(c => before.GetValueOrDefault(c) != after.GetValueOrDefault(c))
            .Select(c => $"{c}: {before.GetValueOrDefault(c)} → {after.GetValueOrDefault(c)}").ToList();
        var outside = changed.Where(c => !exceptions.Any(e => c.StartsWith(e + ":", StringComparison.Ordinal))).ToList();
        Assert.True(outside.Count == 0, "a drill wrote outside its records:\n" + string.Join("\n", outside));

        // the exceptions grew as declared: one plan, one run, its events; ONE snapshot and it is of purpose Drill
        Assert.Equal((1, 1), (after[RecallCollections.DrillPlans] - before.GetValueOrDefault(RecallCollections.DrillPlans),
            after[RecallCollections.DrillRuns] - before.GetValueOrDefault(RecallCollections.DrillRuns)));
        Assert.Equal(7, after[RecallCollections.DrillEvents] - before.GetValueOrDefault(RecallCollections.DrillEvents)); // plan, start, 3 steps, prepare, complete
        var snapshots = w.Rows(RecallCollections.TraceSnapshots);
        Assert.Equal(snapshotsBefore + 1, snapshots.Count);
        Assert.Equal("Drill", snapshots.Single(d => d.Contains("DrillRunId") && d["DrillRunId"] == DrillWorld.RunId(run))["Purpose"].AsString);
        Assert.Equal(["recall.drill-plan.saved", "recall.drill.completed", "recall.drill.result-prepared", "recall.drill.started", "recall.drill.step", "recall.trace.run"],
            w.Rows(Diten.InventoryService.Persistence.InventoryCollections.Audit, Builders<BsonDocument>.Filter.Regex("Action", new BsonRegularExpression("^recall\\.(drill|trace)")))
                .Select(a => a["Action"].AsString).Distinct().OrderBy(a => a, StringComparer.Ordinal));
        Assert.True(changed.Count > 0);
    }

    /// <summary>The tenant's document count in EVERY collection of the database (the scenario's tenant only — the suite shares the database).</summary>
    private async Task<Dictionary<string, long>> CountsAsync(DrillWorld w)
    {
        var names = await (await _factory.Database.ListCollectionNamesAsync()).ToListAsync();
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var name in names.Where(n => !n.StartsWith("system.", StringComparison.Ordinal)))
        {
            counts[name] = await _factory.Collection(name).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId", w.Tenant.ToString()));
        }

        return counts;
    }
}
