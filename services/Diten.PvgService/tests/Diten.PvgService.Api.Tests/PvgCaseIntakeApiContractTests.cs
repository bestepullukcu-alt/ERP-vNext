using System.Text.Json;
using Diten.PvgService.Api;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.PvgService.Api.Tests;

// WP-0230-DL4A (MOD-0230 DL-4 part A): AC-7 (validation after the permission gate), AC-19 (record version transport,
// list total / filtered counts) and AC-31 (API request / response records carry the revision 04 fields).
// The application service is built directly with the real in-memory store and file-local ports; the endpoint
// methods are called statically over a DefaultHttpContext and their IResult is executed to read status and body.
public sealed class PvgCaseIntakeApiContractTests
{
    private const string TenantA = "tenant-a-fake";
    private const string TenantB = "tenant-b-fake";
    private const string LinkedCrmId = "crm-contact-secret-1";
    private const string LinkedPersonId = "person-secret-1";
    private const string PermissionDenied = "PVG_PERMISSION_DENIED";

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset FixtureReceivedAtUtc = new(2020, 3, 1, 8, 0, 0, TimeSpan.Zero);

    // ---- AC-19 (a): record version transport -------------------------------------------------------------------

    [Fact]
    public async Task Put_with_stale_version_returns_409_version_conflict()
    {
        var harness = new Harness();
        var id = await harness.CreateValidAsync();
        Assert.Equal(1, (await harness.StoredAsync(id)).Version);

        var first = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(1, ("reporterNameOrInitials", "A.A."))));
        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.Equal(2, first.Body.Version);

        var stale = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(1, ("reporterNameOrInitials", "B.B."))));
        AssertVersionConflict(stale);
        var stored = await harness.StoredAsync(id);
        Assert.Equal(2, stored.Version);
        Assert.Equal("A.A.", stored.Intake.ReporterNameOrInitials);

        var withoutVersion = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(null, ("reporterNameOrInitials", "C.C."))));
        AssertVersionConflict(withoutVersion);
        Assert.Equal("A.A.", (await harness.StoredAsync(id)).Intake.ReporterNameOrInitials);
    }

    [Theory]
    [InlineData("Put")]
    [InlineData("Triage")]
    [InlineData("Route")]
    public async Task Put_triage_and_route_with_current_version_succeed_and_return_new_version(string operation)
    {
        var harness = new Harness();
        var id = await harness.CreateValidAsync();

        var result = operation switch
        {
            "Put" => await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(1))),
            "Triage" => await harness.TriageAsync(
                id,
                new PvgCaseIntakeTriageRequest(PvgTriageOutcome.Triaged, null, "triage-reason-fake", 1)),
            _ => await harness.RouteAsync(id, new PvgCaseIntakeRouteRequest("queue-fake", 1))
        };

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("Succeeded", result.Body.Outcome);
        Assert.Equal(2, result.Body.Version);
        Assert.Equal(id, result.Body.IntakeDraftId);
        var fetched = await harness.GetAsync(id);
        Assert.Equal(2, fetched.Body.Draft!.Version);
        Assert.Equal(2, Assert.Single(fetched.Body.Items).Version);
    }

    [Theory]
    [InlineData("Triage")]
    [InlineData("Route")]
    public async Task Triage_and_route_with_stale_version_return_409_version_conflict(string operation)
    {
        var harness = new Harness();
        var id = await harness.CreateValidAsync();
        Assert.Equal(StatusCodes.Status200OK, (await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(1)))).StatusCode);

        var stale = operation == "Triage"
            ? await harness.TriageAsync(id, new PvgCaseIntakeTriageRequest(PvgTriageOutcome.Triaged, null, null, 1))
            : await harness.RouteAsync(id, new PvgCaseIntakeRouteRequest("queue-fake", 1));

        AssertVersionConflict(stale);
        var stored = await harness.StoredAsync(id);
        Assert.Equal(2, stored.Version);
        Assert.Equal(PvgIntakeStatus.IntakeUpdated, stored.Intake.Status);
        Assert.Null(stored.Intake.RouteTargetQueue);
    }

    // ---- AC-19 (b): total and filtered counts ------------------------------------------------------------------

    [Fact]
    public async Task List_with_101_drafts_returns_second_page_and_total_counts()
    {
        var harness = new Harness();
        for (var index = 0; index < 101; index++)
        {
            await harness.Store.AddAsync(
                new PvgPersistenceTenantScope(TenantA),
                StoredIntake(TenantA, PvgIntakeStatus.IntakeCreated, FixtureReceivedAtUtc.AddMinutes(index)));
        }

        var second = await harness.ListAsync(TenantA, pageNumber: 2, pageSize: 100);
        Assert.Equal(StatusCodes.Status200OK, second.StatusCode);
        Assert.Single(second.Body.Items);
        Assert.Equal(101, second.Body.TotalCount);
        Assert.Equal(101, second.Body.FilteredCount);

        var first = await harness.ListAsync(TenantA, pageNumber: 1, pageSize: 100);
        Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
        Assert.Equal(100, first.Body.Items.Count);
        Assert.Equal(101, first.Body.TotalCount);
        Assert.Equal(101, first.Body.FilteredCount);

        foreach (var item in first.Body.Items.Concat(second.Body.Items))
        {
            Assert.Equal((await harness.StoredAsync(item.IntakeDraftId)).Version, item.Version);
        }
    }

    [Fact]
    public async Task List_with_status_filter_returns_filtered_count()
    {
        var harness = new Harness();
        var firstId = await harness.CreateValidAsync();
        await harness.CreateValidAsync();
        await harness.CreateValidAsync();
        Assert.Equal(StatusCodes.Status200OK, (await harness.UpdateAsync(firstId, UpdateFromJson(UpdateBody(1)))).StatusCode);

        var updated = await harness.ListAsync(TenantA, status: PvgIntakeStatus.IntakeUpdated);
        Assert.Equal(StatusCodes.Status200OK, updated.StatusCode);
        Assert.Single(updated.Body.Items);
        Assert.Equal(1, updated.Body.FilteredCount);
        Assert.Equal(3, updated.Body.TotalCount);

        var created = await harness.ListAsync(TenantA, status: PvgIntakeStatus.IntakeCreated);
        Assert.Equal(2, created.Body.Items.Count);
        Assert.Equal(2, created.Body.FilteredCount);
        Assert.Equal(3, created.Body.TotalCount);
    }

    [Fact]
    public async Task List_counts_only_the_callers_tenant()
    {
        var harness = new Harness();
        var scopeA = new PvgPersistenceTenantScope(TenantA);
        var scopeB = new PvgPersistenceTenantScope(TenantB);
        for (var index = 0; index < 2; index++)
        {
            await harness.Store.AddAsync(scopeA, StoredIntake(TenantA, PvgIntakeStatus.IntakeCreated, FixtureReceivedAtUtc));
        }

        for (var index = 0; index < 3; index++)
        {
            await harness.Store.AddAsync(scopeB, StoredIntake(TenantB, PvgIntakeStatus.IntakeCreated, FixtureReceivedAtUtc));
        }

        var listA = await harness.ListAsync(TenantA);
        Assert.Equal(2, listA.Body.TotalCount);
        Assert.Equal(2, listA.Body.FilteredCount);
        Assert.Equal(2, listA.Body.Items.Count);
        var idsOfA = (await harness.Store.ListAsync(new PvgPersistenceListScope(scopeA, 1, 100, null)))
            .Select(draft => draft.IntakeDraftId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(idsOfA, listA.Body.Items.Select(item => item.IntakeDraftId).Order(StringComparer.Ordinal).ToArray());

        var listB = await harness.ListAsync(TenantB);
        Assert.Equal(3, listB.Body.TotalCount);
        Assert.Equal(3, listB.Body.FilteredCount);
        Assert.Equal(3, listB.Body.Items.Count);
    }

    // ---- AC-31: revision 04 fields on the API records ----------------------------------------------------------

    [Fact]
    public async Task Post_with_revision04_fields_round_trips_codes_and_ids()
    {
        var harness = new Harness();
        var body = ValidBody(
            ("awarenessAtUtc", "2020-03-01T11:00:00+03:00"),
            ("eventCountry", "unknown"),
            ("reportType", "Literature"),
            ("reporterNameOrInitials", "J.D."),
            ("reporterProfession", "Pharmacist"),
            ("reporterCountry", "unknown"),
            ("productLines", new object[]
            {
                Line("Suspect", "product-fake-a", "lot-a"),
                Line("Concomitant", "product-fake-b", "lot-b")
            }));

        var created = await harness.CreateAsync(CreateFromJson(Json(body)));

        Assert.Equal(StatusCodes.Status200OK, created.StatusCode);
        var id = Assert.IsType<string>(created.Body.IntakeDraftId);
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Null(created.Body.Version);

        var fetched = await harness.GetAsync(id);
        Assert.Equal(StatusCodes.Status200OK, fetched.StatusCode);
        var draft = Assert.IsType<PvgCaseIntakeDraftResponse>(fetched.Body.Draft);
        Assert.Equal(id, draft.IntakeDraftId);
        Assert.Equal(1, draft.Version);
        Assert.Equal(2, draft.ProductLines.Count);
        Assert.Equal(new PvgIntakeProductLine("Suspect", null, "product-fake-a", null, "lot-a"), draft.ProductLines[0]);
        Assert.Equal(new PvgIntakeProductLine("Concomitant", null, "product-fake-b", null, "lot-b"), draft.ProductLines[1]);
        Assert.Equal(FixtureReceivedAtUtc, draft.AwarenessAtUtc);
        Assert.Equal(TimeSpan.Zero, draft.AwarenessAtUtc.Offset);
        Assert.Equal("unknown", draft.EventCountry);
        Assert.Equal("Literature", draft.ReportType);
        Assert.Equal("J.D.", draft.ReporterNameOrInitials);
        Assert.Equal("Pharmacist", draft.ReporterProfession);
        Assert.Equal("unknown", draft.ReporterCountry);
        Assert.Equal("Manual", draft.ReporterSource);
        Assert.Null(draft.ReporterSourceId);
        Assert.Equal("Pending", draft.ReporterCrmSearchState);
        Assert.Contains("\"reporterSource\":\"Manual\"", fetched.Raw, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("validityResult")]
    [InlineData("validityMissingCriteria")]
    [InlineData("validityResult-json-null")]
    [InlineData("validityResult-direct-null")]
    public async Task Post_with_client_validity_result_is_rejected(string row)
    {
        var harness = new Harness();

        switch (row)
        {
            case "validityResult":
            case "validityMissingCriteria":
            {
                object value = row == "validityResult" ? "Valid" : new[] { "X" };
                var rejected = await harness.CreateAsync(CreateFromJson(Json(ValidBody((row, value)))));
                AssertRejected(rejected, PvgValidationReasonCodes.FieldValueInvalid);
                Assert.Equal(0, await harness.CountAsync());
                break;
            }
            case "validityResult-json-null":
            {
                // JSON null is "not sent".
                var accepted = await harness.CreateAsync(CreateFromJson(Json(ValidBody(("validityResult", null)))));
                Assert.Equal(StatusCodes.Status200OK, accepted.StatusCode);
                Assert.Equal(1, await harness.CountAsync());
                break;
            }
            default:
            {
                // Independent of how the framework binds a JSON null: a carried element of kind Null is "not sent".
                var request = CreateFromJson(Json(ValidBody())) with { ValidityResult = JsonDocument.Parse("null").RootElement };
                Assert.True(request.ValidityResult.HasValue);
                Assert.Equal(JsonValueKind.Null, request.ValidityResult!.Value.ValueKind);
                var accepted = await harness.CreateAsync(request);
                Assert.Equal(StatusCodes.Status200OK, accepted.StatusCode);
                Assert.Equal(1, await harness.CountAsync());
                break;
            }
        }
    }

    [Theory]
    [InlineData("source-string")]
    [InlineData("source-number")]
    [InlineData("source-id")]
    public async Task Post_with_reporter_source_or_source_id_is_rejected(string row)
    {
        var harness = new Harness();
        var (name, value) = row switch
        {
            "source-string" => ("reporterSource", (object)"Crm"),
            "source-number" => ("reporterSource", 1),
            _ => ("reporterSourceId", LinkedCrmId)
        };

        var rejected = await harness.CreateAsync(CreateFromJson(Json(ValidBody((name, value)))));

        AssertRejected(rejected, PvgValidationReasonCodes.FieldValueInvalid);
        Assert.DoesNotContain(LinkedCrmId, rejected.Raw, StringComparison.Ordinal);
        Assert.Equal(0, await harness.CountAsync());
    }

    [Fact]
    public async Task Post_with_reporter_crm_search_state_is_rejected()
    {
        var harness = new Harness();

        var rejected = await harness.CreateAsync(CreateFromJson(Json(ValidBody(("reporterCrmSearchState", "Searched")))));

        AssertRejected(rejected, PvgValidationReasonCodes.FieldValueInvalid);
        Assert.Equal(0, await harness.CountAsync());
    }

    [Theory]
    [InlineData("directory-only")]
    [InlineData("id-only")]
    public async Task Post_with_only_one_reporter_selection_field_is_rejected(string row)
    {
        var harness = new Harness();
        var selection = row == "directory-only"
            ? ("reporterSelectionDirectory", (object?)"Crm")
            : ("reporterSelectionId", LinkedCrmId);

        var rejected = await harness.CreateAsync(CreateFromJson(Json(ValidBody(selection))));

        AssertRejected(rejected, PvgValidationReasonCodes.ReporterSelectionIncomplete);
        Assert.DoesNotContain(LinkedCrmId, rejected.Raw, StringComparison.Ordinal);
        Assert.Equal(0, await harness.CountAsync());
    }

    [Theory]
    [InlineData("directory-only")]
    [InlineData("id-only")]
    [InlineData("both")]
    public async Task Put_with_remove_reporter_link_and_selection_pair_is_rejected(string row)
    {
        var harness = new Harness();
        var id = await harness.AddLinkedAsync(PvgReporterSource.Crm, LinkedCrmId);
        var before = await harness.StoredAsync(id);
        var overrides = new List<(string, object?)> { ("removeReporterLink", true) };
        if (row is "directory-only" or "both")
        {
            overrides.Add(("reporterSelectionDirectory", "Crm"));
        }

        if (row is "id-only" or "both")
        {
            overrides.Add(("reporterSelectionId", LinkedCrmId));
        }

        var rejected = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(before.Version, overrides.ToArray())));

        AssertRejected(rejected, PvgValidationReasonCodes.ReporterLinkRemovalWithSelection);
        Assert.DoesNotContain(LinkedCrmId, rejected.Raw, StringComparison.Ordinal);
        await AssertLinkedUnchangedAsync(harness, id, before.Version, PvgReporterSource.Crm, LinkedCrmId);
    }

    [Theory]
    [InlineData(PvgReporterSource.Crm)]
    [InlineData(PvgReporterSource.PersonDirectory)]
    public async Task Put_with_remove_reporter_link_round_trips_manual_source(PvgReporterSource source)
    {
        var harness = new Harness();
        var sourceId = source == PvgReporterSource.Crm ? LinkedCrmId : LinkedPersonId;
        var id = await harness.AddLinkedAsync(source, sourceId);
        var before = await harness.StoredAsync(id);

        var updated = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(before.Version, ("removeReporterLink", true))));

        Assert.Equal(StatusCodes.Status200OK, updated.StatusCode);
        Assert.Equal(before.Version + 1, updated.Body.Version);
        Assert.DoesNotContain(sourceId, updated.Raw, StringComparison.Ordinal);
        var fetched = await harness.GetAsync(id);
        var draft = Assert.IsType<PvgCaseIntakeDraftResponse>(fetched.Body.Draft);
        Assert.Equal("Manual", draft.ReporterSource);
        Assert.Null(draft.ReporterSourceId);
        Assert.Equal("ClosedByUserUnlink", draft.ReporterCrmSearchState);
        Assert.Equal("J.D.", draft.ReporterNameOrInitials);
        Assert.Equal("Pharmacist", draft.ReporterProfession);
        Assert.DoesNotContain(sourceId, fetched.Raw, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("reporter-name", "Create")]
    [InlineData("reporter-name", "Update")]
    [InlineData("product-name", "Create")]
    [InlineData("product-name", "Update")]
    [InlineData("lot-number", "Create")]
    [InlineData("lot-number", "Update")]
    [InlineData("event-country", "Create")]
    [InlineData("event-country", "Update")]
    [InlineData("report-type", "Create")]
    [InlineData("report-type", "Update")]
    [InlineData("company-recipient", "Create")]
    [InlineData("company-recipient", "Update")]
    public async Task Validation_failure_on_revision04_field_returns_code_without_value(string row, string operation)
    {
        var harness = new Harness();
        var (name, value, marker, expectedCode) = row switch
        {
            "reporter-name" => ("reporterNameOrInitials", (object)Padded("reporter-secret-", 129), "reporter-secret-", PvgValidationReasonCodes.FieldValueInvalid),
            "product-name" => (
                "productLines",
                new object[] { Line("Suspect", Padded("product-secret-", 513), "lot-1") },
                "product-secret-",
                PvgValidationReasonCodes.FieldValueInvalid),
            "lot-number" => (
                "productLines",
                new object[] { Line("Suspect", "product-fake-a", Padded("lot-secret-", 65)) },
                "lot-secret-",
                PvgValidationReasonCodes.FieldValueInvalid),
            "event-country" => ("eventCountry", "country-secret", "country-secret", PvgValidationReasonCodes.CountrySourceUnsettled),
            "report-type" => ("reportType", "type-secret", "type-secret", PvgValidationReasonCodes.FieldValueInvalid),
            _ => ("companyRecipientPersonId", "person-secret-1", "person-secret-1", PvgValidationReasonCodes.OwnerReadContractUnavailable)
        };

        (int StatusCode, string Raw, PvgCaseIntakeApiResponse Body) result;
        if (operation == "Create")
        {
            result = await harness.CreateAsync(CreateFromJson(Json(ValidBody((name, value)))));
            Assert.Equal(0, await harness.CountAsync());
        }
        else
        {
            var id = await harness.CreateValidAsync();
            result = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(1, (name, value))));
            Assert.Equal(1, (await harness.StoredAsync(id)).Version);
        }

        AssertRejected(result, expectedCode);
        Assert.DoesNotContain(marker, result.Raw, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("validityResult")]
    [InlineData("validityMissingCriteria")]
    [InlineData("reporterSource")]
    [InlineData("reporterSourceId")]
    [InlineData("reporterCrmSearchState")]
    public async Task Put_with_response_only_fields_is_rejected(string field)
    {
        var harness = new Harness();
        var id = await harness.CreateValidAsync();
        var before = await harness.StoredAsync(id);

        var rejected = await harness.UpdateAsync(id, UpdateFromJson(UpdateBody(before.Version, (field, "response-only-fake"))));

        AssertRejected(rejected, PvgValidationReasonCodes.FieldValueInvalid);
        var after = await harness.StoredAsync(id);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Intake.Status, after.Intake.Status);
        Assert.Equal(before.Intake.ReporterSource, after.Intake.ReporterSource);
        Assert.Equal(before.Intake.ReporterSourceId, after.Intake.ReporterSourceId);
        Assert.Equal(before.Intake.ReporterCrmSearchState, after.Intake.ReporterCrmSearchState);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("absent")]
    public async Task Post_with_remove_reporter_link_is_rejected(string row)
    {
        var harness = new Harness();
        var body = row == "absent" ? ValidBody() : ValidBody(("removeReporterLink", row == "true"));

        var result = await harness.CreateAsync(CreateFromJson(Json(body)));

        if (row == "true")
        {
            AssertRejected(result, PvgValidationReasonCodes.ReporterLinkAbsent);
            Assert.Equal(0, await harness.CountAsync());
        }
        else
        {
            Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
            Assert.Equal(1, await harness.CountAsync());
        }
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    public async Task Create_and_update_map_every_revision04_field_to_the_application_request(string operation)
    {
        var harness = new Harness();
        var body = ValidBody(
            ("intakeChannel", "Email"),
            ("sourceType", "Partner"),
            ("sourceReference", "source-ref-fake-2"),
            ("receivedAtUtc", "2020-03-02T09:30:00Z"),
            ("reporterType", "Consumer"),
            ("reporterContactSummary", "contact-fake-2"),
            ("patientSubjectCode", "patient-fake-9"),
            ("eventOnsetDate", "2020-02-15"),
            ("adverseEventNarrative", "narrative-fake-2"),
            ("productLines", new object[]
            {
                Line("Suspect", "product-one", "lot-one"),
                Line("Concomitant", "product-two", "lot-two")
            }),
            ("seriousness", "NonSerious"),
            ("intakePriority", "Low"),
            ("evidenceLinkReferences", new[] { "evidence-fake-1" }),
            ("awarenessAtUtc", "2020-03-02T10:30:00+03:00"),
            ("eventCountry", "unknown"),
            ("reportType", "Study"),
            ("reporterNameOrInitials", "K.L."),
            ("reporterProfession", "Nurse"),
            ("reporterCountry", "unknown"));

        string id;
        if (operation == "Create")
        {
            var created = await harness.CreateAsync(CreateFromJson(Json(body)));
            Assert.Equal(StatusCodes.Status200OK, created.StatusCode);
            id = created.Body.IntakeDraftId!;
        }
        else
        {
            id = await harness.CreateValidAsync();
            body["expectedVersion"] = 1;
            var updated = await harness.UpdateAsync(id, UpdateFromJson(Json(body)));
            Assert.Equal(StatusCodes.Status200OK, updated.StatusCode);
        }

        var stored = (await harness.StoredAsync(id)).Intake;
        Assert.Equal("Email", stored.IntakeChannel);
        Assert.Equal("Partner", stored.SourceType);
        Assert.Equal("source-ref-fake-2", stored.SourceReference);
        Assert.Equal(new DateTimeOffset(2020, 3, 2, 9, 30, 0, TimeSpan.Zero), stored.ReceivedAtUtc);
        Assert.Equal("Consumer", stored.ReporterType);
        Assert.Equal("contact-fake-2", stored.ReporterContactSummary);
        Assert.Equal("patient-fake-9", stored.PatientSubjectCode);
        Assert.Equal(new DateOnly(2020, 2, 15), stored.EventOnsetDate);
        Assert.Equal("narrative-fake-2", stored.AdverseEventNarrative);
        Assert.Equal(
            [
                new PvgIntakeProductLine("Suspect", null, "product-one", null, "lot-one"),
                new PvgIntakeProductLine("Concomitant", null, "product-two", null, "lot-two")
            ],
            stored.ProductLines);
        Assert.Equal("NonSerious", stored.Seriousness);
        Assert.Equal("Low", stored.IntakePriority);
        Assert.Equal(["evidence-fake-1"], stored.EvidenceLinkReferences);
        Assert.Equal(new DateTimeOffset(2020, 3, 2, 7, 30, 0, TimeSpan.Zero), stored.AwarenessAtUtc);
        Assert.Equal("unknown", stored.EventCountry);
        Assert.Equal("Study", stored.ReportType);
        Assert.Equal("K.L.", stored.ReporterNameOrInitials);
        Assert.Equal("Nurse", stored.ReporterProfession);
        Assert.Equal("unknown", stored.ReporterCountry);
        Assert.Equal(PvgReporterSource.Manual, stored.ReporterSource);
        Assert.Null(stored.ReporterSourceId);
    }

    [Fact]
    public async Task Get_by_id_returns_draft_fields_only_after_field_security_allows()
    {
        var store = new InMemoryPvgIntakeDraftRepository();
        var id = await new Harness(store).AddLinkedAsync(PvgReporterSource.Crm, LinkedCrmId);
        var denied = new Harness(store, allowFieldSecurity: false);

        var blockedDetail = await denied.GetAsync(id);
        Assert.Equal(StatusCodes.Status409Conflict, blockedDetail.StatusCode);
        Assert.Equal("Blocked", blockedDetail.Body.Outcome);
        Assert.Null(blockedDetail.Body.Draft);
        Assert.DoesNotContain("J.D.", blockedDetail.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Pharmacist", blockedDetail.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain("product-fake-a", blockedDetail.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain(LinkedCrmId, blockedDetail.Raw, StringComparison.Ordinal);

        var allowedDetail = await new Harness(store).GetAsync(id);
        Assert.Equal(StatusCodes.Status200OK, allowedDetail.StatusCode);
        Assert.NotNull(allowedDetail.Body.Draft);

        var blockedList = await denied.ListAsync(TenantA);
        Assert.Equal(StatusCodes.Status409Conflict, blockedList.StatusCode);
        Assert.Null(blockedList.Body.TotalCount);
        Assert.Null(blockedList.Body.FilteredCount);
    }

    [Fact]
    public async Task Get_by_id_of_another_tenants_draft_returns_no_draft_fields()
    {
        var harness = new Harness();
        var created = await harness.CreateAsync(CreateFromJson(Json(ValidBody(
            ("reporterNameOrInitials", "tenant-a-name-secret"),
            ("productLines", new object[] { Line("Suspect", "tenant-a-product-secret", "lot-1") })))));
        var id = created.Body.IntakeDraftId!;

        var crossTenant = await harness.GetAsync(id, TenantB);

        Assert.Equal(StatusCodes.Status409Conflict, crossTenant.StatusCode);
        Assert.Equal("Blocked", crossTenant.Body.Outcome);
        Assert.Equal(PvgApplicationReasonCodes.IntakeDraftNotFound, crossTenant.Body.ReasonCode);
        Assert.Null(crossTenant.Body.Draft);
        Assert.Null(crossTenant.Body.Version);
        Assert.Empty(crossTenant.Body.Items);
        Assert.DoesNotContain("tenant-a-name-secret", crossTenant.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant-a-product-secret", crossTenant.Raw, StringComparison.Ordinal);

        var ownTenant = await harness.GetAsync(id, TenantA);
        Assert.Equal(StatusCodes.Status200OK, ownTenant.StatusCode);
        Assert.Equal("tenant-a-name-secret", ownTenant.Body.Draft!.ReporterNameOrInitials);
    }

    // ---- AC-7: validation after the permission gate -------------------------------------------------------------

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Triage")]
    [InlineData("Route")]
    public async Task Unauthorized_caller_with_invalid_body_gets_permission_outcome_without_validation_codes(string operation)
    {
        var store = new InMemoryPvgIntakeDraftRepository();
        var harness = new Harness(store, permissionGate: new DenyAllPermissionGate());
        var invalidBody = new Dictionary<string, object?> { ["reporterSourceId"] = LinkedCrmId };

        var result = operation switch
        {
            "Create" => await harness.CreateAsync(CreateFromJson(Json(invalidBody))),
            "Update" => await harness.UpdateAsync("pvg-draft-unknown", UpdateFromJson(Json(invalidBody))),
            "Triage" => await harness.TriageAsync("pvg-draft-unknown", new PvgCaseIntakeTriageRequest(null, null, null, 1)),
            _ => await harness.RouteAsync("pvg-draft-unknown", new PvgCaseIntakeRouteRequest(null, 1))
        };

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("Blocked", result.Body.Outcome);
        Assert.Equal(PermissionDenied, result.Body.ReasonCode);
        Assert.Empty(result.Body.ValidationReasonCodes);
        Assert.Null(result.Body.Draft);
        Assert.Null(result.Body.Version);
        Assert.Null(result.Body.TotalCount);
        Assert.Null(result.Body.FilteredCount);
        Assert.DoesNotContain(LinkedCrmId, result.Raw, StringComparison.Ordinal);
        Assert.Equal(0, await store.CountAsync(new PvgPersistenceTenantScope(TenantA), null));

        if (operation == "Create")
        {
            var withoutActor = await harness.CreateAsync(CreateFromJson(Json(invalidBody)), actor: false);
            Assert.Equal(StatusCodes.Status409Conflict, withoutActor.StatusCode);
            Assert.Equal(PvgPermissionReasonCodes.ActorContextRequired, withoutActor.Body.ReasonCode);
            Assert.Empty(withoutActor.Body.ValidationReasonCodes);
        }
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private static void AssertVersionConflict((int StatusCode, string Raw, PvgCaseIntakeApiResponse Body) result)
    {
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("Blocked", result.Body.Outcome);
        Assert.Equal(PvgApplicationReasonCodes.IntakeDraftVersionConflict, result.Body.ReasonCode);
        Assert.Empty(result.Body.ValidationReasonCodes);
        Assert.Null(result.Body.Draft);
        Assert.Null(result.Body.Version);
    }

    private static void AssertRejected((int StatusCode, string Raw, PvgCaseIntakeApiResponse Body) result, string expectedCode)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Invalid", result.Body.Outcome);
        Assert.Contains(expectedCode, result.Body.ValidationReasonCodes);
        Assert.Null(result.Body.Draft);
        Assert.Null(result.Body.Version);
        Assert.Null(result.Body.IntakeDraftId);
    }

    private static async Task AssertLinkedUnchangedAsync(
        Harness harness,
        string id,
        long expectedVersion,
        PvgReporterSource source,
        string sourceId)
    {
        var after = await harness.StoredAsync(id);
        Assert.Equal(expectedVersion, after.Version);
        Assert.Equal(source, after.Intake.ReporterSource);
        Assert.Equal(sourceId, after.Intake.ReporterSourceId);
        Assert.Equal(PvgReporterCrmSearchState.Pending, after.Intake.ReporterCrmSearchState);
    }

    private static string Padded(string prefix, int length) => prefix + new string('x', length - prefix.Length);

    private static Dictionary<string, object?> Line(string role, string name, string? lot) =>
        new()
        {
            ["productRole"] = role,
            ["productNameAsReported"] = name,
            ["lotNumber"] = lot
        };

    // A valid revision 04 create body; (name, value) overrides replace or add a property (null value = JSON null).
    private static Dictionary<string, object?> ValidBody(params (string Name, object? Value)[] overrides)
    {
        var body = new Dictionary<string, object?>
        {
            ["intakeChannel"] = "Portal",
            ["sourceType"] = "Reporter",
            ["sourceReference"] = "source-ref-fake",
            ["receivedAtUtc"] = "2020-03-01T08:00:00Z",
            ["reporterType"] = "HealthcareProfessional",
            ["reporterContactSummary"] = "contact-fake",
            ["patientSubjectCode"] = "patient-fake-1",
            ["eventOnsetDate"] = "2020-02-01",
            ["adverseEventNarrative"] = "narrative-fake",
            ["productLines"] = new object[] { Line("Suspect", "product-fake-a", "lot-1") },
            ["seriousness"] = "Serious",
            ["intakePriority"] = "High",
            ["awarenessAtUtc"] = "2020-03-01T08:00:00Z",
            ["eventCountry"] = "unknown",
            ["reportType"] = "Spontaneous",
            ["reporterNameOrInitials"] = "J.D.",
            ["reporterProfession"] = "Pharmacist"
        };

        foreach (var (name, value) in overrides)
        {
            body[name] = value;
        }

        return body;
    }

    private static Dictionary<string, object?> UpdateBody(long? expectedVersion, params (string Name, object? Value)[] overrides)
    {
        var body = ValidBody(overrides);
        if (expectedVersion is not null)
        {
            body["expectedVersion"] = expectedVersion;
        }

        return body;
    }

    private static string Json(Dictionary<string, object?> body) => JsonSerializer.Serialize(body, WebOptions);

    private static PvgCaseIntakeCreateRequest CreateFromJson(string json) =>
        JsonSerializer.Deserialize<PvgCaseIntakeCreateRequest>(json, WebOptions)!;

    private static PvgCaseIntakeUpdateRequest UpdateFromJson(string json) =>
        JsonSerializer.Deserialize<PvgCaseIntakeUpdateRequest>(json, WebOptions)!;

    private static PvgCaseIntakeUpdateRequest UpdateFromJson(Dictionary<string, object?> body) => UpdateFromJson(Json(body));

    private static SafetyCaseIntake StoredIntake(string tenantId, PvgIntakeStatus status, DateTimeOffset receivedAtUtc) =>
        new(
            tenantId,
            status,
            "Portal",
            "Reporter",
            receivedAtUtc,
            "HealthcareProfessional",
            "narrative-fake",
            "Serious",
            "High")
        {
            SourceReference = "source-ref-fake",
            ReporterContactSummary = "contact-fake",
            PatientSubjectCode = "patient-fake-1",
            EventOnsetDate = new DateOnly(2020, 2, 1),
            EvidenceLinkReferences = [],
            ProductLines = [new PvgIntakeProductLine("Suspect", null, "product-fake-a", null, "lot-1")],
            AwarenessAtUtc = receivedAtUtc,
            EventCountry = "unknown",
            ReportType = "Spontaneous",
            ReporterSource = PvgReporterSource.Manual,
            ReporterSourceId = null,
            ReporterCrmSearchState = PvgReporterCrmSearchState.Pending,
            ReporterNameOrInitials = "J.D.",
            ReporterProfession = "Pharmacist",
            ReporterCountry = null
        };

    private static SafetyCaseIntake LinkedIntake(string tenantId, PvgReporterSource source, string sourceId) =>
        new(
            tenantId,
            PvgIntakeStatus.IntakeCreated,
            "Portal",
            "Reporter",
            FixtureReceivedAtUtc,
            "HealthcareProfessional",
            "narrative-fake",
            "Serious",
            "High")
        {
            SourceReference = "source-ref-fake",
            ReporterContactSummary = "contact-fake",
            PatientSubjectCode = "patient-fake-1",
            EventOnsetDate = new DateOnly(2020, 2, 1),
            EvidenceLinkReferences = [],
            ProductLines = [new PvgIntakeProductLine("Suspect", null, "product-fake-a", null, "lot-1")],
            AwarenessAtUtc = FixtureReceivedAtUtc,
            EventCountry = "unknown",
            ReportType = "Spontaneous",
            ReporterSource = source,
            ReporterSourceId = sourceId,
            ReporterCrmSearchState = PvgReporterCrmSearchState.Pending,
            ReporterNameOrInitials = "J.D.",
            ReporterProfession = "Pharmacist",
            ReporterCountry = null
        };

    private sealed class Harness
    {
        public Harness(
            InMemoryPvgIntakeDraftRepository? store = null,
            bool allowFieldSecurity = true,
            IPvgPermissionGate? permissionGate = null)
        {
            Store = store ?? new InMemoryPvgIntakeDraftRepository();
            Service = new PvgIntakeDraftApplicationService(
                new FixedFieldSecurityPolicy(allowFieldSecurity),
                new AllowingWorkflowTransitionGate(),
                new AllowingEvidenceLinkPort(),
                permissionGate ?? new AllowingPermissionGate(),
                Store);
        }

        public InMemoryPvgIntakeDraftRepository Store { get; }

        public PvgIntakeDraftApplicationService Service { get; }

        public async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> CreateAsync(
            PvgCaseIntakeCreateRequest request,
            string tenant = TenantA,
            bool actor = true) =>
            await ExecuteAsync(await PvgCaseIntakeTriageEndpoints.CreateDraftAsync(
                request,
                Context(tenant, actor),
                Service,
                CancellationToken.None));

        public async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> UpdateAsync(
            string id,
            PvgCaseIntakeUpdateRequest request,
            string tenant = TenantA) =>
            await ExecuteAsync(await PvgCaseIntakeTriageEndpoints.UpdateDraftAsync(
                id,
                request,
                Context(tenant),
                Service,
                CancellationToken.None));

        public async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> TriageAsync(
            string id,
            PvgCaseIntakeTriageRequest request,
            string tenant = TenantA) =>
            await ExecuteAsync(await PvgCaseIntakeTriageEndpoints.TriageDraftAsync(
                id,
                request,
                Context(tenant),
                Service,
                CancellationToken.None));

        public async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> RouteAsync(
            string id,
            PvgCaseIntakeRouteRequest request,
            string tenant = TenantA) =>
            await ExecuteAsync(await PvgCaseIntakeTriageEndpoints.RouteDraftAsync(
                id,
                request,
                Context(tenant),
                Service,
                CancellationToken.None));

        public async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> GetAsync(
            string id,
            string tenant = TenantA) =>
            await ExecuteAsync(await PvgCaseIntakeTriageEndpoints.GetDraftByIdAsync(
                id,
                Context(tenant),
                Service,
                CancellationToken.None));

        public async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> ListAsync(
            string tenant,
            int pageNumber = 1,
            int pageSize = 25,
            PvgIntakeStatus? status = null) =>
            await ExecuteAsync(await PvgCaseIntakeTriageEndpoints.ListDraftsAsync(
                Context(tenant),
                Service,
                pageNumber,
                pageSize,
                status,
                CancellationToken.None));

        public async Task<string> CreateValidAsync(string tenant = TenantA)
        {
            var created = await CreateAsync(CreateFromJson(Json(ValidBody())), tenant);
            Assert.Equal(StatusCodes.Status200OK, created.StatusCode);
            return Assert.IsType<string>(created.Body.IntakeDraftId);
        }

        public async Task<string> AddLinkedAsync(PvgReporterSource source, string sourceId) =>
            await Store.AddAsync(new PvgPersistenceTenantScope(TenantA), LinkedIntake(TenantA, source, sourceId));

        public async Task<PvgPersistedIntakeDraft> StoredAsync(string id)
        {
            var persisted = await Store.FindByIdAsync(new PvgPersistenceTenantScope(TenantA), id);
            return Assert.IsType<PvgPersistedIntakeDraft>(persisted);
        }

        public async Task<int> CountAsync() => await Store.CountAsync(new PvgPersistenceTenantScope(TenantA), null);

        private static DefaultHttpContext Context(string tenant, bool actor = true)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[PvgCaseIntakeRequestContext.TenantContextHeader] = tenant;
            if (actor)
            {
                context.Request.Headers[PvgCaseIntakeRequestContext.ActorIdHeader] = "actor-fake-1";
                context.Request.Headers[PvgCaseIntakeRequestContext.ActorKindHeader] = "HumanUser";
            }

            context.Request.Headers[PvgCaseIntakeRequestContext.CorrelationIdHeader] = "corr-fake-1";
            return context;
        }

        private static async Task<(int StatusCode, string Raw, PvgCaseIntakeApiResponse Body)> ExecuteAsync(IResult result)
        {
            var responseContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            responseContext.Response.Body = new MemoryStream();
            await result.ExecuteAsync(responseContext);
            responseContext.Response.Body.Position = 0;
            using var reader = new StreamReader(responseContext.Response.Body);
            var raw = await reader.ReadToEndAsync();
            var body = JsonSerializer.Deserialize<PvgCaseIntakeApiResponse>(raw, WebOptions);
            return (responseContext.Response.StatusCode, raw, Assert.IsType<PvgCaseIntakeApiResponse>(body));
        }
    }

    private sealed class AllowingPermissionGate : IPvgPermissionGate
    {
        public ValueTask<PvgPermissionDecision> EvaluateAsync(
            PvgPermissionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PvgPermissionDecision.Allowed());
    }

    private sealed class FixedFieldSecurityPolicy(bool allow) : IPvgFieldSecurityPolicy
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgFieldSecurityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(allow ? new PvgPortDecision(true, true, "PVG_TEST_ALLOWED") : PvgPortDecision.FieldSecurityDenied());
    }

    private sealed class AllowingWorkflowTransitionGate : IPvgWorkflowTransitionGate
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgWorkflowTransitionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    private sealed class AllowingEvidenceLinkPort : IPvgEvidenceLinkPort
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgEvidenceLinkRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }
}
