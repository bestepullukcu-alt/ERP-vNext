namespace Diten.Web.Views.MasterData.CompetitorProducts;

/// <summary>
/// Localization marker for the WP-VW-W4 (W4-WEB-c) MDM competitor products pages (tenant module: 7 languages).
/// <see cref="ScriptKeys"/> are the strings the page script reads (window.CompetitorL10n) — ONE list, so the bridge serializes
/// exactly these and the tests check exactly these in every language.
/// </summary>
public sealed class CompetitorProductsIndex
{
    public static readonly string[] ScriptKeys =
    [
        "Crumb", "Title", "CrumbFull", "CrumbProposals", "ReadOnly", "NewButton", "TabRecords", "TabProposals", "FiltersTitle",
        "AllOption", "FilterOurs", "FilterCompany", "FilterIngredient", "FilterCountry", "FilterReimbursement", "FilterStatus",
        "SearchPlaceholder", "ExportButton", "ColName", "ColCompany", "ColIngredient", "ColForm", "ColOurs", "ColCountries",
        "ColUsage", "ColStatus", "ColLast", "ColActions", "Aka", "PricedText", "UsageNoAccess", "ViewDetails", "EditButton",
        "EmptyTitle", "EmptyText", "NoResults", "FooterText", "PrevPage", "NextPage", "ProposalsTitle", "ProposalsText",
        "NoProposals", "PropName", "PropCompany", "PropBy", "PropProduct", "PropUsage", "InReports", "SimilarRecords",
        "NoSimilar", "OpenDecision", "ActComplete", "ActMerge", "ActReject", "State_waiting", "State_draft", "State_in_review",
        "State_returned", "State_returned_sod", "State_returned_withdrawn", "State_applied", "Decision_complete",
        "Decision_merge", "Decision_reject", "FormNewTitle", "FormEditTitle", "FormCompleteTitle", "FormNewSub",
        "FormCompleteSub", "CancelButton", "SaveButton", "SaveDecision", "SendForApproval", "ProposalBanner",
        "ProposalBannerText", "FormErrors", "SectionIdentity", "SectionCompetition", "SectionCountries", "FieldName", "NameHelp",
        "FieldCompany", "FieldAliases", "AliasPlaceholder", "AliasHelp", "FromMerge", "FieldIngredients",
        "IngredientPlaceholder", "IngredientHelp", "FieldForm", "ChooseOption", "FormsUnavailable", "FieldStrength", "FieldAtc",
        "FieldOurs", "OursHelp", "ProductsNoAccess", "FieldIndications", "Optional", "IndicationHelp", "IndicationsNoAccess",
        "IndicationsPickProduct", "NoIndications", "UnknownIndication", "CountriesHelp", "AddCountry", "ProductSearch",
        "ProductsNoMatch", "ProductsMore", "CountrySearch", "MarketsUnavailable", "AddButton", "HistoryCount", "NoHistory",
        "RemoveRow", "RemoveLabel", "BoxPrice", "CurrencyLabel", "Reimbursement", "ValidFrom", "Source", "SourcePlaceholder",
        "NewPeriod", "NewPeriodDate", "NoCountries", "NoCountriesText", "ErrMarkets_duplicate", "ErrMarkets_price",
        "ErrMarkets_currency", "PreviousPeriods", "PeriodLabel", "PreviewTitle", "PreviewSub", "PreviewAs", "CompetitorTag",
        "FormStrength", "OurProduct", "PriceIn", "Since", "NoPriceFor", "ShownIndications", "ConflictTitle", "ConflictText",
        "RefreshButton", "Saved", "ErrNameRequired", "ErrCompanyRequired", "ErrIngredientsRequired", "ErrProductsRequired",
        "LeaveTitle", "LeaveText", "StayButton", "LeaveButton", "DetailSub", "MergeOther", "DeactivateButton", "DeactivateTitle",
        "DeactivateText", "Deactivated", "MergedInto", "IndicationCount", "CountryLabel", "SectionHistory", "SectionUsage",
        "LastThreeMonths", "UsageNone", "UsageUnavailable", "DoctorsLabel", "ReportsLabel", "ByProduct", "ByCountry",
        "UnknownCountry", "UsagePrivacy", "SectionMergeTrace", "TraceLine", "NoMergeTrace", "MarketSoon", "MarketSoonText",
        "MergeTitle", "MergeSource", "MergeSourceLine", "StepN", "MergeStepTarget", "MergeStepTargetSub", "MergeStepCompare",
        "MergeStepCompareSub", "MergeStepEffect", "MergeStepEffectSub", "SearchSurvivor", "SimilarBySimilarity", "NoCandidates",
        "NextButton", "BackButton", "CompareHelp", "FieldColumn", "WillMerge", "Survivor", "WillMergeShort", "SurvivorShort",
        "DifferentTag", "CountryRows", "CountryRowsHelp", "MoveColumn", "MoveLabel", "KeptLabel", "NoneLabel", "EffectTitle",
        "EffectReports", "EffectAlias", "EffectMove", "EffectNoMove", "EffectStatusProposal", "EffectStatusRecord",
        "EffectApproval", "Irreversible", "AckText", "MergeButton", "Merged", "SectionProposal", "SectionDecision",
        "SectionDecisionHistory", "NoDecision", "DecisionKind", "PreparedBy", "MergeTarget", "MovedCountries",
        "MergeEffectShort", "RejectReason", "AppliedRowLabel", "AppliedLine", "WithdrawButton", "ApproveButton", "ReturnToDraft",
        "SodNote", "HistSubmitted", "HistRound_approved", "HistRound_rejected", "HistRound_cancelled", "HistRound_timed_out",
        "HistRound_closed", "HistDecision_approve", "HistDecision_return", "DecisionSaved", "Sent", "RejectTitle",
        "RejectPlaceholder", "RejectInfo", "ErrReasonRequired", "ErrTooLong", "ReturnTitle", "ReturnNote", "ErrNoteRequired",
        "UserShort", "UnknownProduct", "ActionFailed", "Status_proposed", "Status_active", "Status_inactive", "Status_merged",
        "Status_rejected", "Field_name", "Field_company", "Field_aliases", "Field_activeIngredients", "Field_formCode",
        "Field_strength", "Field_atcCode", "Field_competesWith", "Field_indications", "Field_created", "Field_proposed",
        "Field_status", "Field_countryPrices", "Err_concurrency_conflict", "Err_forbidden", "Err_service_unavailable",
        "Err_gateway_unavailable", "Err_workflow_template_missing", "Err_approval_forbidden", "Err_workflow_unavailable",
        "Err_workflow_request_rejected", "Err_no_open_review", "Err_withdraw_not_possible", "Err_decision_invalid",
        "Err_rejection_comment_required", "Err_competitor_duplicate", "Err_competitor_market_duplicate",
        "Err_competitor_market_invalid", "Err_competitor_reimbursement_invalid", "Err_competitor_currency_invalid",
        "Err_competitor_form_invalid", "Err_competitor_invalid", "Err_competitor_not_found", "Err_competitor_not_editable",
        "Err_competitor_not_selectable", "Err_competitor_not_proposal", "Err_competitor_decision_locked",
        "Err_competitor_decision_missing", "Err_competitor_proposal_sod", "Err_competitor_merge_confirm_required",
        "Err_competitor_merge_self", "Err_reference_data_unavailable", "Err_tenant_supplied", "Form_tablet", "Form_capsule",
        "Form_syrup", "Form_injection", "Form_ointment", "Form_cream", "Form_drops", "Form_inhaler", "Form_patch", "Form_other",
        "Form_film_coated_tablet", "Form_modified_release_tablet", "Form_extended_release_tablet",
        "Form_modified_release_capsule", "Form_ampoule"
    ];

    /// <summary>The strings the views themselves write.</summary>
    public static readonly string[] ViewKeys =
    [
        "PageTitle", "Loading"
    ];
}
