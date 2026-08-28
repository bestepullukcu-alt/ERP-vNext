namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>
/// MOD-0151 FU08 workbook contract (pack §22.5): sheet names, canonical column order and the fixed protocol keywords.
///
/// <para>The same schema drives the import template, the data export and the import reader, so an exported file
/// round-trips into the reader column-for-column. Only the sheets listed in <see cref="ImportableSheets"/> are ever
/// read back; <see cref="CoverageSummarySheet"/> and <see cref="PlanVsCurrentSheet"/> are export-only read models and
/// are structurally excluded from import.</para>
/// </summary>
public static class TerritoryWorkbookSchema
{
    public const string TemplateVersion = "MOD-0151-FU08-v1";

    // ---- sheets ----------------------------------------------------------------------------------------------

    public const string ValidationNotesSheet = "ValidationNotes";
    public const string ModelSheet = "Model";
    public const string NodesSheet = "Nodes";
    public const string AssignmentRulesSheet = "AssignmentRules";
    public const string AccountAssignmentsSheet = "AccountAssignments";
    public const string ResourceAssignmentsSheet = "ResourceAssignments";
    public const string CoverageSummarySheet = "CoverageSummary";
    public const string PlanVsCurrentSheet = "PlanVsCurrent";
    public const string ReferenceValuesSheet = "ReferenceValues";

    /// <summary>The seven template sheets, in file order.</summary>
    public static readonly IReadOnlyList<string> TemplateSheets = new[]
    {
        ValidationNotesSheet, ModelSheet, NodesSheet, AssignmentRulesSheet,
        AccountAssignmentsSheet, ResourceAssignmentsSheet, ReferenceValuesSheet
    };

    /// <summary>Sheets the reader parses. CoverageSummary / PlanVsCurrent are absent on purpose — they are derived
    /// read models and importing them would let the file contradict its own source.</summary>
    public static readonly IReadOnlyList<string> ImportableSheets = new[]
    {
        ModelSheet, NodesSheet, AssignmentRulesSheet, AccountAssignmentsSheet, ResourceAssignmentsSheet
    };

    /// <summary>Export-only read models. A row on one of these sheets is never applied.</summary>
    public static readonly IReadOnlyList<string> ExportOnlySheets = new[]
    {
        CoverageSummarySheet, PlanVsCurrentSheet
    };

    // ---- columns ---------------------------------------------------------------------------------------------

    public const string OperationColumn = "Operation";

    /// <summary>Never accepted as input: tenancy comes from the caller's claim, never from the file (pack §22.5).</summary>
    public const string TenantIdColumn = "TenantId";

    /// <summary>Explicit "empty this field" token. A blank cell means "leave unchanged", so clearing must be said.</summary>
    public const string ClearToken = "<CLEAR>";

    public static readonly IReadOnlyList<string> ModelColumns = new[]
    {
        OperationColumn, "ModelId", "ModelCode", "Name", "CountryScope", "BusinessUnitScopes",
        "EffectiveFrom", "EffectiveTo", "Status", "ChangeReason"
    };

    public static readonly IReadOnlyList<string> NodeColumns = new[]
    {
        OperationColumn, "NodeId", "TerritoryCode", "Name", "TerritoryLevel", "ParentTerritoryCode",
        "CountryCode", "DivisionCode", "RegionCode", "AreaCode", "ZoneCode", "MicroZoneCode",
        "EffectiveFrom", "EffectiveTo", "SortOrder", "Status"
    };

    public static readonly IReadOnlyList<string> AssignmentRuleColumns = new[]
    {
        OperationColumn, "RuleId", "RuleCode", "Name", "RuleType", "TargetTerritoryCode", "ConflictPolicy",
        "Priority", "IsEnabled", "EffectiveFrom", "EffectiveTo",
        "CountryRefs", "CityRefs", "DistrictRefs", "AccountTypes", "AccountCategories", "AccountStatuses"
    };

