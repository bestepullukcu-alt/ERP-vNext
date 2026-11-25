using System.Collections.Concurrent;
using System.Text.Json;
using Diten.PvgService.Domain.RegPvBase;
using System.Text.Json.Nodes;
using Diten.PvgService.Api.Tests.Persistence;
using Diten.PvgService.Application.RegPvBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diten.PvgService.Api.Tests;

[Collection(PvgSharedMongoCollection.Name)]
public sealed class PvgCaseIntakeEditKeepsStoredDataTests(PvgMongoReplicaSetFixture mongo)
{
    private const string Subject = "case-reader-fake";
    private const string Route = PvgCaseIntakeTriageEndpoints.RoutePrefix;
    private static readonly string[] EditableFields =
    [
        "intakeChannel", "sourceType", "sourceReference", "receivedAtUtc", "reporterType",
        "reporterContactSummary", "patientSubjectCode", "eventOnsetDate", "adverseEventNarrative",
        "seriousness", "intakePriority", "evidenceLinkReferences", "productLines", "awarenessAtUtc",
        "companyRecipientPersonId", "eventCountry", "reportType", "reporterNameOrInitials",
        "reporterProfession", "reporterCountry"
    ];

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Mongo")]
    public async Task Get_by_id_returns_the_fifteen_stored_fields(string store)
    {
        await using var host = await StartHostAsync(store);
        var tenant = PvgMongoTestData.NewTenant();
        var input = CreateInput();
        var id = await CreateRoutedDraftAsync(host, tenant, input);
        var draft = await ReadDraftAsync(host, tenant, id);
        AssertRoutedValues(input, id, draft);
    }

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Mongo")]
    public async Task Get_by_id_refused_by_field_security_returns_none_of_the_fifteen_values(string store)
    {
        var policy = new DetailPolicy();
        await using var host = await StartHostAsync(store, services =>
            services.Replace(ServiceDescriptor.Singleton<IPvgFieldSecurityPolicy>(policy)));
        var tenant = PvgMongoTestData.NewTenant();
        var input = CreateInput();
        var id = await CreateRoutedDraftAsync(host, tenant, input);
        var allowed = await ReadDraftAsync(host, tenant, id);
        AssertRoutedValues(input, id, allowed);

        policy.RefuseDetail = true;
        var denied = await host.SendAsync(HttpMethod.Get, $"{Route}/{id}", tenant, Subject);
        Assert.Equal(409, denied.StatusCode);
        Assert.Equal("Blocked", denied.Body().Outcome);
        Assert.Equal(PvgPortDecision.FieldSecurityDenied().ReasonCode, denied.Body().ReasonCode);
        Assert.Null(denied.Body().Draft);
        var list = await host.SendAsync(HttpMethod.Get, Route, tenant, Subject);
        Assert.Equal(200, list.StatusCode);
        var item = Assert.Single(JsonNode.Parse(list.Raw)!["items"]!.AsArray())!.AsObject();
        Assert.Equal(id, item["intakeDraftId"]!.GetValue<string>());
        Assert.True(item.ContainsKey("status"));
        Assert.Equal(3L, item["version"]!.GetValue<long>());
        Assert.Equal(3, item.Count);

        foreach (var field in new[]
        {
            "intakeChannel", "sourceType", "sourceReference", "receivedAtUtc", "reporterType",
            "reporterContactSummary", "patientSubjectCode", "eventOnsetDate", "adverseEventNarrative",
            "seriousness", "intakePriority", "triageOutcome", "triageReason", "routeTargetQueue",
            "evidenceLinkReferences"
        })
        {
            foreach (var raw in new[] { denied.Raw, list.Raw })
            {
                Assert.DoesNotContain($"\"{field}\"", raw);
                if (allowed[field] is JsonArray references)
                {
                    foreach (var reference in references)
                    {
                        Assert.DoesNotContain(reference!.GetValue<string>(), raw);
                    }
                }
                else
                {
                    Assert.DoesNotContain(allowed[field]!.GetValue<string>(), raw);
                }
            }
        }
    }

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Mongo")]
    public async Task Update_with_the_values_read_back_keeps_every_stored_field(string store)
    {
        await using var host = await StartHostAsync(store);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        var before = await ReadDraftAsync(host, tenant, id);
        AssertOwnerFieldsAreNull(before);
        var version = before["version"]!.GetValue<long>();
        var put = new JsonObject { ["expectedVersion"] = version };
        foreach (var field in EditableFields)
        {
            Assert.True(before.ContainsKey(field), $"Item response is missing {field}.");
            put[field] = before[field]?.DeepClone();
        }
        var saved = await host.SendAsync(HttpMethod.Put, $"{Route}/{id}", tenant, Subject, put.ToJsonString());
        Assert.Equal(200, saved.StatusCode);
        var after = await ReadDraftAsync(host, tenant, id);
        AssertOwnerFieldsAreNull(after);
        Assert.Equal("IntakeUpdated", after["status"]!.GetValue<string>());
        Assert.Equal(version + 1, after["version"]!.GetValue<long>());
        foreach (var field in EditableFields.Concat(new[]
        {
            "intakeDraftId", "reporterSource", "reporterSourceId", "reporterCrmSearchState",
            "triageOutcome", "triageReason", "routeTargetQueue"
        }))
        {
            Assert.True(before.ContainsKey(field) && after.ContainsKey(field), $"Missing read-back field {field}.");
            Assert.True(JsonNode.DeepEquals(before[field], after[field]), $"Editing changed {field}.");
        }
    }


