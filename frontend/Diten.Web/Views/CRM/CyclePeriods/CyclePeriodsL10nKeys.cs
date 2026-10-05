namespace Diten.Web.Views.CRM.CyclePeriods;

/// <summary>
/// WP-CYC-UI-1 — the Cycle Periods localization keys added for the new screens, in ONE list the views, the JS bridge
/// and the tests share. A tenant module ships all seven languages (en, tr, fr, es, zh, ar, ru); a key missing in one of
/// them is an unfinished screen.
/// </summary>
public static class CyclePeriodsL10nKeys
{
    /// <summary>Every key WP-CYC-UI-1 added to <c>CyclePeriodsIndex.*.resx</c>.</summary>
    public static readonly IReadOnlyList<string> NewKeys =
    [
        "CalendarDays", "CalendarForbidden", "CalendarLoadFailed", "CalendarNoCountry", "CalendarResolved",
        "CalendarSummary", "CalendarUnresolved", "CampaignStatus_active", "CampaignStatus_archived",
        "CampaignStatus_cancelled", "CampaignStatus_completed", "CampaignStatus_draft", "CampaignStatus_paused",
        "Campaigns", "Capacity", "CapacityArchived", "CheckActiveOverlap", "CheckClear", "CheckEndNotAfterStart",
        "CheckOtherLevelOverlap", "CheckSequenceTaken", "ChecksSection", "CloseAndReopen", "CloseAndReopenConfirm",
        "ClosedBanner", "ClosedReadOnly", "CodeImmutableHint", "CodeSuggestedHint", "CreateCapacity",
        "CycleCapacities", "DateTemplates", "Dates", "DatesSection", "Days", "DaysAndWorkingDays", "DaysUnit",
        "FinderAmbiguous", "FinderAnsweredBy", "FinderDate", "FinderHint", "FinderIdle", "FinderNone", "FinderOpen",
        "FinderResolved", "FinderSubmit", "FinderTitle", "FinderUnit", "GoToDetails", "HasCapacity", "LifecycleNote", "LoadFailed",
        "Month", "NameSuggestionPattern", "NoCampaigns", "NoCapacity", "NoCapacityBandTitle", "NoCapacityClosed",
        "NoCapacityHint", "NoCapacityTitle", "NoDescription", "NoPlannedVisits", "NoPlanningSessions",
        "NonWorkingDays", "NotActivatedYet", "NotClosedYet", "OpenCapacity", "PartialMonth", "PartialMonthHint",
        "PlannedVisits", "PlanningSessions", "Retry", "SaveAsDraft", "ScopeCardHint_business-unit",
        "ScopeCardHint_country", "ScopeCardHint_legal-entity", "ScopeCardHint_tenant", "SequenceSuggestion", "StatusLine",
        "SessionStatus_archived", "SessionStatus_committed", "SessionStatus_draft", "SessionStatus_generated",
        "Suggestion", "SummarySection", "TemplateH1", "TemplateH2", "TemplateMonth", "TemplateQ1", "TemplateQ2",
        "TemplateQ3", "TemplateQ4", "TimelineConflict", "TimelineEmpty", "TimelineFootnote", "TimelineGap",
        "TimelineGapLabel", "TimelineGapTitle", "TimelineOverlap", "TimelineOverlapTitle", "TimelineToday",
        "UsageLoadFailed", "UseSuggestion", "ViewTable", "ViewTimeline", "VisitCountPattern", "VisitStatus_archived",
        "VisitStatus_cancelled", "VisitStatus_confirmed", "VisitStatus_draft", "VisitStatus_planned",
        "WorkingDaysUnit", "YearAndSequence"
    ];

    /// <summary>The keys the browser scripts read through the l10n bridge (<c>cycleperiods-l10n-ext</c>).</summary>
    public static readonly IReadOnlyList<string> Bridge =
    [
        "CalendarForbidden", "CalendarLoadFailed", "CalendarNoCountry", "CalendarResolved", "CalendarUnresolved",
        "CampaignStatus_active", "CampaignStatus_archived", "CampaignStatus_cancelled", "CampaignStatus_completed",
        "CampaignStatus_draft", "CampaignStatus_paused", "CapacityArchived", "CheckActiveOverlap", "CheckClear",
        "CheckEndNotAfterStart", "CheckOtherLevelOverlap", "CheckSequenceTaken", "CloseAndReopenConfirm",
        "CodeImmutableHint", "CodeSuggestedHint", "CreateCapacity", "DaysUnit", "FinderAmbiguous",
        "FinderAnsweredBy", "FinderDate", "FinderHint", "FinderIdle", "FinderNone", "FinderOpen", "FinderResolved",
        "FinderSubmit", "FinderTitle", "FinderUnit", "HasCapacity", "NameSuggestionPattern", "NoCampaigns",
        "NoCapacity", "NoCapacityClosed", "NoCapacityHint", "NoCapacityTitle", "NoPlannedVisits",
        "NoPlanningSessions", "OpenCapacity", "PartialMonth", "PartialMonthHint", "PlannedVisits", "SaveAsDraft",
        "SequenceSuggestion", "SessionStatus_archived", "SessionStatus_committed", "SessionStatus_draft",
        "SessionStatus_generated", "Suggestion", "TemplateMonth", "TimelineGapLabel", "TimelineGapTitle",
        "TimelineOverlapTitle", "UsageLoadFailed", "VisitCountPattern", "VisitStatus_archived",
        "VisitStatus_cancelled", "VisitStatus_confirmed", "VisitStatus_draft", "VisitStatus_planned",
        "WorkingDaysUnit"
    ];
}