    public static readonly IReadOnlyList<string> AccountAssignmentColumns = new[]
    {
        OperationColumn, "AssignmentId", "AccountId", "AccountCode", "AccountName", "TerritoryCode",
        "BusinessUnitScopes", "EffectiveFrom", "EffectiveTo", "ConflictPolicy", "Override", "OverrideReason",
        "AssignmentStatus", "AssignmentSource", "AppliedRuleCode", "EndedAt"
    };

    public static readonly IReadOnlyList<string> ResourceAssignmentColumns = new[]
    {
        OperationColumn, "AssignmentId", "TerritoryCode", "PositionCode", "PositionTitle", "ResourceId",
        "ResourceDisplayName", "BusinessUnitScopes", "CoverageScope", "IsPrimary",
        "ValidFrom", "ValidTo", "Status", "ChangeReason"
    };

    public static readonly IReadOnlyList<string> CoverageSummaryColumns = new[]
    {
        "AccountId", "AccountCode", "AccountName", "EffectiveAt", "HasCurrentCoverage",
        "TerritoryCode", "TerritoryName", "BusinessUnitScopes", "EffectiveFrom", "EffectiveTo"
    };

    public static readonly IReadOnlyList<string> PlanVsCurrentColumns = new[]
    {
        "TerritoryCode", "PositionCode", "DiffType", "PlannedResourceId", "PlannedResourceDisplayName",
        "CurrentResourceId", "CurrentResourceDisplayName", "PlannedEffectiveFrom", "CurrentEffectiveFrom", "Reason"
    };

    public static readonly IReadOnlyList<string> ReferenceValueColumns = new[]
    {
        "SetCode", "ValueCode", "DisplayName", "Description", "IsActive", "IsDeprecated", "Attributes"
    };

    /// <summary>System-owned identity columns: written by the export, used for matching, never invented by hand.</summary>
    public static readonly IReadOnlyList<string> SystemColumns = new[]
    {
        "ModelId", "NodeId", "RuleId", "AssignmentId"
    };

    /// <summary>Read-only helper columns: exported for readability and ignored on import.</summary>
    public static readonly IReadOnlyList<string> ReadOnlyHelperColumns = new[]
    {
        "Status", "AssignmentStatus", "AssignmentSource", "AppliedRuleCode", "EndedAt", "AccountName", "PositionTitle"
    };

    public const string NotPublishedMarker = "NOT_PUBLISHED";

    /// <summary>Columns bound to a MOD-0048 set, per sheet — drives the in-cell dropdowns.</summary>
    public static IReadOnlyDictionary<string, string> ColumnSetsFor(string sheet) => sheet switch
    {
        ModelSheet => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BusinessUnitScopes"] = TerritoryReferenceSets.BusinessUnitValueSet
        },
        NodesSheet => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TerritoryLevel"] = TerritoryReferenceSets.TerritoryLevel
        },
        AssignmentRulesSheet => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RuleType"] = TerritoryReferenceSets.TerritoryRuleType,
            ["ConflictPolicy"] = TerritoryReferenceSets.TerritoryConflictPolicy
        },
        AccountAssignmentsSheet => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConflictPolicy"] = TerritoryReferenceSets.TerritoryConflictPolicy,
            ["BusinessUnitScopes"] = TerritoryReferenceSets.BusinessUnitValueSet
        },
        _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    };

    public static IReadOnlyList<string> ColumnsFor(string sheet) => sheet switch
    {
        ModelSheet => ModelColumns,
        NodesSheet => NodeColumns,
        AssignmentRulesSheet => AssignmentRuleColumns,
        AccountAssignmentsSheet => AccountAssignmentColumns,
        ResourceAssignmentsSheet => ResourceAssignmentColumns,
        CoverageSummarySheet => CoverageSummaryColumns,
        PlanVsCurrentSheet => PlanVsCurrentColumns,
        ReferenceValuesSheet => ReferenceValueColumns,
        _ => Array.Empty<string>()
    };

    /// <summary>The identity/decision columns whose absence is a FILE-level error (row messages would be meaningless).</summary>
    public static IReadOnlyList<string> MandatoryColumnsFor(string sheet) => sheet switch
    {
        ModelSheet => new[] { OperationColumn },
        NodesSheet => new[] { OperationColumn, "TerritoryCode" },
        AssignmentRulesSheet => new[] { OperationColumn, "RuleCode" },
        AccountAssignmentsSheet => new[] { OperationColumn },
        ResourceAssignmentsSheet => new[] { OperationColumn },
        _ => Array.Empty<string>()
    };

    public static int ColumnIndex(IReadOnlyList<string> columns, string column)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], column, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return 0;
    }
}