    [Theory]
    [InlineData("InMemory", "triage")]
    [InlineData("InMemory", "route")]
    [InlineData("InMemory", "both")]
    [InlineData("Mongo", "triage")]
    [InlineData("Mongo", "route")]
    [InlineData("Mongo", "both")]
    public async Task Update_of_a_triaged_or_routed_draft_is_refused_and_changes_nothing(string store, string transition)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        if (transition != "route") await TriageAsync(host, tenant, id, 1);
        if (transition != "triage")
        {
            var route = await host.SendAsync(HttpMethod.Post, $"{Route}/{id}/route", tenant, Subject,
                "{\"expectedVersion\":" + (transition == "both" ? 2 : 1) + ",\"routeTargetQueue\":\"queue-distinct-fake\"}");
            Assert.Equal(200, route.StatusCode);
        }
        var before = await SnapshotAsync(probe, tenant, id);
        probe.Reset();
        var response = await PutAsync(host, tenant, id, before.Version);
        AssertBlocked(response, "PVG_INTAKE_DRAFT_NOT_EDITABLE");
        await AssertUnchangedAsync(probe, tenant, id, before);
    }

    [Theory]
    [InlineData("InMemory", PvgIntakeStatus.Triaged)]
    [InlineData("InMemory", PvgIntakeStatus.RoutePending)]
    [InlineData("InMemory", PvgIntakeStatus.Draft)]
    [InlineData("InMemory", PvgIntakeStatus.TriagePending)]
    [InlineData("InMemory", PvgIntakeStatus.Routed)]
    [InlineData("Mongo", PvgIntakeStatus.Triaged)]
    [InlineData("Mongo", PvgIntakeStatus.RoutePending)]
    [InlineData("Mongo", PvgIntakeStatus.Draft)]
    [InlineData("Mongo", PvgIntakeStatus.TriagePending)]
    [InlineData("Mongo", PvgIntakeStatus.Routed)]
    public async Task Update_is_allowed_only_in_intake_created_and_intake_updated(string store, PvgIntakeStatus status)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        for (var version = 1; version <= 2; version++)
        {
            var allowed = await PutAsync(host, tenant, id, version);
            Assert.Equal(200, allowed.StatusCode);
            var current = await SnapshotAsync(probe, tenant, id);
            Assert.Equal(version + 1, current.Version);
            Assert.Equal(PvgIntakeStatus.IntakeUpdated, current.Intake.Status);
        }
        var before = await SeedAsync(probe, tenant, id, status);
        Assert.Null(before.Intake.TriageOutcome);
        Assert.Null(before.Intake.TriageReason);
        Assert.Null(before.Intake.RouteTargetQueue);
        probe.Reset();
        AssertBlocked(await PutAsync(host, tenant, id, before.Version), "PVG_INTAKE_DRAFT_NOT_EDITABLE");
        await AssertUnchangedAsync(probe, tenant, id, before);
    }

    [Theory]
    [InlineData("InMemory", "outcome")]
    [InlineData("InMemory", "reason")]
    [InlineData("InMemory", "queue")]
    [InlineData("Mongo", "outcome")]
    [InlineData("Mongo", "reason")]
    [InlineData("Mongo", "queue")]
    public async Task Update_never_erases_triage_or_route_fields(string store, string marker)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        var before = await SeedAsync(probe, tenant, id, PvgIntakeStatus.IntakeUpdated, marker);
        Assert.Equal(1, new object?[] { before.Intake.TriageOutcome, before.Intake.TriageReason, before.Intake.RouteTargetQueue }.Count(x => x is not null));
        probe.Reset();
        AssertBlocked(await PutAsync(host, tenant, id, before.Version), "PVG_INTAKE_DRAFT_NOT_EDITABLE");
        await AssertUnchangedAsync(probe, tenant, id, before);
    }

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Mongo")]
    public async Task Update_refusals_for_another_tenant_or_a_caller_without_the_key_never_say_not_editable(string store)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        await TriageAsync(host, tenant, id, 1);
        var before = await SnapshotAsync(probe, tenant, id);
        probe.Reset();
        var foreign = await PutAsync(host, PvgMongoTestData.NewTenant(), id, before.Version);
        AssertBlocked(foreign, "PVG_INTAKE_DRAFT_NOT_FOUND");
        Assert.DoesNotContain("PVG_INTAKE_DRAFT_NOT_EDITABLE", foreign.Raw);
        await AssertUnchangedAsync(probe, tenant, id, before);
        probe.Reset();
        probe.DenyPermission = true;
        var denied = await PutAsync(host, tenant, id, before.Version);
        AssertBlocked(denied, "PVG_PERMISSION_DENIED");
        Assert.DoesNotContain("PVG_INTAKE_DRAFT_NOT_EDITABLE", denied.Raw);
        Assert.Equal(0, probe.Store.ReadCalls);
        Assert.Equal(new[] { "permission" }, probe.Trace.ToArray());
        await AssertUnchangedAsync(probe, tenant, id, before);
    }

    [Theory]
    [InlineData("InMemory", "equal")]
    [InlineData("InMemory", "missing")]
    [InlineData("InMemory", "stale")]
    [InlineData("InMemory", "future")]
    [InlineData("InMemory", "evidence")]
    [InlineData("Mongo", "equal")]
    [InlineData("Mongo", "missing")]
    [InlineData("Mongo", "stale")]
    [InlineData("Mongo", "future")]
    [InlineData("Mongo", "evidence")]
    public async Task Update_requires_the_version_read_with_the_editability_snapshot(string store, string versionCase)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        Assert.Equal(200, (await PutAsync(host, tenant, id, 1)).StatusCode);
        var before = await SnapshotAsync(probe, tenant, id);
        long? version = versionCase switch { "stale" => before.Version - 1, "future" => before.Version + 1,
            "missing" or "evidence" => null, _ => before.Version };
        probe.Reset();
        probe.DenyEvidence = versionCase == "evidence";
        var response = await PutAsync(host, tenant, id, version);
        Assert.Equal(1, probe.Store.ReadCalls);
        if (versionCase == "equal")
        {
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(new long?[] { before.Version }, probe.Store.ReplaceVersions.ToArray());
            Assert.Equal(before.Version + 1, (await SnapshotAsync(probe, tenant, id)).Version);
        }
        else
        {
            AssertBlocked(response, versionCase == "evidence" ? PvgPortDecision.EvidenceLinkDenied().ReasonCode : "PVG_INTAKE_DRAFT_VERSION_CONFLICT");
            await AssertUnchangedAsync(probe, tenant, id, before);
        }
    }

    [Theory]
    [InlineData("InMemory")]
    [InlineData("Mongo")]
    public async Task Concurrent_triage_between_update_read_and_replace_is_not_overwritten(string store)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        var before = await SnapshotAsync(probe, tenant, id);
        probe.Reset();
        probe.Store.PauseNextRead = true;
        var update = PutAsync(host, tenant, id, before.Version);
        try
        {
            await probe.Store.Captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await TriageAsync(host, tenant, id, before.Version);
            var triaged = await SnapshotAsync(probe, tenant, id);
            Assert.Equal(2, triaged.Version);
            Assert.Equal(PvgTriageOutcome.Duplicate, triaged.Intake.TriageOutcome);
            probe.Store.Release.TrySetResult();
            AssertBlocked(await update.WaitAsync(TimeSpan.FromSeconds(10)), "PVG_INTAKE_DRAFT_VERSION_CONFLICT");
            Assert.Equal(Serialize(triaged), Serialize(await SnapshotAsync(probe, tenant, id)));
            Assert.Equal(new long?[] { 1, 1 }, probe.Store.ReplaceVersions.ToArray());
            Assert.Equal(2, probe.Store.ReadCalls);
        }
        finally
        {
            probe.Store.Release.TrySetResult();
            await update.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData("InMemory", "editable")]
    [InlineData("InMemory", "triaged")]
    [InlineData("InMemory", "routed")]
    [InlineData("InMemory", "linked")]
    [InlineData("InMemory", "manual-removal")]
    [InlineData("Mongo", "editable")]
    [InlineData("Mongo", "triaged")]
    [InlineData("Mongo", "routed")]
    [InlineData("Mongo", "linked")]
    [InlineData("Mongo", "manual-removal")]
    public async Task Field_policy_denial_does_not_disclose_draft_editability(string store, string scenario)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var id = await CreateDraftAsync(host, tenant, CreateInput());
        if (scenario == "triaged") await TriageAsync(host, tenant, id, 1);
        if (scenario == "routed")
            Assert.Equal(200, (await host.SendAsync(HttpMethod.Post, $"{Route}/{id}/route", tenant, Subject,
                "{\"expectedVersion\":1,\"routeTargetQueue\":\"policy-queue-fake\"}")).StatusCode);
        if (scenario == "linked") await SeedAsync(probe, tenant, id, PvgIntakeStatus.IntakeUpdated, linked: true);
        var before = await SnapshotAsync(probe, tenant, id);
        // These independent allow controls must execute even against the old policy ordering.
        if (scenario is "linked" or "manual-removal")
        {
            probe.Reset();
            var control = await PutAsync(host, tenant, id, before.Version, scenario == "manual-removal");
            Assert.Equal(400, control.StatusCode);
            Assert.Equal(new[] { scenario == "linked" ? "PVG_REPORTER_LINK_REMOVAL_REQUIRED" : "PVG_REPORTER_LINK_ABSENT" }, control.Body().ValidationReasonCodes);
            await AssertUnchangedAsync(probe, tenant, id, before);
        }
        probe.Reset();
        probe.DenyUpdate = true;
        var denied = await PutAsync(host, tenant, id, before.Version, scenario == "manual-removal");
        AssertBlocked(denied, PvgPortDecision.FieldSecurityDenied().ReasonCode);
        Assert.Equal(0, probe.Store.ReadCalls);
        Assert.Equal(new[] { "permission", "field:update:SourceReference" }, probe.Trace.ToArray());
        await AssertUnchangedAsync(probe, tenant, id, before);
        probe.Reset();
        probe.DenyUpdate = false;
        var allowed = await PutAsync(host, tenant, id, before.Version, scenario == "manual-removal");
        Assert.Equal(ExpectedUpdateTrace.Concat(new[] { "read" }).ToArray(),
            probe.Trace.Take(ExpectedUpdateTrace.Length + 1).ToArray());
        if (scenario is "triaged" or "routed") AssertBlocked(allowed, "PVG_INTAKE_DRAFT_NOT_EDITABLE");
        else if (scenario is "linked" or "manual-removal")
        {
            Assert.Equal(400, allowed.StatusCode);
            Assert.Equal(new[] { scenario == "linked" ? "PVG_REPORTER_LINK_REMOVAL_REQUIRED" : "PVG_REPORTER_LINK_ABSENT" }, allowed.Body().ValidationReasonCodes);
        }
        else Assert.Equal(200, allowed.StatusCode);
        if (scenario != "editable") await AssertUnchangedAsync(probe, tenant, id, before);
    }

    [Theory]
    [InlineData("InMemory", "editable")]
    [InlineData("InMemory", "closed")]
    [InlineData("InMemory", "absent")]
    [InlineData("InMemory", "foreign")]
    [InlineData("Mongo", "editable")]
    [InlineData("Mongo", "closed")]
    [InlineData("Mongo", "absent")]
    [InlineData("Mongo", "foreign")]
    public async Task Field_policy_denial_does_not_disclose_record_existence(string store, string scenario)
    {
        var probe = new Probe();
        await using var host = await StartProbedHostAsync(store, probe);
        var tenant = PvgMongoTestData.NewTenant();
        var foreignTenant = PvgMongoTestData.NewTenant();
        var editable = await CreateDraftAsync(host, tenant, CreateInput());
        var closed = await CreateDraftAsync(host, tenant, CreateInput());
        await TriageAsync(host, tenant, closed, 1);
        var foreign = await CreateDraftAsync(host, foreignTenant, CreateInput());
        var absent = Guid.NewGuid().ToString("N");
        var (id, version, allowedReason) = scenario switch
        {
            "editable" => (editable, 1L, (string?)null),
            "closed" => (closed, 2L, "PVG_INTAKE_DRAFT_NOT_EDITABLE"),
            "absent" => (absent, 1L, "PVG_INTAKE_DRAFT_NOT_FOUND"),
            _ => (foreign, 1L, "PVG_INTAKE_DRAFT_NOT_FOUND")
        };
        var originals = new[] { await SnapshotAsync(probe, tenant, editable), await SnapshotAsync(probe, tenant, closed),
            await SnapshotAsync(probe, foreignTenant, foreign) };
        probe.Reset();
        probe.DenyUpdate = true;
        // Start with this independent scenario so absent-ID reason failures cannot be masked by an existing-ID read count.
        var denied = await PutAsync(host, tenant, id, version);
        AssertBlocked(denied, PvgPortDecision.FieldSecurityDenied().ReasonCode);
        Assert.Equal(0, probe.Store.ReadCalls);
        Assert.Equal(0, probe.Store.WriteCalls);
        Assert.Equal(new[] { "permission", "field:update:SourceReference" }, probe.Trace.ToArray());
        probe.Reset();
        var editableControl = await PutAsync(host, tenant, editable, 1);
        AssertBlocked(editableControl, PvgPortDecision.FieldSecurityDenied().ReasonCode);
        Assert.Equal(denied.Raw, editableControl.Raw);
        Assert.Equal(0, probe.Store.ReadCalls);
        Assert.Equal(0, probe.Store.WriteCalls);
        Assert.Equal(Serialize(originals[0]), Serialize(await SnapshotAsync(probe, tenant, editable)));
        Assert.Equal(Serialize(originals[1]), Serialize(await SnapshotAsync(probe, tenant, closed)));
        Assert.Equal(Serialize(originals[2]), Serialize(await SnapshotAsync(probe, foreignTenant, foreign)));
        probe.Reset();
        probe.DenyUpdate = false;
        var response = await PutAsync(host, tenant, id, version);
        Assert.Equal(ExpectedUpdateTrace.Concat(new[] { "read" }).ToArray(),
            probe.Trace.Take(ExpectedUpdateTrace.Length + 1).ToArray());
        if (allowedReason is null) Assert.Equal(200, response.StatusCode);
        else
        {
            AssertBlocked(response, allowedReason);
            Assert.Equal(0, probe.Store.WriteCalls);
            Assert.Equal(Serialize(originals[0]), Serialize(await SnapshotAsync(probe, tenant, editable)));
            Assert.Equal(Serialize(originals[1]), Serialize(await SnapshotAsync(probe, tenant, closed)));
            Assert.Equal(Serialize(originals[2]), Serialize(await SnapshotAsync(probe, foreignTenant, foreign)));
        }
    }

    private static readonly string[] ExpectedUpdateTrace = new[] { "permission" }.Concat(new[]
    {
        "SourceReference", "ReceivedAtUtc", "ReporterContactSummary", "PatientSubjectCode", "EventOnsetDate",
        "AdverseEventNarrative", "Seriousness", "IntakePriority", "TriageOutcome", "TriageReason", "RouteTargetQueue",
        "EvidenceLinkReferences", "ProductLines", "ProductRole", "GlobalProductId", "ProductNameAsReported",
        "PresentationGskuId", "LotNumber", "AwarenessAtUtc", "CompanyRecipientPersonId", "EventCountry", "ReportType",
        "ReporterSelectionId", "ReporterSourceId", "ReporterNameOrInitials", "ReporterProfession", "ReporterCountry"
    }.Select(field => "field:update:" + field)).ToArray();

    private Task<PvgMongoApiHost> StartProbedHostAsync(string store, Probe probe) => StartHostAsync(store, services =>
    {
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPvgIntakeDraftStore));
        services.Remove(descriptor);
        services.AddSingleton<IPvgIntakeDraftStore>(provider =>
        {
            var inner = descriptor.ImplementationInstance as IPvgIntakeDraftStore
                ?? (descriptor.ImplementationFactory is { } factory
                    ? (IPvgIntakeDraftStore)factory(provider)
                    : (IPvgIntakeDraftStore)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!));
            probe.Store = new TracedStore(inner, probe.Trace);
            return probe.Store;
        });
        services.Replace(ServiceDescriptor.Singleton<IPvgPermissionGate>(probe));
        services.Replace(ServiceDescriptor.Singleton<IPvgFieldSecurityPolicy>(probe));
        services.Replace(ServiceDescriptor.Singleton<IPvgEvidenceLinkPort>(probe));
    });

    private static async Task<PvgPersistedIntakeDraft> SnapshotAsync(Probe probe, string tenant, string id) =>
        Assert.IsType<PvgPersistedIntakeDraft>(await probe.Store.Inner.FindByIdAsync(new(tenant), id));

    private static string Serialize(PvgPersistedIntakeDraft value) => JsonSerializer.Serialize(value);

    private static async Task AssertUnchangedAsync(Probe probe, string tenant, string id, PvgPersistedIntakeDraft before)
    {
        Assert.Equal(0, probe.Store.WriteCalls);
        Assert.Equal(Serialize(before), Serialize(await SnapshotAsync(probe, tenant, id)));
    }

    private static async Task<PvgPersistedIntakeDraft> SeedAsync(Probe probe, string tenant, string id,
        PvgIntakeStatus status, string? marker = null, bool linked = false)
    {
        var before = await SnapshotAsync(probe, tenant, id);
        var old = before.Intake;
        var seeded = new SafetyCaseIntake(tenant, status, old.IntakeChannel, old.SourceType, old.ReceivedAtUtc,
            old.ReporterType, old.AdverseEventNarrative, old.Seriousness, old.IntakePriority,
            marker == "outcome" ? PvgTriageOutcome.Duplicate : null,
            marker == "reason" ? "marker-reason-fake" : null,
            marker == "queue" ? "marker-queue-fake" : null)
        {
            SourceReference = old.SourceReference, ReporterContactSummary = old.ReporterContactSummary,
            PatientSubjectCode = old.PatientSubjectCode, EventOnsetDate = old.EventOnsetDate,
            EvidenceLinkReferences = old.EvidenceLinkReferences, ProductLines = old.ProductLines,
            AwarenessAtUtc = old.AwarenessAtUtc, CompanyRecipientPersonId = old.CompanyRecipientPersonId,
            EventCountry = old.EventCountry, ReportType = old.ReportType,
            ReporterSource = linked ? PvgReporterSource.Crm : old.ReporterSource,
            ReporterSourceId = linked ? "linked-reporter-fake" : old.ReporterSourceId,
            ReporterCrmSearchState = linked ? PvgReporterCrmSearchState.Searched : old.ReporterCrmSearchState,
            ReporterNameOrInitials = old.ReporterNameOrInitials, ReporterProfession = old.ReporterProfession,
            ReporterCountry = old.ReporterCountry
        };
        Assert.Equal(PvgIntakeDraftReplaceOutcome.Replaced,
            (await probe.Store.Inner.ReplaceAsync(new(tenant), id, before.Version, Subject, seeded)).Outcome);
        return await SnapshotAsync(probe, tenant, id);
    }

    private static Task<PvgMongoApiHost.ApiResponse> PutAsync(PvgMongoApiHost host, string tenant, string id,
        long? version, bool removeReporter = false)
    {
        var input = CreateInput();
        input["expectedVersion"] = version;
        if (removeReporter) input["removeReporterLink"] = true;
        return host.SendAsync(HttpMethod.Put, $"{Route}/{id}", tenant, Subject, input.ToJsonString());
    }

    private static async Task TriageAsync(PvgMongoApiHost host, string tenant, string id, long version)
    {
        var response = await host.SendAsync(HttpMethod.Post, $"{Route}/{id}/triage", tenant, Subject,
            "{\"expectedVersion\":" + version + ",\"triageOutcome\":\"Duplicate\",\"triageReasonCode\":\"PVG_TRIAGE_REASON_DUPLICATE\",\"triageReason\":\"concurrent-triage-reason-fake\"}");
        Assert.Equal(200, response.StatusCode);
    }

    private static void AssertBlocked(PvgMongoApiHost.ApiResponse response, string reason)
    {
        Assert.Equal(409, response.StatusCode);
        Assert.Equal("Blocked", response.Body().Outcome);
        Assert.Equal(reason, response.Body().ReasonCode);
        Assert.Empty(response.Body().ValidationReasonCodes);
        Assert.Null(response.Body().IntakeDraftId);
        Assert.Null(response.Body().Draft);
        foreach (var value in new[] { "contact-fake", "patient-fake-1", "narrative-fake", "linked-reporter-fake",
            "marker-reason-fake", "marker-queue-fake", "concurrent-triage-reason-fake" })
            Assert.DoesNotContain(value, response.Raw);
    }

    private sealed class Probe : IPvgPermissionGate, IPvgFieldSecurityPolicy, IPvgEvidenceLinkPort
    {
        public TracedStore Store { get; set; } = null!;
        public ConcurrentQueue<string> Trace { get; } = new();
        public bool DenyPermission { get; set; }
        public bool DenyUpdate { get; set; }
        public bool DenyEvidence { get; set; }
        public void Reset() { Trace.Clear(); Store.Reset(); }
        public ValueTask<PvgPermissionDecision> EvaluateAsync(PvgPermissionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Operation == PvgIntakeOperation.Update) Trace.Enqueue("permission");
            return ValueTask.FromResult(DenyPermission && request.Operation == PvgIntakeOperation.Update
                ? PvgPermissionDecision.Denied("PVG_PERMISSION_DENIED") : PvgPermissionDecision.Allowed());
        }
        public ValueTask<PvgPortDecision> EvaluateAsync(PvgFieldSecurityRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Operation == PvgIntakeOperation.Update) Trace.Enqueue($"field:{request.Surface}:{request.FieldName}");
            return ValueTask.FromResult(DenyUpdate && request.Operation == PvgIntakeOperation.Update
                ? PvgPortDecision.FieldSecurityDenied() : new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
        }
        public ValueTask<PvgPortDecision> EvaluateAsync(PvgEvidenceLinkRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Operation == PvgIntakeOperation.Update) Trace.Enqueue("evidence");
            return ValueTask.FromResult(DenyEvidence && request.Operation == PvgIntakeOperation.Update
                ? PvgPortDecision.EvidenceLinkDenied() : new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
        }
    }

    private sealed class TracedStore(IPvgIntakeDraftStore inner, ConcurrentQueue<string> trace) : IPvgIntakeDraftStore
    {
        public IPvgIntakeDraftStore Inner => inner;
        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }
        public ConcurrentQueue<long?> ReplaceVersions { get; } = new();
        public bool PauseNextRead { get; set; }
        public TaskCompletionSource Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Reset() { ReadCalls = 0; WriteCalls = 0; ReplaceVersions.Clear(); }
        public ValueTask<string> AddAsync(PvgPersistenceTenantScope scope, string actorId, SafetyCaseIntake intake, CancellationToken cancellationToken = default)
        { WriteCalls++; return inner.AddAsync(scope, actorId, intake, cancellationToken); }
        public ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(PvgPersistenceTenantScope scope, string id, long? expectedVersion,
            string actorId, SafetyCaseIntake intake, CancellationToken cancellationToken = default)
        {
            WriteCalls++; ReplaceVersions.Enqueue(expectedVersion); trace.Enqueue("replace:" + expectedVersion);
            return inner.ReplaceAsync(scope, id, expectedVersion, actorId, intake, cancellationToken);
        }
        public async ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(PvgPersistenceTenantScope scope, string id, CancellationToken cancellationToken = default)
        {
            ReadCalls++; trace.Enqueue("read");
            var pause = PauseNextRead;
            PauseNextRead = false;
            var snapshot = await inner.FindByIdAsync(scope, id, cancellationToken);
            if (pause)
            {
                Captured.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return snapshot;
        }
        public ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(PvgPersistenceListScope scope, CancellationToken cancellationToken = default) =>
            inner.ListAsync(scope, cancellationToken);
        public ValueTask<int> CountAsync(PvgPersistenceTenantScope scope, PvgIntakeStatus? status, CancellationToken cancellationToken = default) =>
            inner.CountAsync(scope, status, cancellationToken);
    }

    private Task<PvgMongoApiHost> StartHostAsync(string store, Action<IServiceCollection>? configure = null)
    {
        var configuration = store == "Mongo"
            ? PvgMongoApiHost.Configuration(store, mongo.ConnectionString, PvgMongoTestHarness.SharedDatabaseName)
            : PvgMongoApiHost.Configuration(store, null, null);
        return PvgMongoApiHost.StartAsync(configuration, configure);
    }

    private static JsonObject CreateInput()
    {
        var input = JsonNode.Parse(PvgMongoApiHost.CreateJson)!.AsObject();
        input["reporterCountry"] = "unknown";
        input["companyRecipientPersonId"] = null;
        var product = input["productLines"]![0]!.AsObject();
        product["globalProductId"] = null;
        product["presentationGskuId"] = null;
        input["evidenceLinkReferences"] = new JsonArray("evidence-first-fake", "evidence-second-fake");

        return input;
    }

    private static async Task<string> CreateDraftAsync(PvgMongoApiHost host, string tenant, JsonObject input)
    {
        var create = await host.SendAsync(HttpMethod.Post, Route, tenant, Subject, input.ToJsonString());
        Assert.Equal(200, create.StatusCode);
        return Assert.IsType<string>(create.Body().IntakeDraftId);
    }

    private static async Task<string> CreateRoutedDraftAsync(PvgMongoApiHost host, string tenant, JsonObject input)
    {
        var id = await CreateDraftAsync(host, tenant, input);
        var itemPath = $"{Route}/{id}";
        const string triageBody = """
            {"expectedVersion":1,"triageOutcome":"Duplicate","triageReasonCode":"PVG_TRIAGE_REASON_DUPLICATE","triageReason":"reason-fake"}
            """;
        var triage = await host.SendAsync(HttpMethod.Post, $"{itemPath}/triage", tenant, Subject, triageBody);
        Assert.Equal(200, triage.StatusCode);
        const string routeBody = """{"expectedVersion":2,"routeTargetQueue":"queue-fake"}""";
        var routed = await host.SendAsync(HttpMethod.Post, $"{itemPath}/route", tenant, Subject, routeBody);
        Assert.Equal(200, routed.StatusCode);
        return id;
    }

    private static async Task<JsonObject> ReadDraftAsync(PvgMongoApiHost host, string tenant, string id)
    {
        var response = await host.SendAsync(HttpMethod.Get, $"{Route}/{id}", tenant, Subject);
        Assert.Equal(200, response.StatusCode);
        var draft = JsonNode.Parse(response.Raw)!["draft"]!.AsObject();
        Assert.Equal(id, draft["intakeDraftId"]!.GetValue<string>());
        return draft;
    }

    private static void AssertOwnerFieldsAreNull(JsonObject draft)
    {
        Assert.True(draft.ContainsKey("companyRecipientPersonId"));
        Assert.Null(draft["companyRecipientPersonId"]);
        var storedProduct = Assert.Single(draft["productLines"]!.AsArray())!.AsObject();
        Assert.True(storedProduct.ContainsKey("globalProductId"));
        Assert.Null(storedProduct["globalProductId"]);
        Assert.True(storedProduct.ContainsKey("presentationGskuId"));
        Assert.Null(storedProduct["presentationGskuId"]);
    }

    private static void AssertRoutedValues(JsonObject input, string id, JsonObject draft)
    {
        Assert.Equal(id, draft["intakeDraftId"]!.GetValue<string>());
        Assert.Equal(3L, draft["version"]!.GetValue<long>());
        AssertOwnerFieldsAreNull(draft);
        foreach (var name in new[]
        {
            "intakeChannel", "sourceType", "sourceReference", "reporterType", "reporterContactSummary",
            "patientSubjectCode", "eventOnsetDate", "adverseEventNarrative", "seriousness", "intakePriority",
            "evidenceLinkReferences"
        })
        {
            Assert.True(draft.ContainsKey(name), $"Item response is missing {name}.");
            Assert.True(JsonNode.DeepEquals(input[name], draft[name]), $"Stored value differs for {name}.");
        }

        Assert.Equal(input["receivedAtUtc"]!.GetValue<DateTimeOffset>(), draft["receivedAtUtc"]!.GetValue<DateTimeOffset>());
        Assert.True(JsonNode.DeepEquals(JsonValue.Create("Duplicate"), draft["triageOutcome"]), "Stored triage outcome must be the enum name Duplicate.");
        Assert.Equal("reason-fake", draft["triageReason"]!.GetValue<string>());
        Assert.Equal("queue-fake", draft["routeTargetQueue"]!.GetValue<string>());
    }

    private sealed class DetailPolicy : IPvgFieldSecurityPolicy
    {
        public bool RefuseDetail { get; set; }

        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgFieldSecurityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(RefuseDetail && request.Surface == "detail"
                ? PvgPortDecision.FieldSecurityDenied()
                : new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }
}
