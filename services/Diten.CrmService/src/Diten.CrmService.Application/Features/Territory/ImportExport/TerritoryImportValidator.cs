using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>A validated row plus the action the apply step would take. Never persisted.</summary>
public sealed class TerritoryImportPlanRow
{
    public required TerritoryParsedRow Source { get; init; }
    public required string Sheet { get; init; }
    public required string EntityType { get; init; }
    public string? Operation { get; set; }
    public string Status { get; set; } = TerritoryImportRowStatuses.Skip;
    public string Severity { get; set; } = TerritoryImportSeverities.Info;
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
    public bool Blocking { get; set; }
    public string? ResolvedKey { get; set; }
    public List<string> ChangedFields { get; } = [];

    /// <summary>The mutation the apply step performs for this row. Null for skip/error rows.</summary>
    public Action<TerritoryImportApplyBuffer>? Action { get; set; }

    public TerritoryImportRowResultDto ToDto() => new(
        Sheet, Source.RowNumber, Severity, ErrorCode, Message, SuggestedFix, Blocking,
        Operation, EntityType, ResolvedKey, ChangedFields, Status);

    public TerritoryImportPlanRow Fail(string code, string message, string? fix = null)
    {
        Status = TerritoryImportRowStatuses.Error;
        Severity = TerritoryImportSeverities.Error;
        ErrorCode = code;
        Message = message;
        SuggestedFix = fix;
        Blocking = true;
        Action = null;
        return this;
    }

    public TerritoryImportPlanRow Conflict(string code, string message, string? fix = null)
    {
        Fail(code, message, fix);
        Status = TerritoryImportRowStatuses.Conflict;
        return this;
    }

    public TerritoryImportPlanRow Skip(string code, string message)
    {
        Status = TerritoryImportRowStatuses.Skip;
        Severity = TerritoryImportSeverities.Info;
        ErrorCode = code;
        Message = message;
        Blocking = false;
        Action = null;
        return this;
    }

    public TerritoryImportPlanRow NoChange(string message)
    {
        Skip(TerritoryImportErrorCodes.NoChange, message);
        Status = TerritoryImportRowStatuses.NoChange;
        return this;
    }

    public TerritoryImportPlanRow Plan(string status, string message, Action<TerritoryImportApplyBuffer> action)
    {
        Status = status;
        Severity = TerritoryImportSeverities.Info;
        Message = message;
        Blocking = false;
        Action = action;
        return this;
    }
}

/// <summary>Collects what an apply actually wrote, per sheet, so a partial apply is always reportable.</summary>
public sealed class TerritoryImportApplyBuffer
{
    public List<TerritoryNode> NodeInserts { get; } = [];
    public List<TerritoryNode> NodeUpdates { get; } = [];
    public List<TerritoryAssignmentRule> RuleInserts { get; } = [];
    public List<TerritoryAssignmentRule> RuleUpdates { get; } = [];
    public List<AccountTerritoryAssignment> AssignmentInserts { get; } = [];
    public List<AccountTerritoryAssignment> AssignmentEnds { get; } = [];
    public TerritoryModel? ModelUpdate { get; set; }
}

/// <summary>
/// MOD-0151 FU08 dry-run validator (pack §22.5). Produces a plan; it never writes and never mutates the context.
///
/// <para>Sheet order is <c>Model → Nodes → AssignmentRules → AccountAssignments</c>, so a node created in the same
/// file can be referenced by a rule or an assignment further down. A row that depends on a blocked earlier row is
/// reported as <c>skipped_dependency</c>, never as a misleading "not found".</para>
/// </summary>
public sealed class TerritoryImportValidator
{
    private readonly TerritoryImportContext _context;
    private readonly Guid _tenantId;

    /// <summary>Nodes created by this file, keyed by code — the in-file forward references.</summary>
    private readonly Dictionary<string, TerritoryNode> _pendingNodes = new(StringComparer.OrdinalIgnoreCase);

    public TerritoryImportValidator(TerritoryImportContext context, Guid tenantId)
    {
        _context = context;
        _tenantId = tenantId;
    }

    public List<TerritoryImportPlanRow> Validate(TerritoryParsedWorkbook workbook)
    {
        var rows = new List<TerritoryImportPlanRow>();
        rows.AddRange(ValidateModel(workbook.Rows(TerritoryWorkbookSchema.ModelSheet)));
        rows.AddRange(ValidateNodes(workbook.Rows(TerritoryWorkbookSchema.NodesSheet)));
        rows.AddRange(ValidateRules(workbook.Rows(TerritoryWorkbookSchema.AssignmentRulesSheet)));
        rows.AddRange(ValidateAccountAssignments(workbook.Rows(TerritoryWorkbookSchema.AccountAssignmentsSheet)));
        rows.AddRange(ValidateResourceAssignments(workbook.Rows(TerritoryWorkbookSchema.ResourceAssignmentsSheet)));
        return rows;
    }

    // ---- Model ---------------------------------------------------------------------------------------------------

    private IEnumerable<TerritoryImportPlanRow> ValidateModel(IReadOnlyList<TerritoryParsedRow> source)
    {
        foreach (var raw in source)
        {
            var row = Start(raw, TerritoryWorkbookSchema.ModelSheet, "TerritoryModel");
            row.ResolvedKey = _context.Model.ModelCode;
            if (IsSkipped(row)) { yield return row; continue; }

            if (row.Operation is not TerritoryImportOperations.Update)
            {
                yield return row.Fail(TerritoryImportErrorCodes.UnsupportedOperation,
                    "The Model sheet supports 'update' only; the import always targets the model in the URL.",
                    "Set Operation to 'update', or leave it empty to skip this sheet.");
                continue;
            }

            if (!_context.IsModelDraft)
            {
                yield return row.Fail(TerritoryImportErrorCodes.ModelNotEditable,
                    $"Model metadata can only be changed while the model is 'draft' (this model is '{_context.Model.Status}').",
                    "Deactivate the model to a draft version, or remove this row.");
                continue;
            }

            if (raw.Has("ModelId") && (!TerritoryImportValues.TryGuid(raw.Get("ModelId"), out var id) || id != _context.Model.Id))
            {
                yield return row.Fail(TerritoryImportErrorCodes.ImmutableField,
                    "ModelId does not match the model this import targets.",
                    "Leave ModelId empty or use the value from a fresh export.");
                continue;
            }

            if (raw.Has("ModelCode") && !TerritoryImportContext.Is(raw.Get("ModelCode"), _context.Model.ModelCode))
            {
                yield return row.Fail(TerritoryImportErrorCodes.ImmutableField,
                    "ModelCode cannot be changed by an import.",
                    "Restore the exported ModelCode, or rename the model on screen.");
                continue;
            }

            var draft = Clone(_context.Model);
            var changed = row.ChangedFields;

            if (raw.Has("Name"))
            {
                if (raw.IsClear("Name"))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "Name is required and cannot be cleared.");
                    continue;
                }
                if (!TerritoryImportContext.Is(draft.Name, raw.Get("Name"))) { draft.Name = raw.Get("Name")!; changed.Add("Name"); }
            }