/// <summary>Allowed values of the <c>Operation</c> column. A fixed protocol keyword list, not reference data.</summary>
public static class TerritoryImportOperations
{
    public const string Add = "add";
    public const string Update = "update";
    public const string End = "end";
    public const string Skip = "skip";

    /// <summary>Accepted synonym of <see cref="Add"/>.</summary>
    public const string Create = "create";

    /// <summary>Never supported: MOD-0151 records are ended/archived, never destroyed.</summary>
    public const string Delete = "delete";

    public static readonly IReadOnlyList<string> Selectable = new[] { Add, Update, End, Skip };

    public static string? Normalize(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant();
        return value switch
        {
            null or "" => null,
            Create => Add,
            _ => value
        };
    }
}

/// <summary>Row outcome categories shown in the dry-run preview and the apply report.</summary>
public static class TerritoryImportRowStatuses
{
    public const string Create = "create";
    public const string Update = "update";
    public const string End = "end";
    public const string Skip = "skip";
    public const string NoChange = "no_change";
    public const string Error = "error";
    public const string Conflict = "conflict";
    public const string SkippedDependency = "skipped_dependency";
    public const string Applied = "applied";
    public const string NotApplied = "not_applied";
}

/// <summary>Stable, machine-readable dry-run error codes. The UI localises on these, never on the message text.</summary>
public static class TerritoryImportErrorCodes
{
    public const string OperationMissing = "operation_missing";
    public const string UnsupportedOperation = "unsupported_operation";
    public const string RequiredFieldMissing = "required_field_missing";
    public const string InvalidDataType = "invalid_data_type";
    public const string DuplicateRow = "duplicate_row";
    public const string DuplicateNodeCode = "duplicate_node_code";
    public const string InvalidParent = "invalid_parent";
    public const string HierarchyCycle = "hierarchy_cycle";
    public const string InvalidTerritoryLevel = "invalid_territory_level";
    public const string LevelOrderViolation = "level_order_violation";
    public const string InvalidBusinessUnitScope = "invalid_business_unit_scope";
    public const string ModelScopeOverflow = "model_scope_overflow";
    public const string InvalidCountryScope = "invalid_country_scope";
    public const string InvalidRuleType = "invalid_rule_type";
    public const string InvalidConflictPolicy = "invalid_conflict_policy";
    public const string InvalidTargetNode = "invalid_target_node";
    public const string InvalidAccount = "invalid_account";
    public const string UnresolvedAccountReference = "unresolved_account_reference";
    public const string CrossTenantAccount = "cross_tenant_account";
    public const string WindowContainment = "window_containment";
    public const string InvalidDateWindow = "invalid_date_window";
    public const string ActiveModelOverlap = "active_model_overlap";
    public const string ModelNotEditable = "model_not_editable";
    public const string ModelNotActive = "model_not_active";
    public const string ReferenceSetNotPublished = "reference_set_not_published";
    public const string TenantIdColumnIgnored = "tenant_id_column_ignored";
    public const string ImmutableField = "immutable_field";
    public const string NotFound = "not_found";
    public const string AlreadyEnded = "already_ended";
    public const string DuplicateAssignment = "duplicate_assignment";
    public const string OverrideReasonRequired = "override_reason_required";
    public const string ResourceApplyNotSupported = "resource_apply_not_supported";
    public const string InvalidPositionCode = "invalid_position_code";
    public const string InvalidResourceRef = "invalid_resource_ref";
    public const string NoChange = "no_change";
    public const string SheetBlocked = "sheet_blocked";
}
