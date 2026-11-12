using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Features.TenantOrganization.Validators;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services.Mdm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 DL-2 AC-50…AC-59 — Organization Unit (<c>ORG</c>) and Position (<c>POS</c>) codes from the in-process port,
/// over real HTTP: the shipped <c>OrganizationUnitsController</c> / <c>PositionsController</c> with their <c>[HasPermission]</c>,
/// the production <c>TenantResolutionMiddleware</c>, MediatR pipeline (<c>AuditBehavior</c> writing the central row through the
/// production outbox), <c>GlobalExceptionHandler</c>, the shipped MOD-0288 handlers and Mongo repositories, the port, the DL-1
/// allocation handler, door and refusal writer on a real replica set, and both MDM readers over a fake MDM whose short code
/// a test can change. MDM's own rules that matter here are mimicked in front of it: the token must hold
/// <c>mdm.legal-entities.read</c> (else 403) and a company of another tenant is 404. Test-only wrappers around the two
/// record repositories stage what the pack names: a lost record write (a <see cref="MongoException"/>, AC-54 b) and a unit that
/// is deleted or archived between the guard's read and the handler's second read (AC-51, F-2, F-15 a).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class OrganizationRecordCodeHttpTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid TenantA = Guid.Parse("40fc0002-0000-4000-8000-00000000a0e1");
    private static readonly Guid TenantB = Guid.Parse("40fc0002-0000-4000-8000-00000000b0e1");
    private static readonly Guid Suspended = Guid.Parse("40fc0002-0000-4000-8000-00000000c0e1");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000e1");
    private static readonly Guid Myg = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private static readonly Guid Gpl = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f8");
    private static readonly Guid CompanyZ = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f9");
    private static readonly Guid CompanyW = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6fa");
    private static readonly Guid CompanyOfB = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6fb");
    private static readonly Guid CompanyOfSuspended = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6fc");
    private const string MdmRead = "mdm.legal-entities.read";

    private static readonly string[] OrganizationKeys =
    [
        "platform.organization-units.create", "platform.organization-units.read", "platform.organization-units.update",
        "platform.organization-units.reporting-line.update", "platform.positions.create", "platform.positions.read",
        "platform.positions.update", MdmRead
    ];

    private readonly DisposableMongoReplicaSet _mongo;

    public OrganizationRecordCodeHttpTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    private static string Token(Guid? tenant = null, params string[] keys) =>
        RecordCodeHttpHost.TenantUserToken(tenant ?? TenantA, User, keys.Length == 0 ? OrganizationKeys : keys);

    // ── AC-50 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC50_org_unit_create_takes_MYG_ORG_000001_then_000002()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);

        var first = await org.PostUnitAsync(Token(), Myg);
        var second = await org.PostUnitAsync(Token(), Myg);

        Assert.Equal((201, 201), (first.Status, second.Status));
        Assert.Equal("MYG-ORG-000001", await org.UnitCodeAsync(first.Id));
        Assert.Equal("MYG-ORG-000002", await org.UnitCodeAsync(second.Id));
        var get = await org.Host.SendAsync(HttpMethod.Get, $"/api/platform/organization-units/{first.Id}", Token());
        Assert.Contains("\"code\":\"MYG-ORG-000001\"", await get.Content.ReadAsStringAsync());

        foreach (var (id, code) in new[] { (first.Id, "MYG-ORG-000001"), (second.Id, "MYG-ORG-000002") })
        {
            var row = Assert.Single(await org.LedgerAsync(), r => r["Code"].AsString == code);
            Assert.Equal("Diten.Platform", row["CallerService"].AsString);
            Assert.Equal($"organization-unit:{id:N}", row["IdempotencyKey"].AsString);
            Assert.Equal("ORG", row["RecordType"].AsString);
            Assert.Equal("LegalEntity", PrefixScopeName(row["PrefixScope"]));
            Assert.Equal(Myg, row["LegalEntityId"].AsGuid);
            Assert.Equal(TenantA, row["TenantId"].AsGuid);
        }

        Assert.Equal(2, await org.Host.CountAsync("record_code_allocations"));
    }

    // ── AC-51 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC51_position_takes_its_unit_s_company_prefix_and_keeps_it_after_a_move()
    {
        // The user's own context company is GPL (the data-scope resolver says so); the first position goes into a MYG unit.
        using var org = await OrganizationHost.StartAsync(_mongo, contextCompany: Gpl);
        var mygUnit = await org.PostUnitAsync(Token(), Myg);
        var gplUnit = await org.PostUnitAsync(Token(), Gpl);

        var inMyg = await org.PostPositionAsync(Token(), mygUnit.Id);
        var inGpl = await org.PostPositionAsync(Token(), gplUnit.Id);
        var nextUnit = await org.PostUnitAsync(Token(), Myg);

        Assert.Equal((201, 201), (inMyg.Status, inGpl.Status));
        Assert.Equal("MYG-POS-000001", await org.PositionCodeAsync(inMyg.Id));
        Assert.Equal("GPL-POS-000001", await org.PositionCodeAsync(inGpl.Id));
        Assert.Equal("MYG-ORG-000002", await org.UnitCodeAsync(nextUnit.Id)); // the POS counter is its own
        Assert.Equal($"position:{inMyg.Id:N}",
            Assert.Single(await org.LedgerAsync(), r => r["Code"].AsString == "MYG-POS-000001")["IdempotencyKey"].AsString);

        var ledgerBefore = await org.Host.CountAsync("record_code_allocations");
        var moved = await org.PutPositionAsync(Token(), inMyg.Id, "MYG-POS-000001", gplUnit.Id);
        Assert.Equal(204, moved.Status);
        Assert.Equal("MYG-POS-000001", await org.PositionCodeAsync(inMyg.Id));
        Assert.Equal(ledgerBefore, await org.Host.CountAsync("record_code_allocations"));

        // The unit's company is no longer active in MDM (MDM answers 404): no position, nothing allocated.
        var counters = await org.CounterSnapshotAsync();
        org.Host.Mdm.Answer(Myg, 404);
        var inactive = await org.PostPositionAsync(Token(), mygUnit.Id);
        Assert.Equal((404, "LEGAL_ENTITY_NOT_FOUND"), (inactive.Status, inactive.Reason));
        Assert.Equal(2, await org.Host.CountAsync(PlatformCollections.Positions));
        Assert.Equal(counters, await org.CounterSnapshotAsync());
    }

    [Theory]
    [InlineData(UnitSecondRead.Deleted)]
    [InlineData(UnitSecondRead.Archived)]
    public async Task AC51_a_unit_gone_or_archived_after_the_guard_is_404_and_allocates_nothing(UnitSecondRead mode)
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var unit = await org.PostUnitAsync(Token(), Myg);
        var counters = await org.CounterSnapshotAsync();
        var ledger = await org.Host.CountAsync("record_code_allocations");

        org.Hooks.SecondReadOf = (unit.Id, mode);
        var answer = await org.PostPositionAsync(Token(), unit.Id);

        Assert.Equal(404, answer.Status);
        Assert.Contains("Organization Unit not found.", answer.Raw);
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.Positions));
        Assert.Equal(counters, await org.CounterSnapshotAsync());
        Assert.Equal(ledger, await org.Host.CountAsync("record_code_allocations"));
        Assert.Equal(2, org.Hooks.ReadsOfTarget); // the guard's read and the handler's second read, nothing else
    }

    [Fact]
    public async Task AC51_a_position_without_mdm_read_is_refused_and_allocates_nothing()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var unit = await org.PostUnitAsync(Token(), Myg);

        var answer = await org.PostPositionAsync(Token(TenantA, "platform.positions.create"), unit.Id);

        Assert.Equal((403, "LEGAL_ENTITY_READ_PERMISSION_MISSING"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.Positions));
        var refusal = Assert.Single(await org.Host.DeliveredAuditEventsAsync(), e => IsAllocationRefusal(e));
        Assert.Equal(AuditOutcome.Denied, refusal.Outcome);
        Assert.Equal(11, Convert.ToInt32(refusal.Metadata["EvaluationStep"]));
    }

    // ── AC-52 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC52_a_create_that_carries_a_code_is_refused_and_allocates_nothing()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var unit = await org.SeedUnitAsync("HEADQUARTERS", Myg);

        foreach (var code in new[] { "MYG-ORG-000009", new string('A', 81), new string('B', 500) })
        {
            var refusedUnit = await org.PostUnitAsync(Token(), Myg, code);
            var refusedPosition = await org.PostPositionAsync(Token(), unit, code);
            Assert.Equal((400, "RECORD_CODE_SYSTEM_ASSIGNED", true), (refusedUnit.Status, refusedUnit.Reason, refusedUnit.IsEnvelope));
            Assert.Equal((400, "RECORD_CODE_SYSTEM_ASSIGNED", true), (refusedPosition.Status, refusedPosition.Reason, refusedPosition.IsEnvelope));
        }

        Assert.Equal(0, await org.Host.CountAsync("record_code_counters"));
        Assert.Equal(0, await org.Host.CountAsync("record_code_allocations"));
        Assert.Equal(1, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.Positions));
        var events = await org.Host.DeliveredAuditEventsAsync();
        Assert.Equal(3, events.Count(e => e.RequestType == "CreateOrganizationUnitCommand" && e.Outcome == AuditOutcome.Failed));
        Assert.Equal(3, events.Count(e => e.RequestType == "CreatePositionCommand" && e.Outcome == AuditOutcome.Failed));

        // Whitespace only is blank: the system's code.
        var blank = await org.PostUnitAsync(Token(), Myg, "   ");
        Assert.Equal(201, blank.Status);
        Assert.Equal("MYG-ORG-000001", await org.UnitCodeAsync(blank.Id));

        // No code rule is left in the validators: a long or missing code never reaches ProblemDetails.
        foreach (var code in new string?[] { null, new string('C', 500) })
        {
            Assert.DoesNotContain(new CreateOrganizationUnitCommandValidator().Validate(new CreateOrganizationUnitCommand(
                new OrganizationUnitRequest(code, "Unit", Myg, null))).Errors, e => e.PropertyName.EndsWith("Code", StringComparison.Ordinal));
            Assert.DoesNotContain(new CreatePositionCommandValidator().Validate(new CreatePositionCommand(
                new PositionRequest(code, "Position", unit, null))).Errors, e => e.PropertyName.EndsWith("Code", StringComparison.Ordinal));
        }
    }

    // ── AC-53 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC53_update_cannot_change_an_old_or_a_new_code()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var headquarters = await org.SeedUnitAsync("HEADQUARTERS", Myg);
        var ceo = await org.SeedPositionAsync("CEO", headquarters);
        var created = await org.PostUnitAsync(Token(), Myg);
        var createdPosition = await org.PostPositionAsync(Token(), created.Id);

        // the old hand-typed unit code
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE"), Pair(await org.PutUnitAsync(Token(), headquarters, "HEADQUARTERS2", Myg)));
        Assert.Equal(204, (await org.PutUnitAsync(Token(), headquarters, "   ", Myg)).Status);
        Assert.Equal(204, (await org.PutUnitAsync(Token(), headquarters, "HEADQUARTERS", Myg)).Status);
        Assert.Equal("HEADQUARTERS", await org.UnitCodeAsync(headquarters));

        // the old hand-typed position code — nothing is trimmed, upper-cased or normalized
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE"), Pair(await org.PutPositionAsync(Token(), ceo, " CEO", headquarters)));
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE"), Pair(await org.PutPositionAsync(Token(), ceo, "ceo", headquarters)));
        var longCode = await org.PutPositionAsync(Token(), ceo, new string('X', 81), headquarters);
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE", true), (longCode.Status, longCode.Reason, longCode.IsEnvelope));
        Assert.Equal(204, (await org.PutPositionAsync(Token(), ceo, "  ", headquarters)).Status);
        Assert.Equal(204, (await org.PutPositionAsync(Token(), ceo, "CEO", headquarters)).Status);
        Assert.Equal("CEO", await org.PositionCodeAsync(ceo));

        // the new system-assigned codes
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE"), Pair(await org.PutUnitAsync(Token(), created.Id, "MYG-ORG-000002", Myg)));
        var longUnit = await org.PutUnitAsync(Token(), created.Id, new string('Y', 81), Myg);
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE", true), (longUnit.Status, longUnit.Reason, longUnit.IsEnvelope));
        Assert.Equal((409, "RECORD_CODE_IMMUTABLE"), Pair(await org.PutPositionAsync(Token(), createdPosition.Id, "MYG-POS-000009", created.Id)));
        Assert.Equal(204, (await org.PutUnitAsync(Token(), created.Id, "MYG-ORG-000001", Myg)).Status);

        // a change of company and the reporting-lines endpoint keep the code (RCS-001 §5.2)
        Assert.Equal(204, (await org.PutUnitAsync(Token(), created.Id, "MYG-ORG-000001", Gpl)).Status);
        var reporting = await org.Host.SendAsync(HttpMethod.Put, $"/api/platform/organization-units/{created.Id}/reporting-lines", Token(), "{}");
        Assert.Equal(204, (int)reporting.StatusCode);
        Assert.Equal("MYG-ORG-000001", await org.UnitCodeAsync(created.Id));
        Assert.Equal("MYG-POS-000001", await org.PositionCodeAsync(createdPosition.Id));
    }

    // ── AC-54 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC54a_a_refused_allocation_answers_as_refused_and_writes_no_record()
    {
        var invalid = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e600");
        using var org = await OrganizationHost.StartAsync(_mongo);
        org.Host.Mdm.Company(invalid, "M1");
        var mygUnit = await org.PostUnitAsync(Token(), Myg);
        await org.Host.ProcessOutboxAsync();
        await org.Host.Collection(AuditCollectionNames.AuditEvents).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);

        // short code that does not fit ^[A-Z]{2,5}$ — the unit, then a position in a company whose code went bad
        var badUnit = await org.PostUnitAsync(Token(), invalid);
        org.Host.Mdm.Company(Myg, "my-g");
        var badPosition = await org.PostPositionAsync(Token(), mygUnit.Id);
        org.Host.Mdm.Company(Myg, "MYG");

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_MISSING"), Pair(badUnit));
        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_MISSING"), Pair(badPosition));
        Assert.Equal(1, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.Positions));
        var events = await org.Host.DeliveredAuditEventsAsync();
        Assert.Equal(2, events.Count(e => IsAllocationRefusal(e) && e.SourceService == "Diten.Platform"));
        Assert.Single(events, e => e.RequestType == "CreateOrganizationUnitCommand" && e.Outcome == AuditOutcome.Failed);
        Assert.Single(events, e => e.RequestType == "CreatePositionCommand" && e.Outcome == AuditOutcome.Failed);

        // a suspended tenant
        var suspended = await org.PostUnitAsync(Token(Suspended), CompanyOfSuspended);
        Assert.Equal((403, "RECORD_CODE_TENANT_NOT_ACTIVE"), Pair(suspended));

        // the allocation's own transaction is lost (three unknown commit results)
        org.Hooks.UnknownCommits = 3;
        var lost = await org.PostUnitAsync(Token(), Myg);
        Assert.Equal((503, "RECORD_CODE_UNAVAILABLE"), Pair(lost));
        Assert.True(lost.IsEnvelope);
        Assert.Equal(1, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
        Assert.Single(await org.Host.DeliveredAuditEventsAsync(),
            e => IsAllocationRefusal(e) && (string?)e.Metadata["ReasonCode"] == "RECORD_CODE_UNAVAILABLE");
    }

    [Fact]
    public async Task AC54b_a_lost_record_write_is_a_500_the_code_a_gap_and_the_warning_comes_first()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);

        org.Hooks.FailNextRecordWrite = true;
        var lost = await org.PostUnitAsync(Token(), Myg);

        Assert.Equal(500, lost.Status);
        Assert.False(lost.IsEnvelope);
        using (var problem = JsonDocument.Parse(lost.Raw))
        {
            Assert.Equal("Server Error", problem.RootElement.GetProperty("title").GetString());
            Assert.False(problem.RootElement.TryGetProperty("reason_code", out _));
        }

        Assert.DoesNotContain("write lost", lost.Raw);
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
        var gap = Assert.Single(await org.LedgerAsync());
        Assert.Equal("MYG-ORG-000001", gap["Code"].AsString);

        var events = await org.Host.DeliveredAuditEventsAsync();
        var central = Assert.Single(events, e => e.RequestType == "CreateOrganizationUnitCommand");
        Assert.Equal(AuditOutcome.Failed, central.Outcome);
        Assert.Equal("RuntimeException", central.Metadata["AuditBehaviorOutcome"]);
        Assert.Equal(nameof(RecordWriteLostException), central.Metadata["ExceptionType"]);
        Assert.Equal("MYG-ORG-000001", Assert.Single(events, IsAllocationSuccess).AfterState!["Code"]);

        var warning = org.Logs.FindIndex(log => log.Level == LogLevel.Warning
            && log.Category.EndsWith("CreateOrganizationUnitCommandHandler", StringComparison.Ordinal)
            && log.Message.Contains("MYG-ORG-000001", StringComparison.Ordinal)
            && log.Message.Contains(TenantA.ToString(), StringComparison.Ordinal)
            && log.Exception is RecordWriteLostException);
        var error = org.Logs.FindIndex(log => log.Level >= LogLevel.Error && log.Exception is RecordWriteLostException);
        Assert.True(warning >= 0, "the handler's warning (code, record id, tenant) was not written");
        Assert.True(error < 0 || warning < error, "the warning must be written before the exception travels on");

        var next = await org.PostUnitAsync(Token(), Myg);
        Assert.Equal("MYG-ORG-000002", await org.UnitCodeAsync(next.Id));
    }

    [Fact]
    public async Task AC54c_a_validation_failure_is_refused_before_the_allocation()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);

        var orphanParent = await org.PostUnitAsync(Token(), Myg, parent: Guid.NewGuid());
        var missingUnit = await org.PostPositionAsync(Token(), Guid.NewGuid());

        Assert.Equal((404, 404), (orphanParent.Status, missingUnit.Status));
        Assert.Equal(0, await org.Host.CountAsync("record_code_counters"));
        Assert.Equal(0, await org.Host.CountAsync("record_code_allocations"));
        Assert.DoesNotContain(await org.Host.DeliveredAuditEventsAsync(), e => IsAllocationRefusal(e));
    }

    [Fact]
    public async Task AC54d_a_door_refusal_on_the_success_path_is_a_503_and_no_record()
    {
        using var org = await OrganizationHost.StartAsync(_mongo, configure: services =>
            services.AddScoped<ITransactionalAuditOutboxWriter, RefusingDoor>());

        var answer = await org.PostUnitAsync(Token(), Myg);

        Assert.Equal((503, "AUDIT_RECORD_UNAVAILABLE"), Pair(answer));
        Assert.False(answer.IsEnvelope);
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
    }

    // ── AC-55 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC55_a_legacy_code_on_the_counter_s_path_is_skipped_even_when_soft_deleted()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var headquarters = await org.SeedUnitAsync("HEADQUARTERS", Myg);
        await org.SeedUnitAsync("MYG-ORG-000001", Myg, deleted: true);

        var created = await org.PostUnitAsync(Token(), Myg);

        Assert.Equal(201, created.Status);
        Assert.Equal("MYG-ORG-000002", await org.UnitCodeAsync(created.Id));
        Assert.Equal("HEADQUARTERS", await org.UnitCodeAsync(headquarters));
        var ledger = await org.LedgerAsync();
        Assert.Equal(
            new[] { ("MYG-ORG-000001", $"organization-unit:{created.Id:N}"), ("MYG-ORG-000002", $"organization-unit:{created.Id:N}:2") },
            ledger.Select(r => (r["Code"].AsString, r["IdempotencyKey"].AsString)).OrderBy(p => p.Item1).ToArray());
        var successes = (await org.Host.DeliveredAuditEventsAsync()).Where(IsAllocationSuccess)
            .Select(e => ((string)e.AfterState!["Code"]!, (string)e.Metadata["IdempotencyKey"]!)).OrderBy(p => p.Item1).ToArray();
        Assert.Equal(ledger.Select(r => (r["Code"].AsString, r["IdempotencyKey"].AsString)).OrderBy(p => p.Item1).ToArray(), successes);
        Assert.Contains(org.Logs, log => log.Level == LogLevel.Warning && log.Message.Contains("MYG-ORG-000001", StringComparison.Ordinal)
            && log.Message.Contains("skipped", StringComparison.Ordinal));

        // three live legacy codes on the path: 409, three numbers burnt, no record
        foreach (var code in new[] { "MYG-ORG-000003", "MYG-ORG-000004", "MYG-ORG-000005" })
        {
            await org.SeedUnitAsync(code, Myg);
        }

        var collided = await org.PostUnitAsync(Token(), Myg);
        Assert.Equal((409, "RECORD_CODE_COLLISION"), Pair(collided));
        var burnt = (await org.LedgerAsync()).Where(r => r["Code"].AsString is "MYG-ORG-000003" or "MYG-ORG-000004" or "MYG-ORG-000005")
            .Select(r => r["IdempotencyKey"].AsString).ToArray();
        Assert.Equal(3, burnt.Length);
        Assert.Single(burnt.Select(k => string.Join(':', k.Split(':').Take(2))).Distinct()); // one record id, three attempts
        Assert.Contains(burnt, k => k.Count(c => c == ':') == 1);
        Assert.Contains(burnt, k => k.EndsWith(":2", StringComparison.Ordinal));
        Assert.Contains(burnt, k => k.EndsWith(":3", StringComparison.Ordinal));
        Assert.Equal(5L, (await org.Host.Collection("record_code_counters").Find(new BsonDocument("RecordType", "ORG")).SingleAsync())["LastNumber"].ToInt64());
        Assert.Equal(6, await org.Host.CountAsync(PlatformCollections.OrganizationUnits)); // HEADQUARTERS, deleted 000001, created, 3 seeded
        Assert.Contains(await org.Host.DeliveredAuditEventsAsync(),
            e => e.RequestType == "CreateOrganizationUnitCommand" && e.Outcome == AuditOutcome.Failed);
    }

    // ── AC-56 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC56_the_interim_short_code_lock_stops_org_and_pos_creates()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var pay = await RecordCodeHttpHost.ReadAsync(await org.Host.AllocateAsync(PayToken(TenantA), RecordCodeHttpHost.Body("PAY", null, Myg, "pay-1")));
        var unit = await org.PostUnitAsync(Token(), Myg);
        Assert.Equal(("MYG-PAY-000001", 201), (pay.Code, unit.Status));
        await org.Host.ProcessOutboxAsync();
        await org.Host.Collection(AuditCollectionNames.AuditEvents).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        var counters = await org.CounterSnapshotAsync();

        org.Host.Mdm.Company(Myg, "MYX");
        var refusedUnit = await org.PostUnitAsync(Token(), Myg);
        var refusedPosition = await org.PostPositionAsync(Token(), unit.Id);

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), Pair(refusedUnit));
        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), Pair(refusedPosition));
        Assert.Equal(1, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
        Assert.Equal(0, await org.Host.CountAsync(PlatformCollections.Positions));
        Assert.Equal(counters, await org.CounterSnapshotAsync());
        var refusals = (await org.Host.DeliveredAuditEventsAsync()).Where(IsAllocationRefusal).ToList();
        Assert.Equal(2, refusals.Count);
        Assert.All(refusals, refusal =>
        {
            Assert.Equal(AuditOutcome.Failed, refusal.Outcome);
            Assert.Equal(11, Convert.ToInt32(refusal.Metadata["EvaluationStep"]));
            Assert.Equal("MYX", refusal.Metadata["Prefix"]);
        });
        Assert.Contains(org.Logs, log => log.Level >= LogLevel.Error && log.Message.Contains("MYX", StringComparison.Ordinal));

        org.Host.Mdm.Company(Myg, "MYG");
        var restored = await org.PostUnitAsync(Token(), Myg);
        Assert.Equal("MYG-ORG-000002", await org.UnitCodeAsync(restored.Id));
    }

    [Fact]
    public async Task AC56b_an_org_unit_first_then_an_mdm_change_stops_treasury()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var unit = await org.PostUnitAsync(Token(), CompanyZ);
        Assert.Equal("MYZ-ORG-000001", await org.UnitCodeAsync(unit.Id));

        org.Host.Mdm.Company(CompanyZ, "MZX");
        var pay = await RecordCodeHttpHost.ReadAsync(await org.Host.AllocateAsync(PayToken(TenantA), RecordCodeHttpHost.Body("PAY", null, CompanyZ, "pay-z")));
        var agr = await RecordCodeHttpHost.ReadAsync(await org.Host.AllocateAsMdmAsync(
            RecordCodeHttpHost.TenantUserToken(TenantA, User, "mdm.contracts.create", MdmRead), RecordCodeHttpHost.Body("AGR", null, CompanyZ, "agr-z")));

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED", true), (pay.Status, pay.Reason, pay.IsEnvelope));
        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), (agr.Status, agr.Reason));
        var refusal = Assert.Single(await org.Host.DeliveredAuditEventsAsync(),
            e => IsAllocationRefusal(e) && e.SourceService == "Diten.FinanceTreasuryService");
        Assert.Equal("MZX", refusal.Metadata["Prefix"]);

        org.Host.Mdm.Company(CompanyZ, "MYZ");
        var recovered = await RecordCodeHttpHost.ReadAsync(await org.Host.AllocateAsync(PayToken(TenantA), RecordCodeHttpHost.Body("PAY", null, CompanyZ, "pay-z2")));
        Assert.Equal((200, "MYZ-PAY-000001"), (recovered.Status, recovered.Code));
    }

    [Fact]
    public async Task AC56c_a_freed_prefix_given_to_another_company_stops_both()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        await org.PostUnitAsync(Token(), CompanyZ);

        org.Host.Mdm.Company(CompanyZ, "MZX");
        org.Host.Mdm.Company(CompanyW, "MYZ");
        var unit = await org.PostUnitAsync(Token(), CompanyW);
        var pay = await RecordCodeHttpHost.ReadAsync(await org.Host.AllocateAsync(PayToken(TenantA), RecordCodeHttpHost.Body("PAY", null, CompanyW, "pay-w")));

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), Pair(unit));
        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), (pay.Status, pay.Reason));
        Assert.Equal(1, await org.Host.CountAsync(PlatformCollections.OrganizationUnits));
    }

    // ── AC-57 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC57_cross_tenant_company_or_unit_is_404_and_allocates_nothing()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var unitOfB = await org.SeedUnitAsync("B-ROOT", CompanyOfB, TenantB);

        var unit = await org.PostUnitAsync(Token(TenantA), CompanyOfB);
        var position = await org.PostPositionAsync(Token(TenantA), unitOfB);

        Assert.Equal((404, 404), (unit.Status, position.Status));
        Assert.Equal(0, await org.Host.CountAsync("record_code_counters"));
        Assert.Equal(0, await org.Host.CountAsync("record_code_allocations"));
        Assert.DoesNotContain(await org.Host.DeliveredAuditEventsAsync(), e => IsAllocationRefusal(e));

        var allowed = await org.PostUnitAsync(Token(TenantB), CompanyOfB);
        Assert.Equal(201, allowed.Status);
        Assert.Equal(TenantB, Assert.Single(await org.LedgerAsync())["TenantId"].AsGuid);
        Assert.Equal(TenantB, (await org.Host.Collection("record_code_counters").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["TenantId"].AsGuid);
    }

    // ── AC-58 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC58_a_user_without_the_create_key_allocates_nothing()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);
        var unit = await org.SeedUnitAsync("ROOT", Myg);
        var withoutCreate = Token(TenantA, "platform.organization-units.read", "platform.positions.read", MdmRead);

        var refusedUnit = await org.PostUnitAsync(withoutCreate, Myg);
        var refusedPosition = await org.PostPositionAsync(withoutCreate, unit);

        Assert.Equal((403, "PERM_DENIED"), Pair(refusedUnit));
        Assert.Equal((403, "PERM_DENIED"), Pair(refusedPosition));
        Assert.Equal(0, await org.Host.CountAsync("record_code_counters"));
        Assert.Equal(0, await org.Host.CountAsync("record_code_allocations"));
        Assert.DoesNotContain(await org.Host.DeliveredAuditEventsAsync(), e => IsAllocationRefusal(e));
    }

    // ── AC-59 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC59_an_org_create_leaves_the_central_row_and_the_allocation_row()
    {
        using var org = await OrganizationHost.StartAsync(_mongo);

        var unit = await org.PostUnitAsync(Token(), Myg);
        var position = await org.PostPositionAsync(Token(), unit.Id);

        Assert.Equal((201, 201), (unit.Status, position.Status));
        var events = await org.Host.DeliveredAuditEventsAsync();
        Assert.Equal(AuditOutcome.Succeeded, Assert.Single(events, e => e.RequestType == "CreateOrganizationUnitCommand").Outcome);
        Assert.Equal(AuditOutcome.Succeeded, Assert.Single(events, e => e.RequestType == "CreatePositionCommand").Outcome);

        foreach (var (key, code, company) in new[]
                 {
                     ($"organization-unit:{unit.Id:N}", "MYG-ORG-000001", Myg),
                     ($"position:{position.Id:N}", "MYG-POS-000001", Myg)
                 })
        {
            var row = Assert.Single(events, e => IsAllocationSuccess(e) && (string?)e.Metadata["IdempotencyKey"] == key);
            Assert.Equal("AllocateRecordCodeCommand", row.RequestType);
            Assert.Equal("Diten.Platform", row.SourceService);
            Assert.Equal(AuditActorType.TenantUser, row.ActorType);
            Assert.Equal(User, row.ActorId);
            Assert.Equal(code, row.AfterState!["Code"]);
            Assert.Equal(company.ToString("D"), row.AfterState["LegalEntityId"]);
            Assert.Equal(false, row.Metadata["GroupPrefixLockedNow"]);
        }
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static string PayToken(Guid tenant) =>
        RecordCodeHttpHost.TenantUserToken(tenant, User, "finance-treasury.payment-batches.create", MdmRead);

    private static (int, string?) Pair(OrganizationAnswer answer) => (answer.Status, answer.Reason);

    private static bool IsAllocationRefusal(AuditEvent e) =>
        e.RequestType == "AllocateRecordCodeCommand" && (string?)e.Metadata["EventName"] == "platform.record-code.allocation-refused";

    private static bool IsAllocationSuccess(AuditEvent e) =>
        e.RequestType == "AllocateRecordCodeCommand" && (string?)e.Metadata["EventName"] == "platform.record-code.allocated";

    private static string PrefixScopeName(BsonValue value) =>
        value.IsString ? value.AsString : ((Diten.Platform.Domain.Enums.RecordCodes.RecordCodePrefixScope)value.ToInt32()).ToString();

    public enum UnitSecondRead
    {
        Deleted,
        Archived
    }

    /// <summary>The test's knobs, shared by every request scope of one host.</summary>
    internal sealed class OrganizationHooks : Diten.Platform.Infrastructure.Persistence.IPlatformTransactionFaultProbe
    {
        public (Guid UnitId, UnitSecondRead Mode)? SecondReadOf { get; set; }
        public int ReadsOfTarget { get; set; }
        public bool FailNextRecordWrite { get; set; }
        public int UnknownCommits { get; set; }

        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct)
        {
            if (UnknownCommits > 0)
            {
                UnknownCommits--;
                throw RecordCodeFaults.UnknownCommit();
            }

            return Task.CompletedTask;
        }

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>One host per test: the record-code host plus the Organization endpoints' production wiring.</summary>
    internal sealed class OrganizationHost : IDisposable
    {
        private OrganizationHost(RecordCodeHttpHost host, OrganizationHooks hooks, CapturingLoggerProvider logs)
        {
            Host = host;
            Hooks = hooks;
            LogProvider = logs;
        }

        public RecordCodeHttpHost Host { get; }
        public OrganizationHooks Hooks { get; }
        private CapturingLoggerProvider LogProvider { get; }
        public List<CapturedLog> Logs => LogProvider.Entries.ToList();

        public static async Task<OrganizationHost> StartAsync(
            DisposableMongoReplicaSet mongo, Guid? contextCompany = null, Action<IServiceCollection>? configure = null)
        {
            var hooks = new OrganizationHooks();
            var logs = new CapturingLoggerProvider();
            var tenantOfCompany = new Dictionary<Guid, Guid> { [CompanyOfB] = TenantB, [CompanyOfSuspended] = Suspended };
            RecordCodeHttpHost? self = null;
            var options = new RecordCodeHostOptions
            {
                FaultProbe = hooks,
                ConfigureServices = services =>
                {
                    services.AddSingleton<ILoggerProvider>(logs);
                    services.AddScoped<IAuditOutboxWriter>(provider => provider.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IActorPermissionContext, ClaimsActorPermissionContext>();
                    services.AddScoped<IDataScopeResolver>(_ => contextCompany is { } company
                        ? new FakeDataScopeResolver(new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, company, null))
                        : new FakeDataScopeResolver());

                    services.AddScoped<OrganizationUnitRepository>();
                    services.AddScoped<IOrganizationUnitRepository>(provider =>
                        new StagedUnits(provider.GetRequiredService<OrganizationUnitRepository>(), hooks));
                    services.AddScoped<IOrganizationReportingGraphRepository>(provider => provider.GetRequiredService<OrganizationUnitRepository>());
                    services.AddScoped<IOrganizationUnitCodeHistoryRepository>(provider => provider.GetRequiredService<OrganizationUnitRepository>());
                    services.AddScoped<PositionRepository>();
                    services.AddScoped<IPositionRepository>(provider =>
                        new StagedPositions(provider.GetRequiredService<PositionRepository>(), hooks));
                    services.AddScoped<IPositionCodeHistoryRepository>(provider => provider.GetRequiredService<PositionRepository>());
                    services.AddScoped<IPositionAssignmentRepository, PositionAssignmentRepository>();

                    // Both MDM readers go through MDM's own two rules (read permission, the company's tenant) to the fake.
                    services.AddHttpClient<ILegalEntityReferenceValidator, MdmLegalEntityReferenceValidator>()
                        .ConfigurePrimaryHttpMessageHandler(() => new MdmRules(self!.Mdm, tenantOfCompany));
                    services.AddHttpClient<Diten.Platform.Application.Contracts.RecordCodes.ILegalEntityShortCodeReader, MdmLegalEntityShortCodeReader>()
                        .ConfigurePrimaryHttpMessageHandler(() => new MdmRules(self!.Mdm, tenantOfCompany));
                    configure?.Invoke(services);
                }
            };

            self = await RecordCodeHttpHost.StartAsync(mongo, "diten_record_code_org", options, TenantA, TenantB);
            await PlatformSchemaManifest.ApplyAsync(self.Database, new[] { SchemaProfile.Organization });
            await self.SeedTenantAsync(Suspended, TenantStatus.Suspended);
            foreach (var (company, code) in new[] { (Myg, "MYG"), (Gpl, "GPL"), (CompanyZ, "MYZ"), (CompanyW, "WWW"), (CompanyOfB, "BBB"), (CompanyOfSuspended, "SUS") })
            {
                self.Mdm.Company(company, code);
            }

            return new OrganizationHost(self, hooks, logs);
        }

        public Task<OrganizationAnswer> PostUnitAsync(string token, Guid company, string? code = null, Guid? parent = null) =>
            SendAsync(HttpMethod.Post, "/api/platform/organization-units", token, UnitBody(code, company, parent));

        public Task<OrganizationAnswer> PutUnitAsync(string token, Guid id, string? code, Guid company, Guid? parent = null) =>
            SendAsync(HttpMethod.Put, $"/api/platform/organization-units/{id}", token, UnitBody(code, company, parent));

        public Task<OrganizationAnswer> PostPositionAsync(string token, Guid unit, string? code = null) =>
            SendAsync(HttpMethod.Post, "/api/platform/positions", token, PositionBody(code, unit));

        public Task<OrganizationAnswer> PutPositionAsync(string token, Guid id, string? code, Guid unit) =>
            SendAsync(HttpMethod.Put, $"/api/platform/positions/{id}", token, PositionBody(code, unit));

        public async Task<string> UnitCodeAsync(Guid id) =>
            (await Host.Collection(PlatformCollections.OrganizationUnits).Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync())["Code"].AsString;

        public async Task<string> PositionCodeAsync(Guid id) =>
            (await Host.Collection(PlatformCollections.Positions).Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync())["Code"].AsString;

        public async Task<List<BsonDocument>> LedgerAsync() =>
            await Host.Collection("record_code_allocations").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

        public async Task<string> CounterSnapshotAsync() =>
            string.Join(";", (await Host.Collection("record_code_counters").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Select(c => $"{c["Prefix"]}-{c["RecordType"]}={c["LastNumber"]}").OrderBy(x => x, StringComparer.Ordinal));

        public async Task<Guid> SeedUnitAsync(string code, Guid company, Guid? tenant = null, bool deleted = false)
        {
            var unit = new OrganizationUnit
            {
                TenantId = tenant ?? TenantA, Code = code, Name = code, LegalEntityId = company, IsDeleted = deleted,
                DeletedAt = deleted ? DateTimeOffset.UtcNow : null
            };
            await Host.Database.GetCollection<OrganizationUnit>(PlatformCollections.OrganizationUnits).InsertOneAsync(unit);
            return unit.Id;
        }

        public async Task<Guid> SeedPositionAsync(string code, Guid unit)
        {
            var position = new Position { TenantId = TenantA, Code = code, Name = code, OrganizationUnitId = unit };
            await Host.Database.GetCollection<Position>(PlatformCollections.Positions).InsertOneAsync(position);
            return position.Id;
        }

        private async Task<OrganizationAnswer> SendAsync(HttpMethod method, string path, string token, string json) =>
            await OrganizationAnswer.ReadAsync(await Host.SendAsync(method, path, token, json));

        private static string UnitBody(string? code, Guid company, Guid? parent) =>
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["code"] = code, ["name"] = "Unit " + Guid.NewGuid().ToString("N")[..6], ["legalEntityId"] = company,
                ["parentOrganizationUnitId"] = parent
            });

        private static string PositionBody(string? code, Guid unit) =>
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["code"] = code, ["name"] = "Position " + Guid.NewGuid().ToString("N")[..6], ["organizationUnitId"] = unit
            });

        public void Dispose() => Host.Dispose();
    }

    internal sealed record OrganizationAnswer(int Status, string? Reason, Guid Id, bool IsEnvelope, string Raw)
    {
        public static async Task<OrganizationAnswer> ReadAsync(HttpResponseMessage response)
        {
            var raw = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(raw) ? "{}" : raw);
            var root = document.RootElement;
            var reason = root.TryGetProperty("reason_code", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            var id = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String
                     && Guid.TryParse(data.GetString(), out var parsed) ? parsed : Guid.Empty;
            return new OrganizationAnswer((int)response.StatusCode, reason, id, root.TryGetProperty("isSuccessful", out _), raw);
        }
    }

    /// <summary>The production unit repository, with the second-read stage of AC-51 / F-2 / F-15 (a) and the lost write of AC-54 (b).</summary>
    private sealed class StagedUnits(OrganizationUnitRepository inner, OrganizationHooks hooks) : IOrganizationUnitRepository
    {
        private int _reads;

        public async Task<OrganizationUnit> CreateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default)
        {
            if (hooks.FailNextRecordWrite)
            {
                hooks.FailNextRecordWrite = false;
                throw new RecordWriteLostException();
            }

            return await inner.CreateAsync(organizationUnit, ct);
        }

        public async Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var unit = await inner.GetByIdAsync(id, ct);
            if (hooks.SecondReadOf is not { } stage || stage.UnitId != id)
            {
                return unit;
            }

            hooks.ReadsOfTarget = ++_reads;
            if (_reads < 2 || unit is null)
            {
                return unit;
            }

            if (stage.Mode == UnitSecondRead.Deleted)
            {
                return null;
            }

            unit.IsArchived = true;
            return unit;
        }

        public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByCodeAsync(code, excludeId, ct);
        public Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default) => inner.UpdateAsync(organizationUnit, ct);
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
    }

    /// <summary>The production position repository, with the lost write of AC-54 (b).</summary>
    private sealed class StagedPositions(PositionRepository inner, OrganizationHooks hooks) : IPositionRepository
    {
        public async Task<Position> CreateAsync(Position position, CancellationToken ct = default)
        {
            if (hooks.FailNextRecordWrite)
            {
                hooks.FailNextRecordWrite = false;
                throw new RecordWriteLostException();
            }

            return await inner.CreateAsync(position, ct);
        }

        public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => inner.ExistsByCodeAsync(code, excludeId, ct);
        public Task UpdateAsync(Position position, CancellationToken ct = default) => inner.UpdateAsync(position, ct);
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public Task<IReadOnlyList<Position>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) => inner.GetByIdsAsync(ids, ct);
    }

    /// <summary>MDM's own two rules in front of the fake: <c>mdm.legal-entities.read</c> (else 403), and a company of another tenant is 404.</summary>
    private sealed class MdmRules : DelegatingHandler
    {
        private readonly IReadOnlyDictionary<Guid, Guid> _tenantOfCompany;

        public MdmRules(FakeMdm mdm, IReadOnlyDictionary<Guid, Guid> tenantOfCompany) : base(mdm) => _tenantOfCompany = tenantOfCompany;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bearer = request.Headers.Authorization?.Parameter;
            var permissions = bearer is null
                ? []
                : new JwtSecurityTokenHandler().ReadJwtToken(bearer).Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray();
            if (!permissions.Contains(MdmRead))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden) { Content = new StringContent("{}") });
            }

            var company = Guid.Parse(request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[2]);
            var tenant = request.Headers.TryGetValues("X-Tenant-Id", out var values) ? values.FirstOrDefault() : null;
            var owner = _tenantOfCompany.TryGetValue(company, out var owned) ? owned : TenantA;
            if (!string.Equals(tenant, owner.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("{}") });
            }

            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>The success path's door refusing (AC-54 d): what the canonical door throws when it cannot write the record.</summary>
    private sealed class RefusingDoor : ITransactionalAuditOutboxWriter
    {
        public Task<bool> TryEnqueueAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default) =>
            throw new TransactionOwnedAuditRefusedException("door refused (test)");
    }

    internal sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<CapturedLog> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, System.Collections.Concurrent.ConcurrentQueue<CapturedLog> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception));
        }
    }
}

/// <summary>A lost record write after the code was allocated — a <see cref="MongoException"/>, as the driver raises one (AC-54 b).</summary>
internal sealed class RecordWriteLostException() : MongoException("record write lost (test)");