            if (raw.Has("CountryScope"))
            {
                var country = raw.IsClear("CountryScope") ? null : raw.Get("CountryScope");
                if (!TerritoryImportContext.Is(draft.CountryScope, country)) { draft.CountryScope = country; changed.Add("CountryScope"); }
            }

            if (raw.Has("BusinessUnitScopes"))
            {
                var scopes = raw.IsClear("BusinessUnitScopes") ? [] : TerritoryImportValues.SplitList(raw.Get("BusinessUnitScopes"));
                var unpublished = scopes.Where(s => !_context.IsPublished(TerritoryReferenceSets.BusinessUnitValueSet, s)).ToList();
                if (unpublished.Count > 0)
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidBusinessUnitScope,
                        $"{unpublished.Count} business-unit scope value(s) are not published in MOD-0048.",
                        "Use a value from the ReferenceValues sheet.");
                    continue;
                }

                if (!ScopeSet(draft.BusinessScopes).SetEquals(scopes.Select(s => s.ToUpperInvariant())))
                {
                    draft.BusinessScopes = scopes
                        .Select(s => new TerritoryBusinessScope { ScopeType = TerritoryReferenceSets.BusinessUnitScopeType, ScopeCode = s })
                        .ToList();
                    changed.Add("BusinessUnitScopes");
                }
            }

            if (ReadWindow(raw, row, draft.EffectiveFrom, draft.EffectiveTo) is not { } window) { yield return row; continue; }
            if (window.From != draft.EffectiveFrom) { draft.EffectiveFrom = window.From; changed.Add("EffectiveFrom"); }
            if (window.To != draft.EffectiveTo) { draft.EffectiveTo = window.To; changed.Add("EffectiveTo"); }

            if (raw.Has("ChangeReason") && !raw.IsClear("ChangeReason"))
            {
                draft.ChangeReason = raw.Get("ChangeReason");
            }

            if (changed.Count == 0)
            {
                yield return row.NoChange("The model row matches the stored model; nothing to update.");
                continue;
            }

            // Not a blocker: overlap is enforced at activation, but the operator should know before they get there.
            var overlaps = _context.OtherActiveModels.Any(other =>
                TerritoryImportContext.Is(other.CountryScope, draft.CountryScope)
                && ScopeSet(other.BusinessScopes).SetEquals(ScopeSet(draft.BusinessScopes))
                && TerritoryImportValues.WindowsOverlap(draft.EffectiveFrom, draft.EffectiveTo, other.EffectiveFrom, other.EffectiveTo));

            row.Plan(TerritoryImportRowStatuses.Update,
                $"Model metadata will be updated ({string.Join(", ", changed)}).",
                buffer => buffer.ModelUpdate = draft);

            if (overlaps)
            {
                row.Severity = TerritoryImportSeverities.Warning;
                row.ErrorCode = TerritoryImportErrorCodes.ActiveModelOverlap;
                row.SuggestedFix = "Another ACTIVE model already covers this country + business-unit scope in an overlapping period; activation will be rejected.";
            }

            yield return row;
        }
    }

    // ---- Nodes ---------------------------------------------------------------------------------------------------

    private IEnumerable<TerritoryImportPlanRow> ValidateNodes(IReadOnlyList<TerritoryParsedRow> source)
    {
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in source)
        {
            var row = Start(raw, TerritoryWorkbookSchema.NodesSheet, "TerritoryNode");
            var code = raw.Get("TerritoryCode");
            row.ResolvedKey = code;
            if (IsSkipped(row)) { yield return row; continue; }

            if (string.IsNullOrWhiteSpace(code))
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "TerritoryCode is required.");
                continue;
            }

            if (!seenCodes.Add(code))
            {
                yield return row.Conflict(TerritoryImportErrorCodes.DuplicateRow,
                    "TerritoryCode appears more than once in this sheet.", "Keep one row per node.");
                continue;
            }

            if (!_context.IsModelDraft)
            {
                yield return row.Fail(TerritoryImportErrorCodes.ModelNotEditable,
                    $"Nodes can only be imported while the model is 'draft' (this model is '{_context.Model.Status}').",
                    "Import nodes into a draft model version.");
                continue;
            }

            if (!_context.SetPublished(TerritoryReferenceSets.TerritoryLevel))
            {
                yield return row.Fail(TerritoryImportErrorCodes.ReferenceSetNotPublished,
                    $"Reference set '{TerritoryReferenceSets.TerritoryLevel}' is not published for this tenant.",
                    "Ask the reference-data operator to publish it before importing nodes.");
                continue;
            }

            var existing = ResolveNode(raw, code, out var idMismatch);
            if (idMismatch)
            {
                yield return row.Fail(TerritoryImportErrorCodes.ImmutableField,
                    "NodeId does not belong to this model.", "Leave NodeId empty to add a node, or use a fresh export.");
                continue;
            }

            var isAdd = row.Operation == TerritoryImportOperations.Add;
            if (!isAdd && row.Operation != TerritoryImportOperations.Update)
            {
                yield return row.Fail(TerritoryImportErrorCodes.UnsupportedOperation,
                    "The Nodes sheet supports 'add' and 'update' only.");
                continue;
            }

            if (!isAdd && existing is null)
            {
                yield return row.Fail(TerritoryImportErrorCodes.NotFound,
                    "No node with this code (or id) exists in the model.", "Use Operation = add to create it.");
                continue;
            }

            var target = existing is null ? NewNode(code) : Clone(existing);
            var changed = row.ChangedFields;

            // TerritoryLevel
            var level = raw.Get("TerritoryLevel");
            if (existing is null && string.IsNullOrWhiteSpace(level))
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "TerritoryLevel is required when adding a node.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(level))
            {
                if (!_context.IsPublished(TerritoryReferenceSets.TerritoryLevel, level))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidTerritoryLevel,
                        "TerritoryLevel is not a published territory-level value.", "Pick a value from the ReferenceValues sheet.");
                    continue;
                }
                if (!TerritoryImportContext.Is(target.TerritoryLevel, level)) { target.TerritoryLevel = level!; changed.Add("TerritoryLevel"); }
            }

            if (raw.Has("Name"))
            {
                if (raw.IsClear("Name"))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "Name is required and cannot be cleared.");
                    continue;
                }
                if (!TerritoryImportContext.Is(target.Name, raw.Get("Name"))) { target.Name = raw.Get("Name")!; changed.Add("Name"); }
            }
            else if (existing is null)
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "Name is required when adding a node.");
                continue;
            }

            // Parent (by code; may point at a node created earlier in this same file).
            if (raw.Has("ParentTerritoryCode"))
            {
                if (raw.IsClear("ParentTerritoryCode"))
                {
                    if (target.ParentTerritoryId is not null) { target.ParentTerritoryId = null; changed.Add("ParentTerritoryCode"); }
                }
                else
                {
                    var parentCode = raw.Get("ParentTerritoryCode")!;
                    if (TerritoryImportContext.Is(parentCode, code))
                    {
                        yield return row.Fail(TerritoryImportErrorCodes.HierarchyCycle, "A node cannot be its own parent.");
                        continue;
                    }

                    var parent = _context.NodeByCode(parentCode) ?? _pendingNodes.GetValueOrDefault(parentCode);
                    if (parent is null)
                    {
                        yield return row.Fail(TerritoryImportErrorCodes.InvalidParent,
                            "ParentTerritoryCode does not match any node in this model or earlier in this file.",
                            "Add the parent node on an earlier row, or fix the code.");
                        continue;
                    }

                    if (WouldCycle(target, parent, code))
                    {
                        yield return row.Fail(TerritoryImportErrorCodes.HierarchyCycle,
                            "This parent is the node itself or one of its descendants; the hierarchy would form a cycle.");
                        continue;
                    }

                    if (LevelRank(parent.TerritoryLevel) is { } parentRank
                        && LevelRank(target.TerritoryLevel) is { } ownRank
                        && ownRank <= parentRank)
                    {
                        yield return row.Fail(TerritoryImportErrorCodes.LevelOrderViolation,
                            "A child node must sit on a lower hierarchy level than its parent.",
                            "Change TerritoryLevel or pick a higher-level parent.");
                        continue;
                    }

                    if (target.ParentTerritoryId != parent.Id) { target.ParentTerritoryId = parent.Id; changed.Add("ParentTerritoryCode"); }
                }
            }

            foreach (var (column, setter, getter) in GeoColumns(target))
            {
                if (!raw.Has(column)) continue;
                var value = raw.IsClear(column) ? null : raw.Get(column);
                if (!TerritoryImportContext.Is(getter(), value)) { setter(value); changed.Add(column); }
            }

            if (ReadWindow(raw, row, target.EffectiveFrom, target.EffectiveTo) is not { } window) { yield return row; continue; }
            if (!TerritoryImportValues.Contains(_context.Model.EffectiveFrom, _context.Model.EffectiveTo, window.From, window.To))
            {
                yield return row.Fail(TerritoryImportErrorCodes.WindowContainment,
                    "The node effective window must sit inside the model window "
                    + $"({TerritoryImportValues.Iso(_context.Model.EffectiveFrom)} → {TerritoryImportValues.IsoOrNull(_context.Model.EffectiveTo) ?? "open"}).");
                continue;
            }
            if (window.From != target.EffectiveFrom) { target.EffectiveFrom = window.From; changed.Add("EffectiveFrom"); }
            if (window.To != target.EffectiveTo) { target.EffectiveTo = window.To; changed.Add("EffectiveTo"); }

            if (raw.Has("SortOrder"))
            {
                if (!TerritoryImportValues.TryInt(raw.Get("SortOrder"), out var sortOrder))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidDataType, "SortOrder must be a whole number.");
                    continue;
                }
                if (target.SortOrder != sortOrder) { target.SortOrder = sortOrder; changed.Add("SortOrder"); }
            }

            if (isAdd && existing is not null)
            {
                // Re-running the same file: identical row → idempotent no-change, differing row → controlled conflict.
                if (changed.Count == 0)
                {
                    _pendingNodes[code] = existing;
                    yield return row.NoChange("A node with this code already exists and matches this row.");
                    continue;
                }

                yield return row.Conflict(TerritoryImportErrorCodes.DuplicateNodeCode,
                    "A node with this TerritoryCode already exists with different values.",
                    "Use Operation = update to change it.");
                continue;
            }

            if (existing is not null && changed.Count == 0)
            {
                _pendingNodes[code] = existing;
                yield return row.NoChange("The node row matches the stored node; nothing to update.");
                continue;
            }

            _pendingNodes[code] = target;
            var isInsert = existing is null;
            row.Plan(
                isInsert ? TerritoryImportRowStatuses.Create : TerritoryImportRowStatuses.Update,
                isInsert ? "A new territory node will be created." : $"The node will be updated ({string.Join(", ", changed)}).",
                buffer => (isInsert ? buffer.NodeInserts : buffer.NodeUpdates).Add(target));
            yield return row;
        }
    }

    // ---- Assignment rules ----------------------------------------------------------------------------------------

    private IEnumerable<TerritoryImportPlanRow> ValidateRules(IReadOnlyList<TerritoryParsedRow> source)
    {
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in source)
        {
            var row = Start(raw, TerritoryWorkbookSchema.AssignmentRulesSheet, "TerritoryAssignmentRule");
            var code = raw.Get("RuleCode");
            row.ResolvedKey = code;
            if (IsSkipped(row)) { yield return row; continue; }

            if (string.IsNullOrWhiteSpace(code))
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "RuleCode is required.");
                continue;
            }

            if (!seenCodes.Add(code))
            {
                yield return row.Conflict(TerritoryImportErrorCodes.DuplicateRow, "RuleCode appears more than once in this sheet.");
                continue;
            }

            if (!_context.IsModelDraft)
            {
                yield return row.Fail(TerritoryImportErrorCodes.ModelNotEditable,
                    $"Assignment rules can only be imported while the model is 'draft' (this model is '{_context.Model.Status}').");
                continue;
            }

            var isAdd = row.Operation == TerritoryImportOperations.Add;
            if (!isAdd && row.Operation != TerritoryImportOperations.Update)
            {
                yield return row.Fail(TerritoryImportErrorCodes.UnsupportedOperation,
                    "The AssignmentRules sheet supports 'add' and 'update' only.");
                continue;
            }

            var existing = _context.RuleByCode(code);
            if (!isAdd && existing is null)
            {
                yield return row.Fail(TerritoryImportErrorCodes.NotFound,
                    "No rule with this code exists in the model.", "Use Operation = add to create it.");
                continue;
            }

            var target = existing is null ? NewRule(code) : Clone(existing);
            var changed = row.ChangedFields;

            var ruleType = raw.Get("RuleType");
            if (existing is null && string.IsNullOrWhiteSpace(ruleType))
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "RuleType is required when adding a rule.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(ruleType))
            {
                if (!_context.IsPublished(TerritoryReferenceSets.TerritoryRuleType, ruleType))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidRuleType,
                        "RuleType is not a published territory-rule-type value.");
                    continue;
                }
                if (!TerritoryImportContext.Is(target.RuleType, ruleType)) { target.RuleType = ruleType!; changed.Add("RuleType"); }
            }

            var policy = raw.Get("ConflictPolicy");
            if (existing is null && string.IsNullOrWhiteSpace(policy))
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "ConflictPolicy is required when adding a rule.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(policy))
            {
                if (!_context.IsPublished(TerritoryReferenceSets.TerritoryConflictPolicy, policy))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidConflictPolicy,
                        "ConflictPolicy is not a published territory-conflict-policy value.");
                    continue;
                }
                if (!TerritoryImportContext.Is(target.ConflictPolicy, policy)) { target.ConflictPolicy = policy!; changed.Add("ConflictPolicy"); }
            }

            var targetCode = raw.Get("TargetTerritoryCode");
            if (existing is null && string.IsNullOrWhiteSpace(targetCode))
            {
                yield return row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "TargetTerritoryCode is required when adding a rule.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(targetCode))
            {
                var node = _context.NodeByCode(targetCode) ?? _pendingNodes.GetValueOrDefault(targetCode);
                if (node is null)
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidTargetNode,
                        "TargetTerritoryCode does not match any node in this model or earlier in this file.");
                    continue;
                }
                if (target.TerritoryId != node.Id) { target.TerritoryId = node.Id; changed.Add("TargetTerritoryCode"); }
            }

            if (raw.Has("Name") && !TerritoryImportContext.Is(target.Name, raw.Get("Name")))
            {
                target.Name = raw.IsClear("Name") ? string.Empty : raw.Get("Name")!;
                changed.Add("Name");
            }

            if (raw.Has("Priority"))
            {
                if (!TerritoryImportValues.TryInt(raw.Get("Priority"), out var priority))
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidDataType, "Priority must be a whole number.");
                    continue;
                }
                if (target.Priority != priority) { target.Priority = priority; changed.Add("Priority"); }
            }

            if (raw.Has("IsEnabled"))
            {
                if (TerritoryImportValues.TryBool(raw.Get("IsEnabled")) is not { } enabled)
                {
                    yield return row.Fail(TerritoryImportErrorCodes.InvalidDataType, "IsEnabled must be TRUE or FALSE.");
                    continue;
                }
                if (target.IsEnabled != enabled) { target.IsEnabled = enabled; changed.Add("IsEnabled"); }
            }

            if (ReadWindow(raw, row, target.EffectiveFrom, target.EffectiveTo) is not { } window) { yield return row; continue; }
            if (window.From != target.EffectiveFrom) { target.EffectiveFrom = window.From; changed.Add("EffectiveFrom"); }
            if (window.To != target.EffectiveTo) { target.EffectiveTo = window.To; changed.Add("EffectiveTo"); }

            foreach (var (column, list) in CriteriaColumns(target.Criteria))
            {
                if (!raw.Has(column)) continue;
                var values = raw.IsClear(column) ? [] : TerritoryImportValues.SplitList(raw.Get(column));
                if (!list.SequenceEqual(values, StringComparer.OrdinalIgnoreCase))
                {
                    list.Clear();
                    list.AddRange(values);
                    changed.Add(column);
                }
            }

            if (isAdd && existing is not null)
            {
                if (changed.Count == 0)
                {
                    yield return row.NoChange("A rule with this code already exists and matches this row.");
                    continue;
                }

                yield return row.Conflict(TerritoryImportErrorCodes.DuplicateRow,
                    "A rule with this RuleCode already exists with different values.", "Use Operation = update to change it.");
                continue;
            }

            if (existing is not null && changed.Count == 0)
            {
                yield return row.NoChange("The rule row matches the stored rule; nothing to update.");
                continue;
            }

            var isInsert = existing is null;
            row.Plan(
                isInsert ? TerritoryImportRowStatuses.Create : TerritoryImportRowStatuses.Update,
                isInsert ? "A new assignment rule will be created." : $"The rule will be updated ({string.Join(", ", changed)}).",
                buffer => (isInsert ? buffer.RuleInserts : buffer.RuleUpdates).Add(target));
            yield return row;
        }
    }

    // ---- Account assignments -------------------------------------------------------------------------------------

    private IEnumerable<TerritoryImportPlanRow> ValidateAccountAssignments(IReadOnlyList<TerritoryParsedRow> source)
    {
        var seenAccounts = new HashSet<Guid>();
        // Assignments planned by THIS file, so two rows for the same account/scope collide here too.
        var planned = new List<AccountTerritoryAssignment>();

        foreach (var raw in source)
        {
            var row = Start(raw, TerritoryWorkbookSchema.AccountAssignmentsSheet, "AccountTerritoryAssignment");
            if (IsSkipped(row)) { yield return row; continue; }

            if (!_context.IsModelActive)
            {
                yield return row.Fail(TerritoryImportErrorCodes.ModelNotActive,
                    $"Account assignments can only be applied to an ACTIVE model (this model is '{_context.Model.Status}').",
                    "Activate the model first — the import uses exactly the same rule as the on-screen apply.");
                continue;
            }

            foreach (var set in new[] { TerritoryReferenceSets.TerritoryAssignmentStatus, TerritoryReferenceSets.TerritoryAssignmentSource })
            {
                if (!_context.SetPublished(set))
                {
                    row.Fail(TerritoryImportErrorCodes.ReferenceSetNotPublished,
                        $"Reference set '{set}' is not published for this tenant.");
                    break;
                }
            }
            if (row.Blocking) { yield return row; continue; }

            var account = ResolveAccount(raw, out var accountIssue);
            if (account is null)
            {
                yield return row.Fail(accountIssue!,
                    accountIssue == TerritoryImportErrorCodes.CrossTenantAccount
                        ? "AccountId does not belong to this tenant."
                        : "The account could not be resolved from AccountId or AccountCode.",
                    "Use an AccountCode from the export, or leave AccountId empty and fill AccountCode.");
                continue;
            }

            row.ResolvedKey = account.AccountCode;

            if (row.Operation == TerritoryImportOperations.End)
            {
                yield return PlanEnd(raw, row, account);
                continue;
            }

            if (row.Operation != TerritoryImportOperations.Add)
            {
                yield return row.Fail(TerritoryImportErrorCodes.UnsupportedOperation,
                    "The AccountAssignments sheet supports 'add' and 'end' only. Move an account by ending the old row and adding a new one.");
                continue;
            }

            if (!seenAccounts.Add(account.AccountId))
            {
                yield return row.Conflict(TerritoryImportErrorCodes.DuplicateRow,
                    "This account appears more than once as an 'add' in this sheet.",
                    "An account may be added only once per import batch — the same rule the on-screen apply uses.");
                continue;
            }

            var node = _context.NodeByCode(raw.Get("TerritoryCode"));
            if (node is null)
            {
                yield return row.Fail(TerritoryImportErrorCodes.InvalidTargetNode,
                    "TerritoryCode does not match an existing node in this model.");
                continue;
            }

            if (!TerritoryImportContext.Is(node.Status, "active"))
            {
                yield return row.Fail(TerritoryImportErrorCodes.InvalidTargetNode,
                    "The target territory node is not active.", "Only active nodes can receive account assignments.");
                continue;
            }

            var scopes = TerritoryImportValues.SplitList(raw.Get("BusinessUnitScopes"));
            var modelScopes = ScopeSet(_context.Model.BusinessScopes);
            var outside = scopes.Where(s => !modelScopes.Contains(s.ToUpperInvariant())).ToList();
            if (outside.Count > 0)
            {
                yield return row.Fail(TerritoryImportErrorCodes.ModelScopeOverflow,
                    "BusinessUnitScopes cannot exceed the territory model scope.",
                    $"The model covers: {string.Join(", ", modelScopes.DefaultIfEmpty("(none)"))}.");
                continue;
            }

            var policy = raw.Get("ConflictPolicy") ?? "block";
            if (!_context.IsPublished(TerritoryReferenceSets.TerritoryConflictPolicy, policy))
            {
                yield return row.Fail(TerritoryImportErrorCodes.InvalidConflictPolicy,
                    "ConflictPolicy is not a published territory-conflict-policy value.");
                continue;
            }

            if (ReadWindow(raw, row, _context.Model.EffectiveFrom, _context.Model.EffectiveTo) is not { } window)
            {
                yield return row; continue;
            }

            if (!TerritoryImportValues.Contains(_context.Model.EffectiveFrom, _context.Model.EffectiveTo, window.From, window.To)
                || !TerritoryImportValues.Contains(node.EffectiveFrom, node.EffectiveTo, window.From, window.To))
            {
                yield return row.Fail(TerritoryImportErrorCodes.WindowContainment,
                    "The assignment window must sit inside both the node window and the model window.");
                continue;
            }

            var isOverride = TerritoryImportValues.TryBool(raw.Get("Override")) ?? false;
            var overrideReason = raw.Get("OverrideReason");
            if (isOverride && string.IsNullOrWhiteSpace(overrideReason))
            {
                yield return row.Fail(TerritoryImportErrorCodes.OverrideReasonRequired,
                    "OverrideReason is required when Override is TRUE.",
                    "Explain why the existing assignment is being replaced.");
                continue;
            }

            var conflicts = _context.Assignments
                .Where(a => a.AccountId == account.AccountId
                            && TerritoryImportContext.Is(a.AssignmentStatus, "active")
                            && ScopesOverlap(a.BusinessScopes, scopes)
                            && TerritoryImportValues.WindowsOverlap(a.EffectiveFrom, a.EffectiveTo, window.From, window.To))
                .ToList();

            var identical = conflicts.FirstOrDefault(a =>
                a.TerritoryNodeId == node.Id
                && a.EffectiveFrom.Date == window.From.Date
                && (a.EffectiveTo?.Date) == (window.To?.Date)
                && ScopeSet(a.BusinessScopes).SetEquals(scopes.Select(s => s.ToUpperInvariant())));

            if (identical is not null && !isOverride)
            {
                // Re-running the same file must not duplicate coverage.
                yield return row.NoChange("An identical active assignment already exists for this account.");
                continue;
            }

            var plannedConflict = planned.Any(a =>
                a.AccountId == account.AccountId
                && ScopesOverlap(a.BusinessScopes, scopes)
                && TerritoryImportValues.WindowsOverlap(a.EffectiveFrom, a.EffectiveTo, window.From, window.To));

            if ((conflicts.Count > 0 || plannedConflict) && !isOverride)
            {
                yield return row.Conflict(TerritoryImportErrorCodes.DuplicateAssignment,
                    "This account already has an overlapping active assignment in the same business scope.",
                    "Set Override = TRUE with an OverrideReason to replace it — the old record is closed, never deleted.");
                continue;
            }

            var created = new AccountTerritoryAssignment
            {
                TenantId = _tenantId,
                AccountId = account.AccountId,
                AccountCode = account.AccountCode,
                AccountDisplayName = account.AccountName,
                TerritoryModelId = _context.Model.Id,
                TerritoryNodeId = node.Id,
                TerritoryNodeCode = node.TerritoryCode,
                TerritoryNodeName = node.Name,
                BusinessScopes = scopes
                    .Select(s => new TerritoryBusinessScope { ScopeType = TerritoryReferenceSets.BusinessUnitScopeType, ScopeCode = s })
                    .ToList(),
                // Provenance: a hand-written import row is 'import'; an override keeps the FU05 'override' source so
                // the two write paths stay indistinguishable downstream.
                AssignmentSource = isOverride ? "override" : "import",
                AssignmentStatus = "active",
                EffectiveFrom = window.From,
                EffectiveTo = window.To,
                ConflictPolicy = policy.Trim(),
                OverrideReason = isOverride ? overrideReason!.Trim() : null,
                AppliedRuleCode = raw.Get("AppliedRuleCode")
            };

            if (created.AppliedRuleCode is { } ruleCode && _context.RuleByCode(ruleCode) is { } rule)
            {
                created.AppliedRuleId = rule.Id;
            }
            else
            {
                created.AppliedRuleCode = null;
            }

            if (!_context.IsPublished(TerritoryReferenceSets.TerritoryAssignmentSource, created.AssignmentSource))
            {
                yield return row.Fail(TerritoryImportErrorCodes.ReferenceSetNotPublished,
                    $"'{created.AssignmentSource}' is not a published territory-assignment-source value.",
                    "Ask the reference-data operator to publish it.");
                continue;
            }

            planned.Add(created);
            var toEnd = isOverride ? conflicts : [];

            row.Plan(TerritoryImportRowStatuses.Create,
                isOverride && toEnd.Count > 0
                    ? $"A new assignment will be created and {toEnd.Count} overlapping assignment(s) will be closed (not deleted)."
                    : "A new account territory assignment will be created.",
                buffer =>
                {
                    var now = DateTimeOffset.UtcNow;
                    foreach (var conflict in toEnd)
                    {
                        var closed = Clone(conflict);
                        closed.AssignmentStatus = "ended";
                        closed.EffectiveTo = created.EffectiveFrom;
                        closed.EndedAt = now;
                        closed.UpdatedAt = now;
                        closed.OverrideReason = overrideReason!.Trim();
                        buffer.AssignmentEnds.Add(closed);
                    }

                    buffer.AssignmentInserts.Add(created);
                });
            yield return row;
        }
    }

    private TerritoryImportPlanRow PlanEnd(TerritoryParsedRow raw, TerritoryImportPlanRow row, TerritoryAccountSnapshot account)
    {
        var candidates = _context.Assignments
            .Where(a => a.AccountId == account.AccountId && a.TerritoryModelId == _context.Model.Id)
            .ToList();

        AccountTerritoryAssignment? target = null;
        if (TerritoryImportValues.TryGuid(raw.Get("AssignmentId"), out var assignmentId))
        {
            target = candidates.FirstOrDefault(a => a.Id == assignmentId);
            if (target is null)
            {
                return row.Fail(TerritoryImportErrorCodes.NotFound, "No assignment with this AssignmentId exists for this account.");
            }
        }
        else
        {
            var byNode = candidates.Where(a => TerritoryImportContext.Is(a.AssignmentStatus, "active")).ToList();
            if (raw.Has("TerritoryCode"))
            {
                byNode = byNode.Where(a => TerritoryImportContext.Is(a.TerritoryNodeCode, raw.Get("TerritoryCode"))).ToList();
            }

            if (byNode.Count == 0)
            {
                return row.Skip(TerritoryImportErrorCodes.AlreadyEnded, "This account has no active assignment to end in this model.");
            }

            if (byNode.Count > 1)
            {
                return row.Conflict(TerritoryImportErrorCodes.DuplicateRow,
                    "More than one active assignment matches this row.", "Fill AssignmentId to say which one to end.");
            }

            target = byNode[0];
        }

        if (TerritoryImportContext.Is(target.AssignmentStatus, "ended"))
        {
            return row.Skip(TerritoryImportErrorCodes.AlreadyEnded, "This assignment is already ended.");
        }

        var endDate = TerritoryImportValues.TryDate(raw.Get("EffectiveTo"), out var parsed) ? parsed : DateTimeOffset.UtcNow;
        if (endDate.Date < target.EffectiveFrom.Date)
        {
            return row.Fail(TerritoryImportErrorCodes.InvalidDateWindow, "The end date cannot be before the assignment start date.");
        }

        var closed = Clone(target);
        closed.AssignmentStatus = "ended";
        closed.EffectiveTo = endDate;
        closed.EndedAt = DateTimeOffset.UtcNow;
        closed.UpdatedAt = closed.EndedAt;
        closed.OverrideReason = raw.Get("OverrideReason") ?? closed.OverrideReason;

        row.ResolvedKey = $"{account.AccountCode} → {target.TerritoryNodeCode}";
        return row.Plan(TerritoryImportRowStatuses.End,
            "The assignment will be closed with an end date; the record itself is kept in history.",
            buffer => buffer.AssignmentEnds.Add(closed));
    }

    // ---- Resource assignments (dry-run only) ---------------------------------------------------------------------

    private IEnumerable<TerritoryImportPlanRow> ValidateResourceAssignments(IReadOnlyList<TerritoryParsedRow> source)
    {
        foreach (var raw in source)
        {
            var row = Start(raw, TerritoryWorkbookSchema.ResourceAssignmentsSheet, "TerritoryResourceAssignment");
            row.ResolvedKey = raw.Get("TerritoryCode");
            if (IsSkipped(row)) { yield return row; continue; }

            // Structural boundary: there is no apply path for this sheet at all (pack §22.5 → FU08A). The row is still
            // validated so the operator can see whether the data WOULD be acceptable once FU08A opens.
            var problems = new List<string>();
            if (!raw.Has("PositionCode")) problems.Add("PositionCode is required");
            if (!raw.Has("ResourceId")) problems.Add("ResourceId is required");
            if (raw.Has("TerritoryCode") && _context.NodeByCode(raw.Get("TerritoryCode")) is null)
                problems.Add("TerritoryCode does not match a node in this model");
            if (raw.Has("CoverageScope") && !_context.IsPublished(TerritoryReferenceSets.TerritoryCoverageScope, raw.Get("CoverageScope")))
                problems.Add("CoverageScope is not a published value");

            row.Fail(TerritoryImportErrorCodes.ResourceApplyNotSupported,
                "Resource assignments cannot be applied from a file in this version"
                + (problems.Count > 0 ? $" (this row would also fail: {string.Join("; ", problems)})." : "."),
                "Use the Resource Assignments screen — create/end/replace/transfer carry rules an import row cannot express.");
            row.Status = TerritoryImportRowStatuses.NotApplied;
            yield return row;
        }
    }

    // ---- shared --------------------------------------------------------------------------------------------------

    private TerritoryImportPlanRow Start(TerritoryParsedRow raw, string sheet, string entityType)
    {
        var row = new TerritoryImportPlanRow { Source = raw, Sheet = sheet, EntityType = entityType };
        row.Operation = TerritoryImportOperations.Normalize(raw.Get(TerritoryWorkbookSchema.OperationColumn));
        return row;
    }

    /// <summary>Empty Operation = skip (an export lands with empty cells; treating them as add/update would either
    /// duplicate or silently mass-update the whole plan). <c>delete</c> is refused outright.</summary>
    private static bool IsSkipped(TerritoryImportPlanRow row)
    {
        if (row.Operation is null)
        {
            row.Skip(TerritoryImportErrorCodes.OperationMissing, "No Operation was set for this row, so it was skipped.");
            return true;
        }

        if (row.Operation == TerritoryImportOperations.Skip)
        {
            row.Skip(TerritoryImportErrorCodes.OperationMissing, "Row skipped on request.");
            return true;
        }

        if (row.Operation == TerritoryImportOperations.Delete)
        {
            row.Fail(TerritoryImportErrorCodes.UnsupportedOperation,
                "'delete' is not supported — MOD-0151 records are ended or archived, never destroyed.",
                "Use 'end' on an account assignment, or archive the model on screen.");
            return true;
        }

        return false;
    }

    private (DateTimeOffset From, DateTimeOffset? To)? ReadWindow(
        TerritoryParsedRow raw, TerritoryImportPlanRow row, DateTimeOffset currentFrom, DateTimeOffset? currentTo)
    {
        var from = currentFrom;
        var to = currentTo;

        if (raw.Has("EffectiveFrom"))
        {
            if (raw.IsClear("EffectiveFrom"))
            {
                row.Fail(TerritoryImportErrorCodes.RequiredFieldMissing, "EffectiveFrom is required and cannot be cleared.");
                return null;
            }
            if (!TerritoryImportValues.TryDate(raw.Get("EffectiveFrom"), out from))
            {
                row.Fail(TerritoryImportErrorCodes.InvalidDataType, "EffectiveFrom is not a valid date (use yyyy-MM-dd).");
                return null;
            }
        }

        if (raw.Has("EffectiveTo"))
        {
            if (raw.IsClear("EffectiveTo"))
            {
                to = null;
            }
            else if (!TerritoryImportValues.TryDate(raw.Get("EffectiveTo"), out var parsed))
            {
                row.Fail(TerritoryImportErrorCodes.InvalidDataType, "EffectiveTo is not a valid date (use yyyy-MM-dd).");
                return null;
            }
            else
            {
                to = parsed;
            }
        }

        if (to is { } end && end.Date < from.Date)
        {
            row.Fail(TerritoryImportErrorCodes.InvalidDateWindow, "EffectiveTo cannot be earlier than EffectiveFrom.");
            return null;
        }

        return (from, to);
    }

    private TerritoryNode? ResolveNode(TerritoryParsedRow raw, string code, out bool idMismatch)
    {
        idMismatch = false;
        if (TerritoryImportValues.TryGuid(raw.Get("NodeId"), out var id))
        {
            var byId = _context.NodeById(id);
            if (byId is null) { idMismatch = true; return null; }
            return byId;
        }

        return _context.NodeByCode(code);
    }

    private TerritoryAccountSnapshot? ResolveAccount(TerritoryParsedRow raw, out string? issue)
    {
        issue = null;
        if (TerritoryImportValues.TryGuid(raw.Get("AccountId"), out var id))
        {
            var byId = _context.AccountById(id);
            if (byId is null) { issue = TerritoryImportErrorCodes.CrossTenantAccount; return null; }
            return byId;
        }

        if (raw.Has("AccountId"))
        {
            issue = TerritoryImportErrorCodes.InvalidAccount;
            return null;
        }

        if (!raw.Has("AccountCode"))
        {
            issue = TerritoryImportErrorCodes.RequiredFieldMissing;
            return null;
        }

        var byCode = _context.AccountByCode(raw.Get("AccountCode"));
        if (byCode is null) { issue = TerritoryImportErrorCodes.UnresolvedAccountReference; return null; }
        return byCode;
    }

    private bool WouldCycle(TerritoryNode node, TerritoryNode candidateParent, string nodeCode)
    {
        if (node.Id != Guid.Empty && candidateParent.Id == node.Id) return true;

        var seen = new HashSet<Guid>();
        var current = candidateParent;
        while (current is not null && seen.Add(current.Id))
        {
            if (node.Id != Guid.Empty && current.ParentTerritoryId == node.Id) return true;
            if (current.ParentTerritoryId is not { } parentId) break;
            current = _context.NodeById(parentId) ?? _pendingNodes.Values.FirstOrDefault(n => n.Id == parentId);
        }

        return false;
    }

    private int? LevelRank(string? level)
        => level is not null && _context.LevelRanks.TryGetValue(level, out var rank) ? rank : null;

    private TerritoryNode NewNode(string code) => new()
    {
        TenantId = _tenantId,
        ModelId = _context.Model.Id,
        TerritoryCode = code,
        Status = TerritoryReferenceSets.DraftStatus,
        EffectiveFrom = _context.Model.EffectiveFrom,
        EffectiveTo = _context.Model.EffectiveTo
    };

    private TerritoryAssignmentRule NewRule(string code) => new()
    {
        TenantId = _tenantId,
        ModelId = _context.Model.Id,
        RuleCode = code,
        Name = code,
        IsEnabled = true,
        EffectiveFrom = _context.Model.EffectiveFrom,
        EffectiveTo = _context.Model.EffectiveTo
    };

    private static IEnumerable<(string Column, Action<string?> Set, Func<string?> Get)> GeoColumns(TerritoryNode node)
    {
        yield return ("CountryCode", v => node.CountryCode = v, () => node.CountryCode);
        yield return ("DivisionCode", v => node.DivisionCode = v, () => node.DivisionCode);
        yield return ("RegionCode", v => node.RegionCode = v, () => node.RegionCode);
        yield return ("AreaCode", v => node.AreaCode = v, () => node.AreaCode);
        yield return ("ZoneCode", v => node.ZoneCode = v, () => node.ZoneCode);
        yield return ("MicroZoneCode", v => node.MicroZoneCode = v, () => node.MicroZoneCode);
    }

    private static IEnumerable<(string Column, List<string> Values)> CriteriaColumns(TerritoryRuleCriteria criteria)
    {
        yield return ("CountryRefs", criteria.CountryRefs);
        yield return ("CityRefs", criteria.CityRefs);
        yield return ("DistrictRefs", criteria.DistrictRefs);
        yield return ("AccountTypes", criteria.AccountTypes);
        yield return ("AccountCategories", criteria.AccountCategories);
        yield return ("AccountStatuses", criteria.AccountStatuses);
    }

    private static HashSet<string> ScopeSet(IEnumerable<TerritoryBusinessScope>? scopes)
        => (scopes ?? [])
            .Where(s => TerritoryImportContext.Is(s.ScopeType, TerritoryReferenceSets.BusinessUnitScopeType))
            .Select(s => (s.ScopeCode ?? string.Empty).Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    private static bool ScopesOverlap(IReadOnlyList<TerritoryBusinessScope> stored, IReadOnlyList<string> incoming)
    {
        if (stored.Count == 0 || incoming.Count == 0) return true;
        var keys = ScopeSet(stored);
        return incoming.Any(s => keys.Contains(s.Trim().ToUpperInvariant()));
    }

    private static TerritoryModel Clone(TerritoryModel source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, IsDeleted = source.IsDeleted, DeletedAt = source.DeletedAt,
        CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt, Version = source.Version,
        ModelCode = source.ModelCode, Name = source.Name, CountryScope = source.CountryScope,
        DivisionScope = source.DivisionScope, BusinessScopes = [.. source.BusinessScopes],
        EffectiveFrom = source.EffectiveFrom, EffectiveTo = source.EffectiveTo, Status = source.Status,
        VersionNumber = source.VersionNumber, BasedOnModelId = source.BasedOnModelId,
        ChangeReason = source.ChangeReason, CorrelationId = source.CorrelationId
    };

    private static TerritoryNode Clone(TerritoryNode source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, IsDeleted = source.IsDeleted, DeletedAt = source.DeletedAt,
        CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt, Version = source.Version,
        ModelId = source.ModelId, ParentTerritoryId = source.ParentTerritoryId, TerritoryCode = source.TerritoryCode,
        Name = source.Name, TerritoryLevel = source.TerritoryLevel, CountryCode = source.CountryCode,
        DivisionCode = source.DivisionCode, RegionCode = source.RegionCode, AreaCode = source.AreaCode,
        ZoneCode = source.ZoneCode, MicroZoneCode = source.MicroZoneCode, Status = source.Status,
        EffectiveFrom = source.EffectiveFrom, EffectiveTo = source.EffectiveTo, SortOrder = source.SortOrder,
        MicroZoneProfile = source.MicroZoneProfile, CorrelationId = source.CorrelationId
    };

    private static TerritoryAssignmentRule Clone(TerritoryAssignmentRule source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, IsDeleted = source.IsDeleted, DeletedAt = source.DeletedAt,
        CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt, Version = source.Version,
        ModelId = source.ModelId, TerritoryId = source.TerritoryId, RuleCode = source.RuleCode, Name = source.Name,
        RuleType = source.RuleType, ConflictPolicy = source.ConflictPolicy, Priority = source.Priority,
        IsEnabled = source.IsEnabled, EffectiveFrom = source.EffectiveFrom, EffectiveTo = source.EffectiveTo,
        CorrelationId = source.CorrelationId,
        Criteria = new TerritoryRuleCriteria
        {
            CountryRefs = [.. source.Criteria.CountryRefs], CityRefs = [.. source.Criteria.CityRefs],
            DistrictRefs = [.. source.Criteria.DistrictRefs], AccountTypes = [.. source.Criteria.AccountTypes],
            AccountCategories = [.. source.Criteria.AccountCategories], AccountStatuses = [.. source.Criteria.AccountStatuses],
            IncludeAccountIds = [.. source.Criteria.IncludeAccountIds], ExcludeAccountIds = [.. source.Criteria.ExcludeAccountIds]
        }
    };

    private static AccountTerritoryAssignment Clone(AccountTerritoryAssignment source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, IsDeleted = source.IsDeleted, DeletedAt = source.DeletedAt,
        CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt, Version = source.Version,
        AccountId = source.AccountId, AccountCode = source.AccountCode, AccountDisplayName = source.AccountDisplayName,
        TerritoryModelId = source.TerritoryModelId, TerritoryNodeId = source.TerritoryNodeId,
        TerritoryNodeCode = source.TerritoryNodeCode, TerritoryNodeName = source.TerritoryNodeName,
        BusinessScopes = [.. source.BusinessScopes], AssignmentSource = source.AssignmentSource,
        AssignmentStatus = source.AssignmentStatus, EffectiveFrom = source.EffectiveFrom, EffectiveTo = source.EffectiveTo,
        AppliedFromPreviewRunId = source.AppliedFromPreviewRunId, AppliedRuleId = source.AppliedRuleId,
        AppliedRuleCode = source.AppliedRuleCode, ConflictPolicy = source.ConflictPolicy,
        OverrideReason = source.OverrideReason, CreatedBy = source.CreatedBy, UpdatedBy = source.UpdatedBy,
        EndedAt = source.EndedAt, EndedBy = source.EndedBy, CorrelationId = source.CorrelationId
    };
}
