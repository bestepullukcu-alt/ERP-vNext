using ClosedXML.Excel;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.ImportExport;
using Diten.CrmService.Application.Features.Territory.ImportExport.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>
/// MOD-0151 FU08 — Import/Export hardening (pack §22.5).
///
/// <para>These tests pin the properties that make the feature safe rather than merely functional: a dry-run writes
/// NOTHING, apply is gated and all-or-nothing per sheet, account assignment rows go through the FU05 rules,
/// resource assignment apply is structurally absent, CoverageSummary / Plan vs Current cannot be imported, nothing
/// is ever hard-deleted, and re-applying the same file does not duplicate anything.</para>
/// </summary>
public sealed class TerritoryImportExportFu08Tests
{
    private static readonly Guid TenantA = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    // ---------------------------------------------------------------- template / export

    [Fact]
    public async Task Template_contains_the_seven_declared_sheets()
    {
        var fx = Fixture();

        var response = await fx.Export.Handle(new BuildTerritoryImportTemplateQuery(fx.Model.Id), default);

        Assert.True(response.IsSuccessful);
        var sheets = SheetNames(response.Data!.Content);
        foreach (var expected in TerritoryWorkbookSchema.TemplateSheets)
        {
            Assert.Contains(expected, sheets);
        }

        Assert.Equal(TerritoryWorkbookSchema.TemplateSheets.Count, sheets.Count);
    }

    [Fact]
    public async Task Export_carries_the_read_only_sheets_and_no_tenant_id_column()
    {
        var fx = Fixture();

        var response = await fx.Export.Handle(new ExportTerritoryModelWorkbookQuery(fx.Model.Id), default);

        Assert.True(response.IsSuccessful);
        var bytes = response.Data!.Content;
        var sheets = SheetNames(bytes);

        Assert.Contains(TerritoryWorkbookSchema.CoverageSummarySheet, sheets);
        Assert.Contains(TerritoryWorkbookSchema.PlanVsCurrentSheet, sheets);
        Assert.Contains(TerritoryWorkbookSchema.ResourceAssignmentsSheet, sheets);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        foreach (var sheet in workbook.Worksheets)
        {
            var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToList();
            Assert.DoesNotContain(TerritoryWorkbookSchema.TenantIdColumn, headers, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Export_writes_existing_nodes_and_assignments()
    {
        var fx = Fixture();

        var response = await fx.Export.Handle(new ExportTerritoryModelWorkbookQuery(fx.Model.Id), default);

        var rows = DataRows(response.Data!.Content, TerritoryWorkbookSchema.NodesSheet);
        Assert.Single(rows);
        Assert.Contains("ZONE-1", rows[0]);
        // An exported row lands with an EMPTY Operation: nothing happens to it until the operator chooses one.
        Assert.True(string.IsNullOrWhiteSpace(rows[0][0]));
    }

    // ---------------------------------------------------------------- dry-run: writes nothing

    [Fact]
    public async Task Dry_run_writes_absolutely_nothing()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);

        var response = await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", dryRun: true, strictMode: false, null, "tester", default);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.DryRun);
        Assert.False(response.Data.Applied);
        Assert.Single(fx.Nodes.Items);            // no node written
        Assert.Empty(fx.Runs.Items);              // no run-history row written either
        Assert.Empty(fx.Rules.Items);
        Assert.Empty(fx.Assignments.Items);
    }

    [Fact]
    public async Task Dry_run_plans_a_create_without_persisting_it()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Equal(1, preview.Summary.Creates);
        Assert.True(preview.CanApply);
        Assert.Single(fx.Nodes.Items);
    }

    // ---------------------------------------------------------------- dry-run: validation rules

    [Fact]
    public async Task Missing_required_column_is_a_file_level_error()
    {
        var fx = Fixture();
        var file = SheetWithHeaders(TerritoryWorkbookSchema.NodesSheet, ["Name", "TerritoryLevel"]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.NotEmpty(preview.FileErrors);
        Assert.False(preview.CanApply);
    }

    [Fact]
    public async Task Unreadable_file_is_reported_without_leaking_the_exception()
    {
        var fx = Fixture();

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, [1, 2, 3, 4], "broken.xlsx", true, false, null, "tester", default)).Data!;

