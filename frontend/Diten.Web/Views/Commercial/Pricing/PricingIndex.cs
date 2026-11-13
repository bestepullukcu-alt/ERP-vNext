namespace Diten.Web.Views.Commercial.Pricing;

/// <summary>
/// Localization marker for the WP-VW-W6 (W6-WEB-b) screen D (MOD-0156: price lists · MF / discount rules · rule trial ·
/// change history · FX rates; tenant module: 7 languages, ONE resource for the five tabs and their pages). The script keys
/// travel in the standard L10n payload (each list module's _IndexL10n.cshtml → its index.l10n.js → window.L10n).
/// <see cref="ScriptKeys"/> are the strings the screen's scripts read; <see cref="ViewKeys"/> the strings only the views (and
/// the export's column headers) write. The rule messages are NOT here: they are W6-WEB-a's shared CommercialRules source.
/// </summary>
public sealed class PricingIndex
{
    // UXH-001 — the help codes of the pages (the price lists tab is the menu page COMMERCIALPRICING = COM-PRICELIST-LIST).
    public const string ListFormHelpCode = "COMPRICELISTFORM";
    public const string ImportHelpCode = "COMPRICELISTIMPORT";
    public const string RuleListHelpCode = "COMPRICINGRULELIST";
    public const string RuleFormHelpCode = "COMPRICINGRULEFORM";
    public const string TrialHelpCode = "COMPRICINGTRIAL";
    public const string HistoryHelpCode = "COMPRICINGHISTORY";
    public const string FxHelpCode = "COMFXRATELIST";

    /// <summary>Every help code of the screen (the menu page's first).</summary>
    public static readonly string[] HelpCodes = ["COMMERCIALPRICING", ListFormHelpCode, ImportHelpCode, RuleListHelpCode, RuleFormHelpCode, TrialHelpCode, HistoryHelpCode, FxHelpCode];

    public static readonly string[] ScriptKeys =
    [
        "NewList", "NewRule", "NewRate", "QuickView", "EmptyTitle", "EmptyText", "ColCompany", "ColCountry", "ColCurrency", "ColKind",
        "ColValidity", "ColItems", "ColVersion", "ColSku", "ColBarcode", "ColDepotPrice", "ColPharmacistPrice", "ColPublicPrice", "ColVat",
        "ColDiff", "ColProduct", "ColMaxDiscount", "ColMaxMf", "ColMaxCombined", "ColQuantity", "ColUnitPrice", "ColPair", "ColRateDate",
        "ColRate", "ColSource", "ColUpdated", "ColRowNo", "ColSourceCode", "ColRowStatus", "CompanyAll", "CountryAll", "CurrencyAll",
        "KindAll", "Kind_official", "Kind_company", "StatusAll", "ProductLabel", "ProductPlaceholder", "AllOption", "SearchHint",
        "Searching", "NoMatch", "TypeAll", "ItemsCount", "ItemsShown", "ValidityRange", "ValidityFrom", "AlwaysValid", "Badge_draft",
        "Badge_in_review", "Badge_planned", "Badge_in_force", "Badge_past", "Badge_discarded", "Badge_unknown", "RuleSt_draft",
        "RuleSt_in_review", "RuleSt_active", "RuleSt_superseded", "RuleSt_retired", "RuleSt_discarded", "RuleSt_unknown", "Change_revise",
        "Change_retire", "Specificity_0", "Specificity_1", "Specificity_2", "Specificity_3", "SpecificityLabel", "Outcome_approved",
        "Outcome_rejected", "Outcome_cancelled", "Outcome_timed_out", "Outcome_approve", "Outcome_publish", "Outcome_return", "RoundOpen",
        "RoundLine", "DecisionLine", "ReasonLine", "NoRounds", "ApprovalTitle", "Publish", "ReturnDraft", "DecisionNote", "FourEyesOwn",
        "PublishConfirm", "ReturnTitle", "ReturnText", "PublishedToast", "ReturnedToast", "ApprovedToast", "SubmittedToast",
        "RetireRequestedToast", "SubmitConfirm", "SubmitRuleConfirm", "DiscardConfirm", "RetireConfirm", "DraftSaved", "RemoveRow",
        "NoItems", "PublishedLine", "Diff_added", "Diff_removed", "Diff_changed", "Diff_unchanged", "Diff_vatChanged", "ErrListHead",
        "ErrItemSku", "ErrPrice", "ErrNoTier", "ErrVat", "ErrImportHead", "Imp_total", "Imp_matched", "Imp_unmatched", "Imp_retired",
        "Imp_duplicate", "Imp_invalidPrice", "Diff_addedCount", "Diff_removedCount", "Diff_changedCount", "Diff_vatChangedCount",
        "NoCompared", "RowStatusAll", "Row_matched", "Row_unmatched", "Row_retired", "Row_duplicate", "Row_invalid_price", "Row_removed",
        "NoRows", "DiscardImportConfirm", "ConvertedText", "QuantityLimits", "MfRatioIs", "SaveRevision", "ErrMf", "ErrPct", "ErrCount",
        "ErrRuleKey", "Tier_pharmacist_purchase", "Tier_depot", "Tier_public", "TrialMode", "ModePricedWith", "ModeQuantityOnly",
        "TrialPriceList", "TrialFree", "TrialMfRatio", "TrialCombined", "TrialNet", "TrialVat", "TrialRule", "TrialNoRule",
        "TrialNoFindings", "RuleRef", "ErrTrialCompany", "Type_price_list", "Type_price_list_item", "Type_pricing_rule", "Type_fx_rate",
        "Type_unknown", "Act_created", "Act_updated", "Act_version_created", "Act_discarded", "Act_submitted", "Act_published",
        "Act_returned", "Act_refused", "Act_withdrawn", "Act_superseded", "Act_retired", "Act_unknown", "ErrRateHead", "ErrRate",
        "Err_generic", "Err_unavailable", "Err_permission_denied", "Err_not_found", "Err_conflict",
        // W6-FIX-b (G-6) — the shared confirmation names the act with its button's own words
        "SubmitForPublishing", "SubmitForApproval", "DiscardDraft", "Retire", "DiscardImport"
    ];

