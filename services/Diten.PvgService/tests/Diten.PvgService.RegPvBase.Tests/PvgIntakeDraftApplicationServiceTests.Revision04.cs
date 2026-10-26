using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Xunit;

namespace Diten.PvgService.RegPvBase.Tests;

// Revision 04 intake fields (MOD-0230 DL-2 part B: AC-21, AC-23, AC-25, AC-27, AC-29).
public sealed partial class PvgIntakeDraftApplicationServiceTests
{
    private static readonly DateTimeOffset FixtureReceivedAtUtc = new(2020, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan PlusThreeHours = TimeSpan.FromHours(3);

    // AC-21 — product list.

    [Fact]
    public async Task Create_without_product_lines_passes_field_validation()
    {
        Assert.True(PvgIntakeDraftValidator.ValidateCreate(TenantContext(), ValidCreateRequest() with { ProductLines = [] }).IsValid);
        Assert.True(PvgIntakeDraftValidator.ValidateCreate(TenantContext(), ValidCreateRequest() with { ProductLines = null }).IsValid);

        var (service, store) = AllowedServiceWithStore();
        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ProductLines = [] }));

        Assert.True(created.Result.IsSuccess);
        Assert.Empty((await StoredAsync(store, created.IntakeDraftId!)).Intake.ProductLines);
    }

    [Fact]
    public async Task Product_line_without_as_reported_name_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, null, null, "lot-secret-1")] }));

        AssertRejected(rejected, PvgIntakeField.ProductNameAsReported, PvgValidationReasonCodes.RequiredFieldMissing, "lot-secret-1");
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData("Witness")]
    [InlineData("suspect")]
    public async Task Product_line_role_outside_option_set_is_rejected(string productRole)
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest(productRole, null, "as-reported-secret", null, null)] }));

        AssertRejected(rejected, PvgIntakeField.ProductRole, PvgValidationReasonCodes.FieldValueInvalid, productRole, "as-reported-secret");
        await AssertNothingStoredAsync(store);
    }

    [Fact]
    public async Task Product_line_gsku_without_global_product_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, "as-reported-secret", "gsku-secret-1", null)] }));

        AssertRejected(
            rejected,
            PvgIntakeField.PresentationGskuId,
            PvgValidationReasonCodes.ProductGskuRequiresGlobalProduct,
            "gsku-secret-1");
        Assert.DoesNotContain(
            new PvgValidationFailure(PvgIntakeField.PresentationGskuId, PvgValidationReasonCodes.OwnerReadContractUnavailable),
            rejected.Result.ValidationFailures);
        await AssertNothingStoredAsync(store);
    }

    [Fact]
    public async Task Product_line_global_product_id_is_rejected_while_MOD0290_contract_is_absent()
    {
        var (service, store) = AllowedServiceWithStore();

        var withProduct = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", "global-product-secret-1", "as-reported-secret", null, null)] }));
        var withProductAndGsku = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", "global-product-secret-1", "as-reported-secret", "gsku-secret-1", null)] }));

        AssertRejected(withProduct, PvgIntakeField.GlobalProductId, PvgValidationReasonCodes.OwnerReadContractUnavailable, "global-product-secret-1");
        AssertRejected(
            withProductAndGsku,
            PvgIntakeField.PresentationGskuId,
            PvgValidationReasonCodes.OwnerReadContractUnavailable,
            "global-product-secret-1",
            "gsku-secret-1");
        await AssertNothingStoredAsync(store);
    }

    // AC-23 — awareness moment and company recipient.

    [Fact]
    public async Task Create_without_awareness_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { AwarenessAtUtc = null }));

        AssertRejected(rejected, PvgIntakeField.AwarenessAtUtc, PvgValidationReasonCodes.RequiredFieldMissing);
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("earlier")]
    public async Task Awareness_equal_to_received_default_or_earlier_is_stored_unchanged(string row)
    {
        var awareness = row == "equal" ? FixtureReceivedAtUtc : FixtureReceivedAtUtc.AddDays(-2);
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ReceivedAtUtc = FixtureReceivedAtUtc, AwarenessAtUtc = awareness }));

        Assert.True(created.Result.IsSuccess);
        var stored = (await StoredAsync(store, created.IntakeDraftId!)).Intake;
        Assert.Equal(awareness, stored.AwarenessAtUtc);
        Assert.Equal(TimeSpan.Zero, stored.AwarenessAtUtc.Offset);
    }

    [Fact]
    public async Task Awareness_later_than_received_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ReceivedAtUtc = FixtureReceivedAtUtc, AwarenessAtUtc = FixtureReceivedAtUtc.AddMinutes(1) }));

        AssertRejected(rejected, PvgIntakeField.AwarenessAtUtc, PvgValidationReasonCodes.AwarenessAfterReceived);
        Assert.Single(rejected.Result.ValidationFailures, failure => failure.Field == PvgIntakeField.AwarenessAtUtc);
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData(PvgIntakeOperation.Create)]
    [InlineData(PvgIntakeOperation.Update)]
    public async Task Awareness_with_offset_is_normalized_to_utc(PvgIntakeOperation operation)
    {
        var sent = new DateTimeOffset(2020, 3, 1, 9, 0, 0, PlusThreeHours);
        var (service, store) = AllowedServiceWithStore();

        var intakeDraftId = await CreateOrUpdateAsync(
            service,
            store,
            operation,
            ValidCreateRequest() with { ReceivedAtUtc = FixtureReceivedAtUtc, AwarenessAtUtc = sent },
            ValidUpdateRequest() with { ReceivedAtUtc = FixtureReceivedAtUtc, AwarenessAtUtc = sent });

        var stored = (await StoredAsync(store, intakeDraftId)).Intake;
        Assert.Equal(TimeSpan.Zero, stored.AwarenessAtUtc.Offset);
        Assert.Equal(sent, stored.AwarenessAtUtc);
    }

    [Fact]
    public async Task Company_recipient_is_rejected_while_MOD0288_contract_is_absent()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { CompanyRecipientPersonId = "person-recipient-secret-1" }));

        AssertRejected(rejected, PvgIntakeField.CompanyRecipientPersonId, PvgValidationReasonCodes.OwnerReadContractUnavailable, "person-recipient-secret-1");
        await AssertNothingStoredAsync(store);

        var withoutRecipient = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { CompanyRecipientPersonId = null }));

        Assert.True(withoutRecipient.Result.IsSuccess);
        Assert.Null((await StoredAsync(store, withoutRecipient.IntakeDraftId!)).Intake.CompanyRecipientPersonId);
    }

    [Fact]
    public async Task Awareness_before_1900_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { AwarenessAtUtc = new DateTimeOffset(1899, 12, 31, 23, 59, 59, TimeSpan.Zero) }));

        AssertRejected(rejected, PvgIntakeField.AwarenessAtUtc, PvgValidationReasonCodes.FieldValueInvalid);
        await AssertNothingStoredAsync(store);
    }

    // AC-25 — event and reporter country while the country source is unsettled.

    [Fact]
    public async Task Create_without_event_country_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { EventCountry = null }));

        AssertRejected(rejected, PvgIntakeField.EventCountry, PvgValidationReasonCodes.RequiredFieldMissing);
        await AssertNothingStoredAsync(store);
    }

    [Fact]
    public async Task Event_country_unknown_is_accepted()
    {
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { EventCountry = "unknown" }));

        Assert.True(created.Result.IsSuccess);
        Assert.Equal("unknown", (await StoredAsync(store, created.IntakeDraftId!)).Intake.EventCountry);
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("tr")]
    [InlineData("Unknown")]
    [InlineData("UNKNOWN")]
    public async Task Event_country_code_is_rejected_while_country_source_is_unsettled(string eventCountry)
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { EventCountry = eventCountry }));

        AssertRejected(rejected, PvgIntakeField.EventCountry, PvgValidationReasonCodes.CountrySourceUnsettled, eventCountry);
        await AssertNothingStoredAsync(store);
    }

    [Fact]
    public async Task Reporter_country_accepts_null_and_unknown_only_while_list_is_absent()
    {
        var (service, store) = AllowedServiceWithStore();

        var withNull = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReporterCountry = null }));
        var withUnknown = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReporterCountry = "unknown" }));
        var withCode = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReporterCountry = "TR" }));

        Assert.True(withNull.Result.IsSuccess);
        Assert.Null((await StoredAsync(store, withNull.IntakeDraftId!)).Intake.ReporterCountry);
        Assert.True(withUnknown.Result.IsSuccess);
        Assert.Equal("unknown", (await StoredAsync(store, withUnknown.IntakeDraftId!)).Intake.ReporterCountry);
        AssertRejected(withCode, PvgIntakeField.ReporterCountry, PvgValidationReasonCodes.CountrySourceUnsettled, "TR");
        Assert.Equal(2, (await AllStoredAsync(store)).Count);
    }

    // AC-27 — report type (values wait for QPPV confirmation).

    [Fact]
    public async Task Create_without_report_type_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReportType = null }));

        AssertRejected(rejected, PvgIntakeField.ReportType, PvgValidationReasonCodes.RequiredFieldMissing);
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("spontaneous")]
    public async Task Report_type_outside_option_set_is_rejected(string reportType)
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReportType = reportType }));

        AssertRejected(rejected, PvgIntakeField.ReportType, PvgValidationReasonCodes.FieldValueInvalid, reportType);
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData("Spontaneous")]
    [InlineData("Study")]
    [InlineData("Literature")]
    [InlineData("Solicited")]
    [InlineData("Other")]
    public async Task Report_type_accepts_each_value_of_the_pending_option_set(string reportTypePendingQppvConfirmation)
    {
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ReportType = reportTypePendingQppvConfirmation }));

        Assert.True(created.Result.IsSuccess);
        Assert.Equal(reportTypePendingQppvConfirmation, (await StoredAsync(store, created.IntakeDraftId!)).Intake.ReportType);
    }

    // AC-29 — reporter record and the reporter selection contract.

    [Fact]
    public async Task Manual_reporter_without_identifier_is_accepted_at_create()
    {
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with
        {
            ReporterContactSummary = null,
            ReporterNameOrInitials = null,
            ReporterProfession = null,
            ReporterCountry = null,
            ReporterSelectionDirectory = null,
            ReporterSelectionId = null
        }));

        Assert.True(created.Result.IsSuccess);
        var stored = (await StoredAsync(store, created.IntakeDraftId!)).Intake;
        Assert.Equal(PvgReporterSource.Manual, stored.ReporterSource);
        Assert.Null(stored.ReporterNameOrInitials);
        Assert.Null(stored.ReporterProfession);
        Assert.Null(stored.ReporterCountry);
        Assert.Null(stored.ReporterContactSummary);
    }

    [Fact]
    public async Task Create_without_reporter_selection_stores_manual_source()
    {
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReporterNameOrInitials = "J.D." }));

        Assert.True(created.Result.IsSuccess);
        var stored = (await StoredAsync(store, created.IntakeDraftId!)).Intake;
        Assert.Equal(PvgReporterSource.Manual, stored.ReporterSource);
        Assert.Null(stored.ReporterSourceId);
    }

    [Fact]
    public async Task Create_sets_reporter_crm_search_state_pending()
    {
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with { ReporterNameOrInitials = "J.D." }));

        Assert.True(created.Result.IsSuccess);
        Assert.Equal(PvgReporterCrmSearchState.Pending, (await StoredAsync(store, created.IntakeDraftId!)).Intake.ReporterCrmSearchState);
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("id")]
    public async Task Reporter_selection_with_only_directory_or_only_id_is_rejected(string row)
    {
        var (service, store) = AllowedServiceWithStore();
        var request = row == "directory"
            ? ValidCreateRequest() with { ReporterSelectionDirectory = "Crm" }
            : ValidCreateRequest() with { ReporterSelectionId = "crm-contact-secret-1" };

        var rejected = await service.CreateDraftAsync(CreateCommand(request));

        AssertRejected(
            rejected,
            row == "directory" ? PvgIntakeField.ReporterSelectionId : PvgIntakeField.ReporterSelectionDirectory,
            PvgValidationReasonCodes.ReporterSelectionIncomplete,
            "crm-contact-secret-1");
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData("Ldap")]
    [InlineData("crm")]
    public async Task Reporter_selection_directory_outside_option_set_is_rejected(string directory)
    {
        var (service, store) = AllowedServiceWithStore();

        var rejected = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ReporterSelectionDirectory = directory, ReporterSelectionId = "crm-contact-secret-1" }));

        AssertRejected(rejected, PvgIntakeField.ReporterSelectionDirectory, PvgValidationReasonCodes.FieldValueInvalid, "crm-contact-secret-1");
        Assert.DoesNotContain(
            rejected.Result.ValidationFailures,
            failure => failure.ReasonCode == PvgValidationReasonCodes.OwnerReadContractUnavailable);
        await AssertNothingStoredAsync(store);
    }

    [Theory]
    [InlineData("Crm", PvgIntakeOperation.Create)]
    [InlineData("Crm", PvgIntakeOperation.Update)]
    [InlineData("PersonDirectory", PvgIntakeOperation.Create)]
    [InlineData("PersonDirectory", PvgIntakeOperation.Update)]
    public async Task Reporter_selection_is_rejected_while_owner_contracts_are_absent(string directory, PvgIntakeOperation operation)
    {
        var (service, store) = AllowedServiceWithStore();
        var selectionId = directory == "Crm" ? "crm-contact-secret-1" : "person-secret-1";

        if (operation == PvgIntakeOperation.Create)
        {
            var rejectedCreate = await service.CreateDraftAsync(CreateCommand(
                ValidCreateRequest() with { ReporterSelectionDirectory = directory, ReporterSelectionId = selectionId }));

            AssertRejected(rejectedCreate, PvgIntakeField.ReporterSelectionDirectory, PvgValidationReasonCodes.OwnerReadContractUnavailable, selectionId);
            await AssertNothingStoredAsync(store);
            return;
        }

        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        var before = await StoredAsync(store, created.IntakeDraftId!);

        var rejectedUpdate = await service.UpdateDraftAsync(UpdateCommand(
            created.IntakeDraftId!,
            ValidUpdateRequest() with { ReporterSelectionDirectory = directory, ReporterSelectionId = selectionId },
            before.Version));

        AssertRejected(rejectedUpdate, PvgIntakeField.ReporterSelectionDirectory, PvgValidationReasonCodes.OwnerReadContractUnavailable, selectionId);
        var after = await StoredAsync(store, created.IntakeDraftId!);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(PvgReporterSource.Manual, after.Intake.ReporterSource);
        Assert.Null(after.Intake.ReporterSourceId);
        Assert.Equal("source-ref", after.Intake.SourceReference);
    }

    [Theory]
    [InlineData(PvgReporterSource.Crm)]
    [InlineData(PvgReporterSource.PersonDirectory)]
    public async Task Update_without_reporter_selection_on_linked_record_is_rejected(PvgReporterSource source)
    {
        var (service, store) = AllowedServiceWithStore();
        var intakeDraftId = await store.AddAsync(Scope(), LinkedFixtureIntake(source));
        var before = await StoredAsync(store, intakeDraftId);

        var withoutMarker = await service.UpdateDraftAsync(UpdateCommand(
            intakeDraftId,
            FixtureUpdateRequest() with { RemoveReporterLink = null },
            before.Version));
        var withFalseMarker = await service.UpdateDraftAsync(UpdateCommand(
            intakeDraftId,
            FixtureUpdateRequest() with { RemoveReporterLink = false },
            before.Version));

        AssertRejected(withoutMarker, PvgIntakeField.RemoveReporterLink, PvgValidationReasonCodes.ReporterLinkRemovalRequired, LinkedSourceId(source));
        AssertRejected(withFalseMarker, PvgIntakeField.RemoveReporterLink, PvgValidationReasonCodes.ReporterLinkRemovalRequired, LinkedSourceId(source));
        await AssertLinkedFixtureUnchangedAsync(store, intakeDraftId, source, before.Version);
    }

    [Theory]
    [InlineData(PvgReporterSource.Crm)]
    [InlineData(PvgReporterSource.PersonDirectory)]
    public async Task Remove_reporter_link_sets_manual_clears_source_id_and_keeps_snapshot(PvgReporterSource source)
    {
        var (service, store) = AllowedServiceWithStore();
        var intakeDraftId = await store.AddAsync(Scope(), LinkedFixtureIntake(source));
        var before = await StoredAsync(store, intakeDraftId);

        var removed = await service.UpdateDraftAsync(UpdateCommand(
            intakeDraftId,
            FixtureUpdateRequest() with { RemoveReporterLink = true },
            before.Version));

        Assert.True(removed.Result.IsSuccess);
        var stored = (await StoredAsync(store, intakeDraftId)).Intake;
        Assert.Equal(PvgReporterSource.Manual, stored.ReporterSource);
        Assert.Null(stored.ReporterSourceId);
        Assert.Equal(PvgReporterCrmSearchState.ClosedByUserUnlink, stored.ReporterCrmSearchState);
        Assert.Equal("J.D.", stored.ReporterNameOrInitials);
        Assert.Equal("Pharmacist", stored.ReporterProfession);
        Assert.Null(stored.ReporterCountry);
        Assert.Equal("reporter@example.test", stored.ReporterContactSummary);

        var auditIntent = Assert.IsType<PvgAuditIntent>(removed.AuditIntent);
        Assert.Equal(
            new[] { PvgIntakeField.ReporterSource, PvgIntakeField.ReporterSourceId, PvgIntakeField.ReporterCrmSearchState }.Order(),
            auditIntent.ChangedFields.Order());
        AssertDoesNotRender(auditIntent, LinkedSourceId(source), "J.D.", "Pharmacist", "reporter@example.test");
        AssertSafeAuditIntent(auditIntent);
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("id")]
    [InlineData("both")]
    public async Task Remove_reporter_link_with_selection_pair_is_rejected(string row)
    {
        var (service, store) = AllowedServiceWithStore();
        var intakeDraftId = await store.AddAsync(Scope(), LinkedFixtureIntake(PvgReporterSource.Crm));
        var before = await StoredAsync(store, intakeDraftId);
        var request = row switch
        {
            "directory" => FixtureUpdateRequest() with { RemoveReporterLink = true, ReporterSelectionDirectory = "Crm" },
            "id" => FixtureUpdateRequest() with { RemoveReporterLink = true, ReporterSelectionId = "crm-contact-secret-2" },
            _ => FixtureUpdateRequest() with
            {
                RemoveReporterLink = true,
                ReporterSelectionDirectory = "Crm",
                ReporterSelectionId = "crm-contact-secret-2"
            }
        };

        var rejected = await service.UpdateDraftAsync(UpdateCommand(intakeDraftId, request, before.Version));

        AssertRejected(rejected, PvgIntakeField.RemoveReporterLink, PvgValidationReasonCodes.ReporterLinkRemovalWithSelection, "crm-contact-secret-2");
        await AssertLinkedFixtureUnchangedAsync(store, intakeDraftId, PvgReporterSource.Crm, before.Version);
    }

    [Fact]
    public async Task Remove_reporter_link_without_existing_link_is_rejected()
    {
        var (service, store) = AllowedServiceWithStore();
        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        var before = await StoredAsync(store, created.IntakeDraftId!);

        var rejected = await service.UpdateDraftAsync(UpdateCommand(
            created.IntakeDraftId!,
            ValidUpdateRequest() with { RemoveReporterLink = true },
            before.Version));

        AssertRejected(rejected, PvgIntakeField.RemoveReporterLink, PvgValidationReasonCodes.ReporterLinkAbsent);
        var after = await StoredAsync(store, created.IntakeDraftId!);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(PvgReporterCrmSearchState.Pending, after.Intake.ReporterCrmSearchState);
        Assert.Equal("source-ref", after.Intake.SourceReference);
        Assert.Null(typeof(PvgCreateIntakeDraftRequest).GetProperty(nameof(PvgUpdateIntakeDraftRequest.RemoveReporterLink)));
    }

    // CT additions.

    [Theory]
    [InlineData("Suspect")]
    [InlineData("Concomitant")]
    public async Task Product_line_with_suspect_or_concomitant_role_is_stored_with_as_reported_name(string productRole)
    {
        var (service, store) = AllowedServiceWithStore();

        var created = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest(productRole, null, "as-reported-name-1", null, "lot-1")] }));

        Assert.True(created.Result.IsSuccess);
        Assert.Equal(
            [new PvgIntakeProductLine(productRole, null, "as-reported-name-1", null, "lot-1")],
            (await StoredAsync(store, created.IntakeDraftId!)).Intake.ProductLines);
    }

    [Fact]
    public async Task Product_lines_and_reporter_texts_over_their_limits_are_rejected()
    {
        var (service, store) = AllowedServiceWithStore();

        var tooManyLines = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = Enumerable.Range(1, 21).Select(index => SuspectLineRequest($"as-reported-{index}")).ToArray() }));
        var longName = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [SuspectLineRequest(new string('p', 513))] }));
        var longLot = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, "as-reported", null, new string('l', 65))] }));
        var longReporterName = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ReporterNameOrInitials = new string('n', 129) }));
        var longReporterProfession = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ReporterProfession = new string('r', 129) }));

        AssertRejected(tooManyLines, PvgIntakeField.ProductLines, PvgValidationReasonCodes.FieldValueInvalid);
        AssertRejected(longName, PvgIntakeField.ProductNameAsReported, PvgValidationReasonCodes.FieldValueInvalid, new string('p', 513));
        AssertRejected(longLot, PvgIntakeField.LotNumber, PvgValidationReasonCodes.FieldValueInvalid, new string('l', 65));
        AssertRejected(longReporterName, PvgIntakeField.ReporterNameOrInitials, PvgValidationReasonCodes.FieldValueInvalid, new string('n', 129));
        AssertRejected(longReporterProfession, PvgIntakeField.ReporterProfession, PvgValidationReasonCodes.FieldValueInvalid, new string('r', 129));
        await AssertNothingStoredAsync(store);

        var atLimits = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with
        {
            ProductLines = Enumerable.Range(1, 20)
                .Select(index => new PvgIntakeProductLineRequest("Suspect", null, new string('p', 512), null, new string('l', 64)))
                .ToArray(),
            ReporterNameOrInitials = new string('n', 128),
            ReporterProfession = new string('r', 128)
        }));

        Assert.True(atLimits.Result.IsSuccess);
        var stored = (await StoredAsync(store, atLimits.IntakeDraftId!)).Intake;
        Assert.Equal(20, stored.ProductLines.Count);
        Assert.All(stored.ProductLines, line => Assert.Equal(512, line.ProductNameAsReported.Length));
        Assert.All(stored.ProductLines, line => Assert.Equal(64, line.LotNumber!.Length));
        Assert.Equal(128, stored.ReporterNameOrInitials!.Length);
        Assert.Equal(128, stored.ReporterProfession!.Length);
    }

    [Theory]
    [InlineData("missing-awareness")]
    [InlineData("awareness-after-received")]
    [InlineData("awareness-before-1900")]
    [InlineData("missing-event-country")]
    [InlineData("event-country-code")]
    [InlineData("reporter-country-code")]
    [InlineData("missing-report-type")]
    [InlineData("report-type-outside-set")]
    [InlineData("company-recipient")]
    [InlineData("global-product-id")]
    [InlineData("gsku-without-global-product")]
    [InlineData("product-role-outside-set")]
    [InlineData("missing-product-role")]
    [InlineData("missing-product-name")]
    [InlineData("21-product-lines")]
    [InlineData("513-product-name")]
    [InlineData("65-lot-number")]
    [InlineData("129-reporter-name")]
    [InlineData("129-reporter-profession")]
    [InlineData("only-selection-directory")]
    [InlineData("selection-directory-outside-set")]
    public async Task Update_applies_the_create_field_rules(string row)
    {
        var (request, field, reasonCode) = row switch
        {
            "missing-awareness" => (ValidUpdateRequest() with { AwarenessAtUtc = null }, PvgIntakeField.AwarenessAtUtc, PvgValidationReasonCodes.RequiredFieldMissing),
            "awareness-after-received" => (
                ValidUpdateRequest() with { ReceivedAtUtc = FixtureReceivedAtUtc, AwarenessAtUtc = FixtureReceivedAtUtc.AddHours(1) },
                PvgIntakeField.AwarenessAtUtc,
                PvgValidationReasonCodes.AwarenessAfterReceived),
            "awareness-before-1900" => (
                ValidUpdateRequest() with { AwarenessAtUtc = new DateTimeOffset(1899, 12, 31, 0, 0, 0, TimeSpan.Zero) },
                PvgIntakeField.AwarenessAtUtc,
                PvgValidationReasonCodes.FieldValueInvalid),
            "missing-event-country" => (ValidUpdateRequest() with { EventCountry = null }, PvgIntakeField.EventCountry, PvgValidationReasonCodes.RequiredFieldMissing),
            "event-country-code" => (ValidUpdateRequest() with { EventCountry = "TR" }, PvgIntakeField.EventCountry, PvgValidationReasonCodes.CountrySourceUnsettled),
            "reporter-country-code" => (ValidUpdateRequest() with { ReporterCountry = "TR" }, PvgIntakeField.ReporterCountry, PvgValidationReasonCodes.CountrySourceUnsettled),
            "missing-report-type" => (ValidUpdateRequest() with { ReportType = null }, PvgIntakeField.ReportType, PvgValidationReasonCodes.RequiredFieldMissing),
            "report-type-outside-set" => (ValidUpdateRequest() with { ReportType = "Unknown" }, PvgIntakeField.ReportType, PvgValidationReasonCodes.FieldValueInvalid),
            "company-recipient" => (
                ValidUpdateRequest() with { CompanyRecipientPersonId = "person-recipient-secret-1" },
                PvgIntakeField.CompanyRecipientPersonId,
                PvgValidationReasonCodes.OwnerReadContractUnavailable),
            "global-product-id" => (
                ValidUpdateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", "global-product-secret-1", "as-reported", null, null)] },
                PvgIntakeField.GlobalProductId,
                PvgValidationReasonCodes.OwnerReadContractUnavailable),
            "gsku-without-global-product" => (
                ValidUpdateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, "as-reported", "gsku-secret-1", null)] },
                PvgIntakeField.PresentationGskuId,
                PvgValidationReasonCodes.ProductGskuRequiresGlobalProduct),
            "product-role-outside-set" => (
                ValidUpdateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Witness", null, "as-reported", null, null)] },
                PvgIntakeField.ProductRole,
                PvgValidationReasonCodes.FieldValueInvalid),
            "missing-product-role" => (
                ValidUpdateRequest() with { ProductLines = [new PvgIntakeProductLineRequest(null, null, "as-reported", null, null)] },
                PvgIntakeField.ProductRole,
                PvgValidationReasonCodes.RequiredFieldMissing),
            "missing-product-name" => (
                ValidUpdateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, null, null, null)] },
                PvgIntakeField.ProductNameAsReported,
                PvgValidationReasonCodes.RequiredFieldMissing),
            "21-product-lines" => (
                ValidUpdateRequest() with { ProductLines = Enumerable.Range(1, 21).Select(index => SuspectLineRequest($"as-reported-{index}")).ToArray() },
                PvgIntakeField.ProductLines,
                PvgValidationReasonCodes.FieldValueInvalid),
            "513-product-name" => (
                ValidUpdateRequest() with { ProductLines = [SuspectLineRequest(new string('p', 513))] },
                PvgIntakeField.ProductNameAsReported,
                PvgValidationReasonCodes.FieldValueInvalid),
            "65-lot-number" => (
                ValidUpdateRequest() with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, "as-reported", null, new string('l', 65))] },
                PvgIntakeField.LotNumber,
                PvgValidationReasonCodes.FieldValueInvalid),
            "129-reporter-name" => (
                ValidUpdateRequest() with { ReporterNameOrInitials = new string('n', 129) },
                PvgIntakeField.ReporterNameOrInitials,
                PvgValidationReasonCodes.FieldValueInvalid),
            "129-reporter-profession" => (
                ValidUpdateRequest() with { ReporterProfession = new string('r', 129) },
                PvgIntakeField.ReporterProfession,
                PvgValidationReasonCodes.FieldValueInvalid),
            "only-selection-directory" => (
                ValidUpdateRequest() with { ReporterSelectionDirectory = "Crm" },
                PvgIntakeField.ReporterSelectionId,
                PvgValidationReasonCodes.ReporterSelectionIncomplete),
            "selection-directory-outside-set" => (
                ValidUpdateRequest() with { ReporterSelectionDirectory = "Ldap", ReporterSelectionId = "crm-contact-secret-1" },
                PvgIntakeField.ReporterSelectionDirectory,
                PvgValidationReasonCodes.FieldValueInvalid),
            _ => throw new InvalidOperationException($"Unsupported update rule row {row}.")
        };
        var (service, store) = AllowedServiceWithStore();
        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        var before = await StoredAsync(store, created.IntakeDraftId!);

        var rejected = await service.UpdateDraftAsync(UpdateCommand(created.IntakeDraftId!, request, before.Version));

        AssertRejected(rejected, field, reasonCode, "person-recipient-secret-1", "global-product-secret-1", "gsku-secret-1", "crm-contact-secret-1");
        var after = await StoredAsync(store, created.IntakeDraftId!);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, after.Intake.Status);
        Assert.Equal("source-ref", after.Intake.SourceReference);
        AssertSingleSuspectLine(after.Intake, "suspect product");
    }

    [Fact]
    public async Task Revision04_fields_are_kept_through_update_triage_and_route()
    {
        var (service, store) = AllowedServiceWithStore();
        PvgIntakeProductLine[] createdLines =
        [
            new("Suspect", null, "as-reported-a", null, "lot-a"),
            new("Concomitant", null, "as-reported-b", null, "lot-b")
        ];
        PvgIntakeProductLine[] updatedLines =
        [
            new("Concomitant", null, "as-reported-c", null, "lot-c"),
            new("Suspect", null, "as-reported-d", null, "lot-d")
        ];

        var created = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with
        {
            ReceivedAtUtc = FixtureReceivedAtUtc,
            ProductLines = createdLines.Select(ToLineRequest).ToArray(),
            AwarenessAtUtc = FixtureReceivedAtUtc.AddDays(-1),
            EventCountry = "unknown",
            ReportType = "Study",
            ReporterNameOrInitials = "J.D.",
            ReporterProfession = "Pharmacist",
            ReporterCountry = "unknown"
        }));
        Assert.True(created.Result.IsSuccess);
        var afterCreate = await StoredAsync(store, created.IntakeDraftId!);
        AssertRevision04Fields(afterCreate.Intake, createdLines, FixtureReceivedAtUtc.AddDays(-1), "Study", "J.D.", "Pharmacist", "unknown");

        var updated = await service.UpdateDraftAsync(UpdateCommand(
            created.IntakeDraftId!,
            ValidUpdateRequest() with
            {
                ReceivedAtUtc = FixtureReceivedAtUtc,
                ProductLines = updatedLines.Select(ToLineRequest).ToArray(),
                AwarenessAtUtc = FixtureReceivedAtUtc.AddDays(-2),
                EventCountry = "unknown",
                ReportType = "Literature",
                ReporterNameOrInitials = "K.L.",
                ReporterProfession = "Nurse",
                ReporterCountry = null
            },
            afterCreate.Version));
        Assert.True(updated.Result.IsSuccess);
        var afterUpdate = await StoredAsync(store, created.IntakeDraftId!);
        AssertRevision04Fields(afterUpdate.Intake, updatedLines, FixtureReceivedAtUtc.AddDays(-2), "Literature", "K.L.", "Nurse", null);

        var triaged = await service.TriageDraftAsync(TriageCommand(created.IntakeDraftId!, afterUpdate.Version));
        Assert.True(triaged.Result.IsSuccess);
        var afterTriage = await StoredAsync(store, created.IntakeDraftId!);
        Assert.Equal(PvgIntakeStatus.Triaged, afterTriage.Intake.Status);
        AssertRevision04Fields(afterTriage.Intake, updatedLines, FixtureReceivedAtUtc.AddDays(-2), "Literature", "K.L.", "Nurse", null);

        var routed = await service.RouteDraftAsync(RouteCommand(created.IntakeDraftId!, afterTriage.Version));
        Assert.True(routed.Result.IsSuccess);
        var afterRoute = await StoredAsync(store, created.IntakeDraftId!);
        Assert.Equal(PvgIntakeStatus.RoutePending, afterRoute.Intake.Status);
        AssertRevision04Fields(afterRoute.Intake, updatedLines, FixtureReceivedAtUtc.AddDays(-2), "Literature", "K.L.", "Nurse", null);
    }

    [Theory]
    [InlineData(PvgReporterCrmSearchState.ClosedByUserUnlink)]
    [InlineData(PvgReporterCrmSearchState.Pending)]
    public async Task Update_keeps_stored_reporter_source_and_search_state(PvgReporterCrmSearchState searchState)
    {
        var (service, store) = AllowedServiceWithStore();
        var intakeDraftId = await store.AddAsync(Scope(), FixtureIntake(PvgReporterSource.Manual, null, searchState, FixtureReceivedAtUtc));
        var before = await StoredAsync(store, intakeDraftId);

        var updated = await service.UpdateDraftAsync(UpdateCommand(
            intakeDraftId,
            FixtureUpdateRequest() with { ReporterNameOrInitials = "K.L.", AdverseEventNarrative = "changed narrative" },
            before.Version));

        Assert.True(updated.Result.IsSuccess);
        var stored = (await StoredAsync(store, intakeDraftId)).Intake;
        Assert.Equal(searchState, stored.ReporterCrmSearchState);
        Assert.Equal(PvgReporterSource.Manual, stored.ReporterSource);
        Assert.Null(stored.ReporterSourceId);
        Assert.Equal("K.L.", stored.ReporterNameOrInitials);
    }

    [Theory]
    [InlineData("narrative-and-report-type")]
    [InlineData("lot-number")]
    [InlineData("second-line")]
    [InlineData("product-name")]
    [InlineData("product-role")]
    [InlineData("reporter-name")]
    [InlineData("reporter-profession")]
    [InlineData("reporter-contact-summary")]
    [InlineData("earlier-awareness")]
    [InlineData("same-instant-other-offset")]
    [InlineData("no-change")]
    [InlineData("intake-channel")]
    [InlineData("source-type")]
    [InlineData("source-reference")]
    [InlineData("received-at")]
    [InlineData("reporter-type")]
    [InlineData("patient-subject-code")]
    [InlineData("event-onset-date")]
    [InlineData("seriousness")]
    [InlineData("intake-priority")]
    [InlineData("evidence-link-references")]
    [InlineData("reporter-country")]
    public async Task Update_audit_intent_names_changed_fields_without_values(string row)
    {
        var fixture = FixtureUpdateRequest();
        var (request, expectedFields, submittedTexts) = row switch
        {
            "narrative-and-report-type" => (
                fixture with { AdverseEventNarrative = "changed narrative secret", ReportType = "Study" },
                new[] { PvgIntakeField.AdverseEventNarrative, PvgIntakeField.ReportType },
                new[] { "changed narrative secret" }),
            "lot-number" => (
                fixture with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, "lot-secret-2")] },
                [PvgIntakeField.ProductLines],
                ["lot-secret-2"]),
            "second-line" => (
                fixture with
                {
                    ProductLines =
                    [
                        new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, "lot-1"),
                        new PvgIntakeProductLineRequest("Concomitant", null, "concomitant secret", null, null)
                    ]
                },
                [PvgIntakeField.ProductLines],
                ["concomitant secret"]),
            "product-name" => (
                fixture with { ProductLines = [new PvgIntakeProductLineRequest("Suspect", null, "renamed product secret", null, "lot-1")] },
                [PvgIntakeField.ProductLines],
                ["renamed product secret"]),
            "product-role" => (
                fixture with { ProductLines = [new PvgIntakeProductLineRequest("Concomitant", null, "suspect product", null, "lot-1")] },
                [PvgIntakeField.ProductLines],
                ["suspect product"]),
            "reporter-name" => (fixture with { ReporterNameOrInitials = "K.L." }, [PvgIntakeField.ReporterNameOrInitials], ["K.L."]),
            "reporter-profession" => (fixture with { ReporterProfession = "Nurse secret" }, [PvgIntakeField.ReporterProfession], ["Nurse secret"]),
            "reporter-contact-summary" => (
                fixture with { ReporterContactSummary = "other@example.test" },
                [PvgIntakeField.ReporterContactSummary],
                ["other@example.test"]),
            "earlier-awareness" => (fixture with { AwarenessAtUtc = FixtureReceivedAtUtc.AddHours(-1) }, [PvgIntakeField.AwarenessAtUtc], []),
            "same-instant-other-offset" => (fixture with { AwarenessAtUtc = FixtureReceivedAtUtc.ToOffset(TimeSpan.FromHours(5)) }, [], []),
            "no-change" => (fixture, [], []),
            "intake-channel" => (fixture with { IntakeChannel = "Email" }, [PvgIntakeField.IntakeChannel], []),
            "source-type" => (fixture with { SourceType = "Partner" }, [PvgIntakeField.SourceType], []),
            "source-reference" => (fixture with { SourceReference = "other-source-ref-secret" }, [PvgIntakeField.SourceReference], ["other-source-ref-secret"]),
            "received-at" => (fixture with { ReceivedAtUtc = FixtureReceivedAtUtc.AddDays(1) }, [PvgIntakeField.ReceivedAtUtc], []),
            "reporter-type" => (fixture with { ReporterType = "Consumer" }, [PvgIntakeField.ReporterType], []),
            "patient-subject-code" => (fixture with { PatientSubjectCode = "patient-code-secret" }, [PvgIntakeField.PatientSubjectCode], ["patient-code-secret"]),
            "event-onset-date" => (fixture with { EventOnsetDate = new DateOnly(2020, 2, 15) }, [PvgIntakeField.EventOnsetDate], []),
            "seriousness" => (fixture with { Seriousness = "NonSerious" }, [PvgIntakeField.Seriousness], []),
            "intake-priority" => (fixture with { IntakePriority = "Low" }, [PvgIntakeField.IntakePriority], []),
            "evidence-link-references" => (fixture with { EvidenceLinkReferences = ["evidence-ref-secret"] }, [PvgIntakeField.EvidenceLinkReferences], ["evidence-ref-secret"]),
            "reporter-country" => (fixture with { ReporterCountry = "unknown" }, [PvgIntakeField.ReporterCountry], []),
            _ => throw new InvalidOperationException($"Unsupported changed-field row {row}.")
        };
        var (service, store) = AllowedServiceWithStore();
        var intakeDraftId = await store.AddAsync(
            Scope(),
            FixtureIntake(PvgReporterSource.Manual, null, PvgReporterCrmSearchState.Pending, FixtureReceivedAtUtc.ToOffset(PlusThreeHours)));
        var before = await StoredAsync(store, intakeDraftId);

        var updated = await service.UpdateDraftAsync(UpdateCommand(intakeDraftId, request, before.Version));

        Assert.True(updated.Result.IsSuccess);
        var auditIntent = Assert.IsType<PvgAuditIntent>(updated.AuditIntent);
        Assert.Equal(expectedFields.Order(), auditIntent.ChangedFields.Order());
        AssertDoesNotRender(auditIntent, submittedTexts.Concat(["J.D.", "Pharmacist", "reporter@example.test"]).ToArray());
        AssertSafeAuditIntent(auditIntent);
    }

    [Theory]
    [InlineData(PvgIntakeOperation.Create)]
    [InlineData(PvgIntakeOperation.Update)]
    public async Task Legacy_shaped_request_is_invalid_before_permission_gate(PvgIntakeOperation operation)
    {
        var callLog = new List<string>();
        var service = NewService(
            new RecordingFieldSecurityPolicy(callLog),
            new RecordingWorkflowTransitionGate(callLog),
            new RecordingEvidenceLinkPort(callLog),
            new RecordingPermissionGate(callLog) { Decision = PvgPermissionDecision.Denied(PvgPermissionReasonCodes.PermissionDenied) });

        // The shape the API mapping produces until DL-4: slice 1 fields and one Suspect line, no revision 04 field.
        var result = operation == PvgIntakeOperation.Create
            ? await service.CreateDraftAsync(CreateCommand(ValidCreateRequest() with
            {
                ProductLines = [SuspectLineRequest("suspect product")],
                AwarenessAtUtc = null,
                EventCountry = null,
                ReportType = null
            }))
            : await service.UpdateDraftAsync(UpdateCommand(
                "pvg-draft-legacy-shape",
                ValidUpdateRequest() with
                {
                    ProductLines = [SuspectLineRequest("suspect product update")],
                    AwarenessAtUtc = null,
                    EventCountry = null,
                    ReportType = null
                },
                1));

        Assert.Equal(PvgApplicationOutcome.Invalid, result.Result.Outcome);
        Assert.Equal(
            new[]
            {
                new PvgValidationFailure(PvgIntakeField.AwarenessAtUtc, PvgValidationReasonCodes.RequiredFieldMissing),
                new PvgValidationFailure(PvgIntakeField.EventCountry, PvgValidationReasonCodes.RequiredFieldMissing),
                new PvgValidationFailure(PvgIntakeField.ReportType, PvgValidationReasonCodes.RequiredFieldMissing)
            }.OrderBy(failure => failure.Field),
            result.Result.ValidationFailures.OrderBy(failure => failure.Field));
        Assert.Empty(callLog);
        AssertSafe(result);
    }

    [Theory]
    [InlineData(PvgIntakeOperation.Create)]
    [InlineData(PvgIntakeOperation.Update)]
    public async Task Trimmed_values_are_validated_and_stored_trimmed(PvgIntakeOperation operation)
    {
        var (service, store) = AllowedServiceWithStore();
        PvgIntakeProductLineRequest[] untrimmedLines = [new(" Suspect ", null, " Aspirin ", null, " L1 ")];

        var intakeDraftId = await CreateOrUpdateAsync(
            service,
            store,
            operation,
            ValidCreateRequest() with
            {
                EventCountry = " unknown ",
                ReporterCountry = "   ",
                ProductLines = untrimmedLines,
                ReportType = " Spontaneous ",
                ReporterNameOrInitials = " J.D. ",
                ReporterProfession = " Pharmacist "
            },
            ValidUpdateRequest() with
            {
                EventCountry = " unknown ",
                ReporterCountry = "   ",
                ProductLines = untrimmedLines,
                ReportType = " Spontaneous ",
                ReporterNameOrInitials = " J.D. ",
                ReporterProfession = " Pharmacist "
            });

        var stored = (await StoredAsync(store, intakeDraftId)).Intake;
        Assert.Equal("unknown", stored.EventCountry);
        Assert.Null(stored.ReporterCountry);
        Assert.Equal([new PvgIntakeProductLine("Suspect", null, "Aspirin", null, "L1")], stored.ProductLines);
        Assert.Equal("Spontaneous", stored.ReportType);
        Assert.Equal("J.D.", stored.ReporterNameOrInitials);
        Assert.Equal("Pharmacist", stored.ReporterProfession);

        var blankName = await service.CreateDraftAsync(CreateCommand(
            ValidCreateRequest() with { ProductLines = [SuspectLineRequest("   ")] }));

        AssertRejected(blankName, PvgIntakeField.ProductNameAsReported, PvgValidationReasonCodes.RequiredFieldMissing);
    }

    [Theory]
    [InlineData(PvgIntakeOperation.Create)]
    [InlineData(PvgIntakeOperation.Triage)]
    [InlineData(PvgIntakeOperation.Route)]
    public async Task Create_triage_and_route_audit_intents_carry_no_changed_fields(PvgIntakeOperation operation)
    {
        var service = NewService(
            new RecordingFieldSecurityPolicy([]),
            new RecordingWorkflowTransitionGate([]),
            new RecordingEvidenceLinkPort([]),
            new RecordingPermissionGate([]));

        var mutation = await MutateAsync(service, operation);

        Assert.True(mutation.Result.IsSuccess);
        var auditIntent = Assert.IsType<PvgAuditIntent>(mutation.AuditIntent);
        Assert.Equal(operation, auditIntent.Operation);
        Assert.Empty(auditIntent.ChangedFields);
    }

    private static (PvgIntakeDraftApplicationService Service, InMemoryPvgIntakeDraftRepository Store) AllowedServiceWithStore()
    {
        var store = new InMemoryPvgIntakeDraftRepository();
        var service = NewService(
            new RecordingFieldSecurityPolicy([]),
            new RecordingWorkflowTransitionGate([]),
            new RecordingEvidenceLinkPort([]),
            new RecordingPermissionGate([]),
            store);
        return (service, store);
    }

    private static PvgPersistenceTenantScope Scope() => new(TenantContext().TenantId);

    private static async ValueTask<PvgPersistedIntakeDraft> StoredAsync(IPvgIntakeDraftStore store, string intakeDraftId)
    {
        var persisted = await store.FindByIdAsync(Scope(), intakeDraftId);
        Assert.NotNull(persisted);
        return persisted;
    }

    private static async ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> AllStoredAsync(IPvgIntakeDraftStore store) =>
        await store.ListAsync(new PvgPersistenceListScope(Scope(), 1, 100, null));

    private static async ValueTask AssertNothingStoredAsync(IPvgIntakeDraftStore store) =>
        Assert.Empty(await AllStoredAsync(store));

    private static async ValueTask<string> CreateOrUpdateAsync(
        PvgIntakeDraftApplicationService service,
        IPvgIntakeDraftStore store,
        PvgIntakeOperation operation,
        PvgCreateIntakeDraftRequest createRequest,
        PvgUpdateIntakeDraftRequest updateRequest)
    {
        if (operation == PvgIntakeOperation.Create)
        {
            var created = await service.CreateDraftAsync(CreateCommand(createRequest));
            Assert.True(created.Result.IsSuccess);
            return created.IntakeDraftId!;
        }

        var prepared = await service.CreateDraftAsync(CreateCommand(ValidCreateRequest()));
        Assert.True(prepared.Result.IsSuccess);
        var version = (await StoredAsync(store, prepared.IntakeDraftId!)).Version;
        var updated = await service.UpdateDraftAsync(UpdateCommand(prepared.IntakeDraftId!, updateRequest, version));
        Assert.True(updated.Result.IsSuccess);
        return prepared.IntakeDraftId!;
    }

    private static void AssertRejected(
        PvgIntakeDraftMutationResult result,
        PvgIntakeField expectedField,
        string expectedReasonCode,
        params string[] submittedValues)
    {
        Assert.False(result.Result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Invalid, result.Result.Outcome);
        Assert.Contains(new PvgValidationFailure(expectedField, expectedReasonCode), result.Result.ValidationFailures);
        Assert.All(result.Result.ValidationFailures, failure => AssertSafeReasonCode(failure.ReasonCode));
        Assert.Null(result.IntakeDraftId);
        Assert.Null(result.AuditIntent);
        AssertDoesNotRender(result, submittedValues);
        AssertSafe(result);
    }

    private static void AssertDoesNotRender(object value, params string[] submittedValues)
    {
        var rendered = value.ToString()!;
        foreach (var submittedValue in submittedValues)
        {
            Assert.DoesNotContain(submittedValue, rendered, StringComparison.Ordinal);
        }
    }

    private static void AssertRevision04Fields(
        SafetyCaseIntake intake,
        IReadOnlyList<PvgIntakeProductLine> expectedLines,
        DateTimeOffset expectedAwareness,
        string expectedReportType,
        string expectedReporterName,
        string expectedReporterProfession,
        string? expectedReporterCountry)
    {
        Assert.Equal(expectedLines.Count, intake.ProductLines.Count);
        for (var index = 0; index < expectedLines.Count; index++)
        {
            Assert.Equal(expectedLines[index].ProductRole, intake.ProductLines[index].ProductRole);
            Assert.Equal(expectedLines[index].GlobalProductId, intake.ProductLines[index].GlobalProductId);
            Assert.Equal(expectedLines[index].ProductNameAsReported, intake.ProductLines[index].ProductNameAsReported);
            Assert.Equal(expectedLines[index].PresentationGskuId, intake.ProductLines[index].PresentationGskuId);
            Assert.Equal(expectedLines[index].LotNumber, intake.ProductLines[index].LotNumber);
        }

        Assert.Equal(expectedAwareness, intake.AwarenessAtUtc);
        Assert.Equal(TimeSpan.Zero, intake.AwarenessAtUtc.Offset);
        Assert.Null(intake.CompanyRecipientPersonId);
        Assert.Equal("unknown", intake.EventCountry);
        Assert.Equal(expectedReportType, intake.ReportType);
        Assert.Equal(expectedReporterName, intake.ReporterNameOrInitials);
        Assert.Equal(expectedReporterProfession, intake.ReporterProfession);
        Assert.Equal(expectedReporterCountry, intake.ReporterCountry);
        Assert.Equal(PvgReporterSource.Manual, intake.ReporterSource);
        Assert.Null(intake.ReporterSourceId);
        Assert.Equal(PvgReporterCrmSearchState.Pending, intake.ReporterCrmSearchState);
    }

    private static PvgIntakeProductLineRequest ToLineRequest(PvgIntakeProductLine line) =>
        new(line.ProductRole, line.GlobalProductId, line.ProductNameAsReported, line.PresentationGskuId, line.LotNumber);

    private static string LinkedSourceId(PvgReporterSource source) =>
        source == PvgReporterSource.Crm ? "crm-contact-secret-1" : "person-secret-1";

    // Linked record fixture (pack AC-29): placed directly in the store because the read contracts are absent.
    private static SafetyCaseIntake LinkedFixtureIntake(PvgReporterSource source) =>
        FixtureIntake(source, LinkedSourceId(source), PvgReporterCrmSearchState.Pending, FixtureReceivedAtUtc);

    private static SafetyCaseIntake FixtureIntake(
        PvgReporterSource reporterSource,
        string? reporterSourceId,
        PvgReporterCrmSearchState reporterCrmSearchState,
        DateTimeOffset awarenessAtUtc) =>
        new(
            TenantContext().TenantId,
            PvgIntakeStatus.IntakeCreated,
            "Portal",
            "Reporter",
            FixtureReceivedAtUtc,
            "HealthcareProfessional",
            "free text narrative with PHI",
            "Serious",
            "High")
        {
            SourceReference = "source-ref",
            ReporterContactSummary = "reporter@example.test",
            PatientSubjectCode = "patient-subject-code",
            EventOnsetDate = new DateOnly(2020, 2, 1),
            EvidenceLinkReferences = [],
            ProductLines = [new PvgIntakeProductLine("Suspect", null, "suspect product", null, "lot-1")],
            AwarenessAtUtc = awarenessAtUtc,
            CompanyRecipientPersonId = null,
            EventCountry = "unknown",
            ReportType = "Spontaneous",
            ReporterSource = reporterSource,
            ReporterSourceId = reporterSourceId,
            ReporterCrmSearchState = reporterCrmSearchState,
            ReporterNameOrInitials = "J.D.",
            ReporterProfession = "Pharmacist",
            ReporterCountry = null
        };

    // Sends back exactly the fixture values, with the awareness instant written in UTC.
    private static PvgUpdateIntakeDraftRequest FixtureUpdateRequest() =>
        new(
            "Portal",
            "Reporter",
            "source-ref",
            FixtureReceivedAtUtc,
            "HealthcareProfessional",
            "reporter@example.test",
            "patient-subject-code",
            new DateOnly(2020, 2, 1),
            "free text narrative with PHI",
            [new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, "lot-1")],
            "Serious",
            "High",
            [],
            FixtureReceivedAtUtc,
            null,
            "unknown",
            "Spontaneous",
            null,
            null,
            "J.D.",
            "Pharmacist",
            null,
            null);

    private static async ValueTask AssertLinkedFixtureUnchangedAsync(
        IPvgIntakeDraftStore store,
        string intakeDraftId,
        PvgReporterSource source,
        long expectedVersion)
    {
        var after = await StoredAsync(store, intakeDraftId);
        Assert.Equal(expectedVersion, after.Version);
        Assert.Equal(PvgIntakeStatus.IntakeCreated, after.Intake.Status);
        Assert.Equal(source, after.Intake.ReporterSource);
        Assert.Equal(LinkedSourceId(source), after.Intake.ReporterSourceId);
        Assert.Equal(PvgReporterCrmSearchState.Pending, after.Intake.ReporterCrmSearchState);
    }
}
