using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.PlannedVisit;

/// <summary>
/// MOD-0155 FU01 — pure structural validation, in-domain fail-closed vocabulary, the time-window and duration rules,
/// the deterministic consent Purpose/SubjectType maps and the plan state machine. No I/O, no fakes: these are the rules
/// that never need another row.
/// </summary>
public sealed class PlannedVisitValidationTests
{
    // ── VisitCode ────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void VisitCode_valid_passes()
        => Assert.Null(PlannedVisitValidation.ValidateVisitCode("PV-2026.01_a"));

    [Fact]
    public void VisitCode_empty_is_required()
        => Assert.Equal(PlannedVisitErrorCodes.CodeRequired, PlannedVisitValidation.ValidateVisitCode("  ")!.Code);

    [Fact]
    public void VisitCode_too_long_is_invalid()
        => Assert.Equal(PlannedVisitErrorCodes.CodeInvalid,
            PlannedVisitValidation.ValidateVisitCode(new string('a', 65))!.Code);

    [Fact]
    public void VisitCode_bad_characters_are_invalid()
        => Assert.Equal(PlannedVisitErrorCodes.CodeInvalid, PlannedVisitValidation.ValidateVisitCode("bad code!")!.Code);

    // ── Fail-closed vocabulary ───────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("account")]
    [InlineData("contact")]
    [InlineData("account-contact-link")]
    [InlineData("pharmacy")]
    public void TargetType_known_values_pass(string value)
        => Assert.Null(PlannedVisitValidation.ValidateVocabulary("TargetType", value, PlannedVisitTargetType.All));

    [Fact]
    public void TargetType_pharmacy_is_a_known_value()
        => Assert.True(PlannedVisitTargetType.IsKnown("pharmacy"));

    [Fact]
    public void TargetType_unknown_is_rejected_fail_closed()
        => Assert.Equal(PlannedVisitErrorCodes.UnsupportedVocabularyValue,
            PlannedVisitValidation.ValidateVocabulary("TargetType", "hcp", PlannedVisitTargetType.All)!.Code);

    [Fact]
    public void Purpose_unknown_is_rejected()
        => Assert.NotNull(PlannedVisitValidation.ValidateVocabulary("VisitPurpose", "xyz", PlannedVisitPurpose.All));

    [Fact]
    public void VisitType_unknown_is_rejected()
        => Assert.NotNull(PlannedVisitValidation.ValidateVocabulary("VisitType", "carrier-pigeon", PlannedVisitType.All));

    [Fact]
    public void Source_unknown_is_rejected()
        => Assert.NotNull(PlannedVisitValidation.ValidateVocabulary("Source", "telepathy", PlannedVisitSource.All));

    [Fact]
    public void ResourceType_unknown_is_rejected()
        => Assert.NotNull(PlannedVisitValidation.ValidateVocabulary("ResourceType", "robot", PlannedVisitResourceTypes.All));

    [Fact]
    public void SelectionMode_fail_closed()
    {
        Assert.True(PlannedVisitSelectionMode.IsKnown("manual"));
        Assert.False(PlannedVisitSelectionMode.IsKnown("whatever"));
    }

    [Fact]
    public void ContentSource_fail_closed()
    {
        Assert.True(PlannedVisitContentSource.IsKnown("strategy"));
        Assert.True(PlannedVisitContentSource.IsKnown("manual"));
        Assert.False(PlannedVisitContentSource.IsKnown("auto"));
    }

    // ── Time window + duration ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeWindow_both_empty_is_valid()
        => Assert.Null(PlannedVisitValidation.ValidateTimeWindow(null, null));

    [Fact]
    public void TimeWindow_only_one_side_is_invalid()
        => Assert.Equal(PlannedVisitErrorCodes.TimeWindowInvalid,
            PlannedVisitValidation.ValidateTimeWindow("09:00", null)!.Code);

    [Fact]
    public void TimeWindow_end_not_after_start_is_invalid()
        => Assert.NotNull(PlannedVisitValidation.ValidateTimeWindow("10:00", "09:00"));