    public static readonly string[] ViewKeys =
    [
        "ScreenTitle", "ScreenDescription", "TabLists", "TabRules", "TabTrial", "TabHistory", "TabFx", "PageTitle", "PageDescription",
        "ListsNote", "ImportButton", "CloseButton", "OpenDetails", "DrawerTitleList", "DrawerTitleRule", "DrawerTitleChange",
        "DrawerTitleRate", "ColName", "ColStatus", "ColActions", "ColQty", "ColMf", "ColDiscount", "ColWhen", "ColWho", "ColWhat",
        "ColChanges", "CompanyLabel", "CountryLabel", "CurrencyLabel", "KindLabel", "StatusLabel", "TypeLabel", "FromDate", "ToDate",
        "FromCurrencyLabel", "ToCurrencyLabel", "SoftLabel", "HardLabel", "SoftLegend", "HardLegend", "ListCreateTitle", "ListEditTitle",
        "ListDetailsTitle", "ListHeadSection", "ItemsSection", "ItemsHelp", "AddItem", "NameLabel", "CountryFromCompany", "ValidFromLabel",
        "ValidToLabel", "BackToList", "SaveDraft", "EditDraft", "NewVersion", "DiffToggle",
        "ImportTitle", "StepFile", "StepPreview", "StepPublish", "FileLabel", "FileHelp", "ColumnsTitle", "ColumnsHelp", "ColumnSku",
        "ColumnDepot", "ColumnPharmacist", "ColumnPublic", "ColumnVat", "ColumnGtin", "GtinOffNote", "Upload", "OnlyMatchedNote",
        "RowStatusLabel", "ConvertToDraft", "OpenDraft", "RulesTitle", "RulesNote", "RuleCreateTitle", "RuleEditTitle",
        "RuleDetailsTitle", "RuleKeySection", "LimitsSection", "SegmentLabel", "SegmentSoon", "Optional", "MaxDiscountLabel", "MaxMfLabel",
        "MaxCombinedLabel", "QuantityLabel", "QuantityMin", "QuantitySoftMax", "QuantityHardMax", "MfRatioHelp", "Revise",
        "VersionsTitle", "TrialTitle", "TrialResult", "TrialEmpty", "TryButton", "AsOfLabel", "TierLabel",
        "HistoryTitle", "FxTitle", "FxCreateTitle", "FxEditTitle", "RateSection", "SaveRate", "EditRate",
        // W6-FIX-b (G-1) — each tab says its OWN sentence under its title (they all said the price lists' one)
        "RulesDescription", "HistoryDescription", "FxDescription"
    ];
}