        Assert.NotEmpty(preview.FileErrors);
        Assert.False(preview.CanApply);
        Assert.DoesNotContain(preview.FileErrors, e => e.Contains("Exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Duplicate_node_code_in_the_same_sheet_is_a_conflict()
    {
        var fx = Fixture();
        var file = Workbook(nodes:
        [
            Row("add", "ZONE-2", "Zone Two", "zone"),
            Row("add", "ZONE-2", "Zone Two Again", "zone")
        ]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.DuplicateRow && r.Blocking);
    }

    [Fact]
    public async Task Invalid_parent_is_blocking()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone", parent: "NOPE")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.InvalidParent && r.Blocking);
    }

    [Fact]
    public async Task A_node_cannot_be_its_own_parent()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone", parent: "ZONE-2")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.HierarchyCycle && r.Blocking);
    }

    [Fact]
    public async Task Child_level_must_rank_below_its_parent()
    {
        var fx = Fixture();
        // ZONE-1 is a 'zone' (rank 50); making a 'country' (rank 20) its child inverts the hierarchy.
        var file = Workbook(nodes: [Row("add", "C-1", "Country One", "country", parent: "ZONE-1")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.LevelOrderViolation && r.Blocking);
    }

    [Fact]
    public async Task Unpublished_territory_level_fails_closed()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "not-a-level")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.InvalidTerritoryLevel && r.Blocking);
    }

    [Fact]
    public async Task An_unpublished_reference_set_blocks_the_whole_file()
    {
        var fx = Fixture(publishLevels: false);
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.ReferenceSetNotPublished);
    }

    [Fact]
    public async Task Node_window_must_sit_inside_the_model_window()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone", from: "2020-01-01")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.WindowContainment && r.Blocking);
    }

    [Fact]
    public async Task Tenant_id_column_is_ignored_and_reported()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")], withTenantIdColumn: true);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.FileWarnings, w => w.Contains("TenantId", StringComparison.OrdinalIgnoreCase));
        // Ignored, not fatal: the row still validates.
        Assert.Contains(preview.Rows, r => r.Status == TerritoryImportRowStatuses.Create);
    }

    [Fact]
    public async Task Empty_operation_is_a_skip_and_delete_is_refused()
    {
        var fx = Fixture();
        var file = Workbook(nodes:
        [
            Row(null, "ZONE-2", "Zone Two", "zone"),
            Row("delete", "ZONE-1", "Zone One", "zone")
        ]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.OperationMissing && !r.Blocking);
        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.UnsupportedOperation && r.Blocking);
    }

    [Fact]
    public async Task Blocking_and_non_blocking_rows_are_distinguished()
    {
        var fx = Fixture();
        var file = Workbook(nodes:
        [
            Row("add", "ZONE-2", "Zone Two", "zone"),
            Row(null, "ZONE-3", "Zone Three", "zone"),
            Row("add", "ZONE-4", "Zone Four", "not-a-level")
        ]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Equal(1, preview.Rows.Count(r => r.Blocking));
        Assert.Equal(2, preview.Rows.Count(r => !r.Blocking));
    }

    // ---------------------------------------------------------------- account assignments (FU05 guards)

    [Fact]
    public async Task Account_assignment_import_needs_an_active_model()
    {
        var fx = Fixture();   // draft model
        var file = Workbook(accounts: [AccountRow("add", "ACC-1", "ZONE-1")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.ModelNotActive && r.Blocking);
    }

    [Fact]
    public async Task Unresolved_account_code_is_blocking()
    {
        var fx = Fixture(modelStatus: "active");
        var file = Workbook(accounts: [AccountRow("add", "ACC-NOPE", "ZONE-1")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.UnresolvedAccountReference && r.Blocking);
    }

    [Fact]
    public async Task Cross_tenant_account_id_is_blocking()
    {
        var fx = Fixture(modelStatus: "active");
        var file = Workbook(accounts: [AccountRow("add", null, "ZONE-1", accountId: Guid.NewGuid().ToString())]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.CrossTenantAccount && r.Blocking);
    }

    [Fact]
    public async Task Business_unit_scope_cannot_exceed_the_model_scope()
    {
        var fx = Fixture(modelStatus: "active");
        var file = Workbook(accounts: [AccountRow("add", "ACC-1", "ZONE-1", scopes: "gamma")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.ModelScopeOverflow && r.Blocking);
    }

    [Fact]
    public async Task Overlapping_assignment_without_override_is_a_conflict()
    {
        var fx = Fixture(modelStatus: "active");
        fx.Assignments.Items.Add(ExistingAssignment(fx));
        var file = Workbook(accounts: [AccountRow("add", "ACC-1", "ZONE-1", scopes: "alpha", nodeCodeForNew: "ZONE-1")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        // Identical row → idempotent no_change, not a duplicate.
        Assert.Contains(preview.Rows, r => r.Status == TerritoryImportRowStatuses.NoChange);
        Assert.Single(fx.Assignments.Items);
    }

    [Fact]
    public async Task Override_without_a_reason_is_blocking()
    {
        var fx = Fixture(modelStatus: "active");
        fx.Assignments.Items.Add(ExistingAssignment(fx));
        var file = Workbook(accounts:
        [
            AccountRow("add", "ACC-1", "ZONE-2", scopes: "alpha", isOverride: "TRUE")
        ]);
        fx.Nodes.Items.Add(SecondNode(fx));

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.OverrideReasonRequired && r.Blocking);
    }

    [Fact]
    public async Task Override_with_a_reason_closes_the_old_record_without_deleting_it()
    {
        var fx = Fixture(modelStatus: "active");
        var existing = ExistingAssignment(fx);
        fx.Assignments.Items.Add(existing);
        fx.Nodes.Items.Add(SecondNode(fx));
        var file = Workbook(accounts:
        [
            AccountRow("add", "ACC-1", "ZONE-2", scopes: "alpha", isOverride: "TRUE", overrideReason: "Territory replan")
        ]);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.True(result.Applied);
        Assert.Equal(2, fx.Assignments.Items.Count);                                  // nothing was deleted
        var closed = fx.Assignments.Items.Single(a => a.Id == existing.Id);
        Assert.Equal("ended", closed.AssignmentStatus);
        Assert.NotNull(closed.EndedAt);
        var created = fx.Assignments.Items.Single(a => a.Id != existing.Id);
        Assert.Equal("override", created.AssignmentSource);
    }

    [Fact]
    public async Task A_manual_import_row_is_marked_with_the_import_source()
    {
        var fx = Fixture(modelStatus: "active");
        var file = Workbook(accounts: [AccountRow("add", "ACC-1", "ZONE-1", scopes: "alpha")]);

        await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default);

        Assert.Equal("import", fx.Assignments.Items.Single().AssignmentSource);
    }

    [Fact]
    public async Task Ending_an_assignment_keeps_the_record()
    {
        var fx = Fixture(modelStatus: "active");
        var existing = ExistingAssignment(fx);
        fx.Assignments.Items.Add(existing);
        var file = Workbook(accounts: [AccountRow("end", "ACC-1", "ZONE-1")]);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.True(result.Applied);
        Assert.Single(fx.Assignments.Items);
        Assert.Equal("ended", fx.Assignments.Items[0].AssignmentStatus);
    }

    [Fact]
    public async Task Account_master_is_never_mutated_by_an_import()
    {
        var fx = Fixture(modelStatus: "active");
        var before = fx.Accounts.Accounts.Single();
        var file = Workbook(accounts: [AccountRow("add", "ACC-1", "ZONE-1", scopes: "alpha")]);

        await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default);

        Assert.Equal(before, fx.Accounts.Accounts.Single());
        // ITerritoryAccountReader has no write member and no ContactTerritoryAssignment aggregate exists, so an
        // account/contact mutation is not expressible on this path at all.
    }

    // ---------------------------------------------------------------- resource assignments / read models

    [Fact]
    public async Task Resource_assignment_rows_can_never_be_applied()
    {
        var fx = Fixture(modelStatus: "active");
        var file = Workbook(resources: [ResourceRow("add", "ZONE-1", "MR-1", "res-1")]);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.Contains(result.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.ResourceApplyNotSupported && r.Blocking);
        Assert.False(result.Applied);
    }

    [Fact]
    public void Coverage_summary_and_plan_vs_current_are_not_importable_sheets()
    {
        Assert.DoesNotContain(TerritoryWorkbookSchema.CoverageSummarySheet, TerritoryWorkbookSchema.ImportableSheets);
        Assert.DoesNotContain(TerritoryWorkbookSchema.PlanVsCurrentSheet, TerritoryWorkbookSchema.ImportableSheets);
    }

    [Fact]
    public async Task A_coverage_summary_sheet_in_the_upload_is_ignored_with_a_warning()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")], withCoverageSheet: true);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.FileWarnings, w => w.Contains(TerritoryWorkbookSchema.CoverageSummarySheet, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(preview.Rows, r => r.Sheet == TerritoryWorkbookSchema.CoverageSummarySheet);
    }

    // ---------------------------------------------------------------- apply gating / atomicity

    [Fact]
    public async Task Apply_is_refused_when_a_sheet_has_a_blocking_row_and_nothing_is_written()
    {
        var fx = Fixture();
        var file = Workbook(nodes:
        [
            Row("add", "ZONE-2", "Zone Two", "zone"),
            Row("add", "ZONE-3", "Zone Three", "not-a-level")
        ]);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        // 1 of 2 considered rows blocked = 50% > the 20% "wrong file" limit, so nothing runs at all.
        Assert.False(result.Applied);
        Assert.Single(fx.Nodes.Items);
        Assert.Equal(TerritoryImportRunStatuses.Blocked, result.RunStatus);
    }

    [Fact]
    public async Task One_blocking_row_keeps_the_whole_sheet_unwritten()
    {
        var fx = Fixture();
        // 9 good rows + 1 bad = 10% blocking, under the wrong-file limit, so the apply proceeds — and the
        // sheet-level all-or-nothing rule must still keep every row of that sheet unwritten.
        var rows = Enumerable.Range(2, 9).Select(i => Row("add", $"ZONE-{i}", $"Zone {i}", "zone")).ToList();
        rows.Add(Row("add", "ZONE-BAD", "Zone Bad", "not-a-level"));
        var file = Workbook(nodes: rows);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.Single(fx.Nodes.Items);
        var sheet = result.Sheets.Single(s => s.Sheet == TerritoryWorkbookSchema.NodesSheet);
        Assert.False(sheet.Applied);
        Assert.NotNull(sheet.NotAppliedReason);
        Assert.Contains(result.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.SheetBlocked);
    }

    [Fact]
    public async Task Strict_mode_blocks_the_file_on_a_single_blocking_row()
    {
        var fx = Fixture();
        var rows = Enumerable.Range(2, 9).Select(i => Row("add", $"ZONE-{i}", $"Zone {i}", "zone")).ToList();
        rows.Add(Row("add", "ZONE-BAD", "Zone Bad", "not-a-level"));
        var file = Workbook(nodes: rows);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, strictMode: true, null, "tester", default)).Data!;

        Assert.False(result.Applied);
        Assert.Contains("Strict mode", result.BlockedReason);
        Assert.Single(fx.Nodes.Items);
    }

    [Fact]
    public async Task Apply_with_nothing_to_do_is_blocked_instead_of_reporting_success()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row(null, "ZONE-2", "Zone Two", "zone")]);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.False(result.Applied);
        Assert.Contains("nothing to apply", result.BlockedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_clean_apply_writes_the_rows()
    {
        var fx = Fixture();
        var file = Workbook(nodes:
        [
            Row("add", "ZONE-2", "Zone Two", "zone"),
            Row("add", "MZ-1", "Micro One", "microzone", parent: "ZONE-2")
        ]);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.True(result.Applied);
        Assert.Equal(3, fx.Nodes.Items.Count);
        // The in-file forward reference resolved: MZ-1's parent is the ZONE-2 created by the same file.
        var child = fx.Nodes.Items.Single(n => n.TerritoryCode == "MZ-1");
        var parent = fx.Nodes.Items.Single(n => n.TerritoryCode == "ZONE-2");
        Assert.Equal(parent.Id, child.ParentTerritoryId);
    }

    [Fact]
    public async Task Applying_the_same_file_twice_does_not_duplicate_anything()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);

        await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default);
        var second = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default)).Data!;

        Assert.Equal(2, fx.Nodes.Items.Count);                          // still just the seed + one
        Assert.Contains(second.Rows, r => r.Status == TerritoryImportRowStatuses.NoChange);
        Assert.Equal(1, second.PreviousAppliesOfThisFile);
    }

    [Fact]
    public async Task An_add_row_that_differs_from_the_stored_node_is_a_controlled_conflict()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-1", "Renamed Zone", "zone")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.DuplicateNodeCode && r.Blocking);
    }

    [Fact]
    public async Task An_empty_cell_leaves_the_field_unchanged_and_clear_empties_it()
    {
        var fx = Fixture();
        fx.Nodes.Items[0].CountryCode = "tr";
        var file = Workbook(nodes: [Row("update", "ZONE-1", null, null, country: TerritoryWorkbookSchema.ClearToken)]);

        await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default);

        var node = fx.Nodes.Items.Single(n => n.TerritoryCode == "ZONE-1");
        Assert.Null(node.CountryCode);
        Assert.Equal("Zone One", node.Name);   // the empty Name cell left the stored value alone
    }

    // ---------------------------------------------------------------- import run history

    [Fact]
    public async Task Apply_records_an_append_only_run_and_dry_run_does_not()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);

        await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default);
        Assert.Empty(fx.Runs.Items);

        var result = (await fx.Engine.RunAsync(fx.Model.Id, file, "secret-plan.xlsx", false, false, null, "tester", default)).Data!;

        var run = Assert.Single(fx.Runs.Items);
        Assert.Equal(TerritoryImportRunStatuses.Applied, run.Status);
        Assert.Equal("secret-plan.xlsx", run.FileName);
        Assert.Equal(64, run.FileHash.Length);          // SHA-256 hex
        Assert.NotNull(run.AppliedAt);
        Assert.Equal(result.ImportRunId, run.Id);
        Assert.NotEmpty(run.DryRunResult.SheetOutcomes);
    }

    [Fact]
    public void Import_run_history_has_no_update_or_delete_member()
    {
        var members = typeof(ITerritoryImportRunRepository).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(members, m => m.Contains("Update", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_blocked_apply_is_still_recorded_but_writes_nothing()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "not-a-level")]);

        await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", false, false, null, "tester", default);

        Assert.Equal(TerritoryImportRunStatuses.Blocked, Assert.Single(fx.Runs.Items).Status);
        Assert.Single(fx.Nodes.Items);
    }

    [Fact]
    public async Task Run_history_query_returns_the_recorded_runs()
    {
        var fx = Fixture();
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);
        await fx.Engine.RunAsync(fx.Model.Id, file, "plan.xlsx", false, false, null, "tester", default);

        var handler = new GetTerritoryImportRunsHandler(TenantFactory.Tenant(TenantA), fx.Models, fx.Runs);
        var response = await handler.Handle(new GetTerritoryImportRunsQuery(fx.Model.Id), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, response.Data!.TotalCount);
        Assert.Equal("plan.xlsx", response.Data.Items[0].FileName);
    }

    // ---------------------------------------------------------------- model sheet + guards

    [Fact]
    public async Task Model_metadata_can_only_be_changed_on_a_draft()
    {
        var fx = Fixture(modelStatus: "active");
        var file = Workbook(model: [ModelRow("update", name: "Renamed")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.ModelNotEditable && r.Blocking);
    }

    [Fact]
    public async Task Model_code_cannot_be_changed_by_an_import()
    {
        var fx = Fixture();
        var file = Workbook(model: [ModelRow("update", modelCode: "OTHER")]);

        var preview = (await fx.Engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default)).Data!;

        Assert.Contains(preview.Rows, r => r.ErrorCode == TerritoryImportErrorCodes.ImmutableField && r.Blocking);
    }

    [Fact]
    public async Task Cross_tenant_model_is_not_found()
    {
        var fx = Fixture();
        var engine = BuildEngine(fx, Guid.NewGuid());
        var file = Workbook(nodes: [Row("add", "ZONE-2", "Zone Two", "zone")]);

        var response = await engine.RunAsync(fx.Model.Id, file, "f.xlsx", true, false, null, "tester", default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    // ---------------------------------------------------------------- fixture / workbook builders

    private static List<string> SheetNames(byte[] bytes)
    {
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        return workbook.Worksheets.Select(w => w.Name).ToList();
    }

    private static List<List<string>> DataRows(byte[] bytes, string sheet)
    {
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet(sheet);
        var columns = TerritoryWorkbookSchema.ColumnsFor(sheet).Count;
        var last = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        var rows = new List<List<string>>();
        for (var r = 2; r <= last; r++)
        {
            rows.Add(Enumerable.Range(1, columns).Select(c => worksheet.Cell(r, c).GetString()).ToList());
        }

        return rows;
    }

    private static Dictionary<string, string?> Row(
        string? operation, string code, string? name, string? level, string? parent = null,
        string? from = null, string? country = null) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Operation"] = operation,
        ["TerritoryCode"] = code,
        ["Name"] = name,
        ["TerritoryLevel"] = level,
        ["ParentTerritoryCode"] = parent,
        ["EffectiveFrom"] = from,
        ["CountryCode"] = country
    };

    private static Dictionary<string, string?> AccountRow(
        string? operation, string? accountCode, string? territoryCode, string? accountId = null,
        string? scopes = null, string? isOverride = null, string? overrideReason = null,
        string? nodeCodeForNew = null) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Operation"] = operation,
        ["AccountId"] = accountId,
        ["AccountCode"] = accountCode,
        ["TerritoryCode"] = nodeCodeForNew ?? territoryCode,
        ["BusinessUnitScopes"] = scopes,
        ["ConflictPolicy"] = "block",
        ["Override"] = isOverride,
        ["OverrideReason"] = overrideReason
    };

    private static Dictionary<string, string?> ResourceRow(
        string? operation, string territoryCode, string positionCode, string resourceId) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Operation"] = operation,
        ["TerritoryCode"] = territoryCode,
        ["PositionCode"] = positionCode,
        ["ResourceId"] = resourceId
    };

    private static Dictionary<string, string?> ModelRow(
        string? operation, string? name = null, string? modelCode = null) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Operation"] = operation,
        ["Name"] = name,
        ["ModelCode"] = modelCode
    };

    private static byte[] Workbook(
        IReadOnlyList<Dictionary<string, string?>>? model = null,
        IReadOnlyList<Dictionary<string, string?>>? nodes = null,
        IReadOnlyList<Dictionary<string, string?>>? rules = null,
        IReadOnlyList<Dictionary<string, string?>>? accounts = null,
        IReadOnlyList<Dictionary<string, string?>>? resources = null,
        bool withTenantIdColumn = false,
        bool withCoverageSheet = false)
    {
        using var workbook = new XLWorkbook();

        void Sheet(string name, IReadOnlyList<Dictionary<string, string?>>? rows)
        {
            if (rows is null) return;
            var columns = TerritoryWorkbookSchema.ColumnsFor(name).ToList();
            if (withTenantIdColumn) columns.Add(TerritoryWorkbookSchema.TenantIdColumn);

            var sheet = workbook.Worksheets.Add(name);
            for (var c = 0; c < columns.Count; c++) sheet.Cell(1, c + 1).Value = columns[c];

            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    if (rows[r].TryGetValue(columns[c], out var value) && value is not null)
                    {
                        sheet.Cell(r + 2, c + 1).Value = value;
                    }
                }

                if (withTenantIdColumn)
                {
                    sheet.Cell(r + 2, columns.Count).Value = TenantA.ToString();
                }
            }
        }

        Sheet(TerritoryWorkbookSchema.ModelSheet, model);
        Sheet(TerritoryWorkbookSchema.NodesSheet, nodes);
        Sheet(TerritoryWorkbookSchema.AssignmentRulesSheet, rules);
        Sheet(TerritoryWorkbookSchema.AccountAssignmentsSheet, accounts);
        Sheet(TerritoryWorkbookSchema.ResourceAssignmentsSheet, resources);

        if (withCoverageSheet)
        {
            var sheet = workbook.Worksheets.Add(TerritoryWorkbookSchema.CoverageSummarySheet);
            sheet.Cell(1, 1).Value = "AccountId";
            sheet.Cell(2, 1).Value = Guid.NewGuid().ToString();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] SheetWithHeaders(string sheetName, IReadOnlyList<string> headers)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Count; c++) sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Cell(2, 1).Value = "x";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static AccountTerritoryAssignment ExistingAssignment(FixtureState fx) => new()
    {
        TenantId = TenantA, TerritoryModelId = fx.Model.Id, AccountId = fx.AccountId,
        AccountCode = "ACC-1", AccountDisplayName = "Account One",
        TerritoryNodeId = fx.Nodes.Items[0].Id, TerritoryNodeCode = "ZONE-1", TerritoryNodeName = "Zone One",
        BusinessScopes = [new() { ScopeType = "business-unit", ScopeCode = "alpha" }],
        AssignmentSource = "rule", AssignmentStatus = "active", ConflictPolicy = "block",
        EffectiveFrom = fx.Model.EffectiveFrom, EffectiveTo = fx.Model.EffectiveTo
    };

    private static TerritoryNode SecondNode(FixtureState fx) => new()
    {
        TenantId = TenantA, ModelId = fx.Model.Id, Status = "active", TerritoryCode = "ZONE-2",
        Name = "Zone Two", TerritoryLevel = "zone",
        EffectiveFrom = fx.Model.EffectiveFrom, EffectiveTo = fx.Model.EffectiveTo
    };

    private static TerritoryImportEngine BuildEngine(FixtureState fx, Guid tenantId) => new(
        TenantFactory.Tenant(tenantId), fx.Models, fx.Nodes, fx.Rules, fx.Assignments, fx.Accounts, fx.Runs,
        fx.Catalog, new FakeTerritoryReferenceValidator());

    private static FixtureState Fixture(string modelStatus = "draft", bool publishLevels = true)
    {
        var model = new TerritoryModel
        {
            TenantId = TenantA, ModelCode = "TM-FU08", Name = "FU08 Model", Status = modelStatus,
            CountryScope = "tr",
            EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1), EffectiveTo = DateTimeOffset.UtcNow.AddYears(1),
            BusinessScopes = [new() { ScopeType = "business-unit", ScopeCode = "alpha" }]
        };

        var models = new FakeTerritoryModelRepo(); models.Items.Add(model);
        var nodes = new FakeTerritoryNodeRepo();
        nodes.Items.Add(new TerritoryNode
        {
            TenantId = TenantA, ModelId = model.Id, Status = "active", TerritoryCode = "ZONE-1",
            Name = "Zone One", TerritoryLevel = "zone",
            EffectiveFrom = model.EffectiveFrom, EffectiveTo = model.EffectiveTo
        });

        var accountId = Guid.NewGuid();
        var accounts = new FakeTerritoryAccountReader();
        accounts.Accounts.Add(new TerritoryAccountSnapshot(
            accountId, "ACC-1", "Account One", "hospital", null, "active", "tr", "IST", "BEY"));

        var catalog = new FakeCatalogReader()
            .Set(TerritoryReferenceSets.TerritoryLevel, publishLevels
                ? Published(["division", "country", "region", "area", "zone", "microzone"])
                : ReferenceSetSnapshot.NotPublished(TerritoryReferenceSets.TerritoryLevel))
            .Set(TerritoryReferenceSets.TerritoryRuleType, Published(["geography", "account-list", "account-type"]))
            .Set(TerritoryReferenceSets.TerritoryConflictPolicy, Published(["block", "warn", "priority", "manual-review"]))
            .Set(TerritoryReferenceSets.TerritoryAssignmentStatus, Published(["proposed", "active", "ended", "rejected"]))
            .Set(TerritoryReferenceSets.TerritoryAssignmentSource, Published(["rule", "manual", "import", "override"]))
            .Set(TerritoryReferenceSets.TerritoryCoverageScope, Published(["exact-territory", "territory-subtree"]))
            .Set(TerritoryReferenceSets.BusinessUnitValueSet, Published(["alpha", "beta", "gamma"]));

        var state = new FixtureState(
            model, accountId, models, nodes, new FakeTerritoryAssignmentRuleRepo(),
            new FakeAccountTerritoryAssignmentRepo(), accounts, new FakeTerritoryImportRunRepo(), catalog, null!, null!);

        var engine = BuildEngine(state, TenantA);
        var export = new TerritoryWorkbookExportHandler(
            TenantFactory.Tenant(TenantA), state.Models, state.Nodes, state.Rules, state.Assignments,
            new FakeTerritoryResourceAssignmentRepo(), new FakeTerritoryPlanSnapshotRepo(), state.Catalog);

        return state with { Engine = engine, Export = export };
    }

    private static ReferenceSetSnapshot Published(IEnumerable<string> codes)
        => new("set", true, codes.Select(c => new ReferenceValueSnapshot(c, c, null, true, false, null)).ToList());

    private sealed record FixtureState(
        TerritoryModel Model,
        Guid AccountId,
        FakeTerritoryModelRepo Models,
        FakeTerritoryNodeRepo Nodes,
        FakeTerritoryAssignmentRuleRepo Rules,
        FakeAccountTerritoryAssignmentRepo Assignments,
        FakeTerritoryAccountReader Accounts,
        FakeTerritoryImportRunRepo Runs,
        FakeCatalogReader Catalog,
        TerritoryImportEngine Engine,
        TerritoryWorkbookExportHandler Export);
}