    [Fact]
    public void TimeWindow_bad_format_is_invalid()
        => Assert.NotNull(PlannedVisitValidation.ValidateTimeWindow("9am", "10am"));

    [Fact]
    public void TimeWindow_valid_passes()
        => Assert.Null(PlannedVisitValidation.ValidateTimeWindow("09:00", "10:30"));

    [Fact]
    public void Duration_out_of_range_is_invalid()
    {
        Assert.NotNull(PlannedVisitValidation.ValidateDuration(0, null, null));
        Assert.NotNull(PlannedVisitValidation.ValidateDuration(1441, null, null));
    }

    [Fact]
    public void Duration_exceeding_window_is_invalid()
        => Assert.Equal(PlannedVisitErrorCodes.DurationInvalid,
            PlannedVisitValidation.ValidateDuration(120, "09:00", "10:00")!.Code);

    [Fact]
    public void Duration_within_window_passes()
        => Assert.Null(PlannedVisitValidation.ValidateDuration(45, "09:00", "10:00"));

    // ── Consent Purpose / SubjectType maps (§4.7) ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("medical-visit", "medical-visit")]
    [InlineData("follow-up", "medical-visit")]
    [InlineData("product-information", "product-information")]
    [InlineData("training", "training")]
    [InlineData("campaign", "campaign")]
    [InlineData("service", "service")]
    [InlineData("compliance", "compliance")]
    [InlineData("other", "other")]
    public void Consent_purpose_map_is_deterministic(string visitPurpose, string expected)
        => Assert.Equal(expected, PlannedVisitValidation.ToConsentPurpose(visitPurpose));

    [Theory]
    [InlineData("contact", ConsentSubjectType.Contact)]
    [InlineData("account-contact-link", ConsentSubjectType.AccountContactLink)]
    [InlineData("account", ConsentSubjectType.Account)]
    [InlineData("pharmacy", ConsentSubjectType.Account)] // D9 - pharmacy asks at the account level
    public void Consent_subject_type_map_is_deterministic(string targetType, string expected)
        => Assert.Equal(expected, PlannedVisitValidation.ToConsentSubjectType(targetType));

    // ── State machine (§12.2) ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("draft", "planned")]
    [InlineData("planned", "confirmed")]
    [InlineData("draft", "cancelled")]
    [InlineData("planned", "cancelled")]
    [InlineData("confirmed", "cancelled")]
    [InlineData("draft", "archived")]
    [InlineData("planned", "archived")]
    [InlineData("confirmed", "archived")]
    [InlineData("cancelled", "archived")]
    public void Valid_transitions_are_allowed(string from, string to)
        => Assert.True(PlannedVisitValidation.IsTransitionAllowed(from, to));

    [Theory]
    [InlineData("draft", "confirmed")]     // must be planned first
    [InlineData("confirmed", "planned")]   // no going back
    [InlineData("cancelled", "planned")]
    [InlineData("archived", "planned")]    // archived is terminal
    [InlineData("archived", "cancelled")]
    public void Invalid_transitions_are_rejected(string from, string to)
        => Assert.False(PlannedVisitValidation.IsTransitionAllowed(from, to));

    // ── Consent subject id resolution ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsentSubjectId_prefers_link_then_contact_then_account()
    {
        var link = new PlannedVisitEntity
        {
            TargetType = "account-contact-link", AccountContactLinkId = Guid.NewGuid(), AccountId = Guid.NewGuid()
        };
        Assert.Equal(link.AccountContactLinkId, PlannedVisitValidation.ConsentSubjectId(link));

        var contact = new PlannedVisitEntity { TargetType = "contact", ContactId = Guid.NewGuid() };
        Assert.Equal(contact.ContactId, PlannedVisitValidation.ConsentSubjectId(contact));

        var pharmacy = new PlannedVisitEntity { TargetType = "pharmacy", AccountId = Guid.NewGuid() };
        Assert.Equal(pharmacy.AccountId, PlannedVisitValidation.ConsentSubjectId(pharmacy));
    }
}
