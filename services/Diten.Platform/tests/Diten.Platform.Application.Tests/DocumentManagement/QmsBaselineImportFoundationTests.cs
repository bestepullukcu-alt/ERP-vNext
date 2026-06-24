using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class QmsBaselineImportFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string SourceKey = "qms-v2";
    private const string Correlation = "fu02-corr-001";

    private static IReadOnlyList<QmsFolderImportRow> ValidRows() =>
    [
        new(1, "Quality", null, null, null, null, null, null, null, null, null, null, null, null, null),
        new(2, "Quality/Manuals", null, null, null, null, null, null, null, null, null, null, null, null, null),
        new(3, "Quality/Manuals/SOP", null, null, null, null, null, null, null, null, null, null, null, null, null)
    ];

    private static QmsFolderTreeValidator Validator() => new();

    [Fact]
    public void Dry_run_valid_import_builds_ordered_tree_and_writes_nothing()
    {
        var plan = Validator().BuildPlan(ValidRows(), TenantA, SourceKey);

        Assert.True(plan.Summary.IsValid);
        Assert.Equal(3, plan.Summary.TotalRows);
        Assert.Equal(3, plan.Summary.ImportedDefinitionsCount);
        Assert.Equal(3, plan.Definitions.Count);
        Assert.Equal(
            new[] { "Quality", "Quality/Manuals", "Quality/Manuals/SOP" },
            plan.Definitions.Select(d => d.FullPath).ToArray());
        Assert.Null(plan.Definitions[0].ParentCanonicalId);
        Assert.Equal(plan.Definitions[0].CanonicalId, plan.Definitions[1].ParentCanonicalId);
    }

    [Fact]
    public void Dry_run_invalid_hierarchy_gap_is_reported()
    {
        // "Quality/Manuals/SOP" with no parent "Quality/Manuals" => hierarchy gap.
        IReadOnlyList<QmsFolderImportRow> rows =
        [
            new(1, "Quality", null, null, null, null, null, null, null, null, null, null, null, null, null),
            new(2, "Quality/Manuals/SOP", null, null, null, null, null, null, null, null, null, null, null, null, null)
        ];

        var plan = Validator().BuildPlan(rows, TenantA, SourceKey);

        Assert.False(plan.Summary.IsValid);
        Assert.NotEmpty(plan.Summary.InvalidHierarchyFindings);
        Assert.Empty(plan.Definitions);
    }

    [Fact]
    public void Duplicate_sibling_path_is_a_conflict()
    {
        IReadOnlyList<QmsFolderImportRow> rows =
        [
            new(1, "Quality", null, null, null, null, null, null, null, null, null, null, null, null, null),
            new(2, "quality", null, null, null, null, null, null, null, null, null, null, null, null, null)
        ];

        var plan = Validator().BuildPlan(rows, TenantA, SourceKey);

        Assert.NotEmpty(plan.Summary.DuplicatePathConflicts);
        var failure = QmsBaselineMapping.ClassifyFailure(plan.Summary);
        Assert.Equal(409, failure!.Value.Status);
        Assert.Equal(QmsBaselineReasonCodes.Conflict, failure.Value.ReasonCode);
    }

    [Fact]
    public void Empty_folder_name_is_rejected()
    {
        IReadOnlyList<QmsFolderImportRow> rows =
        [
            new(1, null, null, "  ", null, null, null, null, null, null, null, null, null, null, null)
        ];

        var plan = Validator().BuildPlan(rows, TenantA, SourceKey);

        Assert.False(plan.Summary.IsValid);
    }

    [Fact]
    public void Canonical_id_and_definition_hash_are_deterministic_for_same_input()
    {
        var first = Validator().BuildPlan(ValidRows(), TenantA, SourceKey);
        var second = Validator().BuildPlan(ValidRows(), TenantA, SourceKey);

        Assert.Equal(
            first.Definitions.Select(d => d.CanonicalId),
            second.Definitions.Select(d => d.CanonicalId));
        Assert.Equal(
            first.Definitions.Select(d => d.DefinitionHash),
            second.Definitions.Select(d => d.DefinitionHash));
    }

    [Fact]
    public void Canonical_id_differs_per_tenant()
    {
        var a = Validator().BuildPlan(ValidRows(), TenantA, SourceKey);
        var b = Validator().BuildPlan(ValidRows(), TenantB, SourceKey);

        Assert.NotEqual(a.Definitions[0].CanonicalId, b.Definitions[0].CanonicalId);
    }

    [Fact]
    public void Snapshot_hash_is_deterministic_for_same_definitions()
    {
        var plan = Validator().BuildPlan(ValidRows(), TenantA, SourceKey);
        var hasher = new BaselineSnapshotHasher();

        var firstSet = ToEntities(plan, TenantA);
        var secondSet = ToEntities(plan, TenantA);

        var first = hasher.Compute(firstSet);
        var second = hasher.Compute(secondSet);

        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
        Assert.Equal(first.StructuralControlsHash, second.StructuralControlsHash);
        Assert.Equal(plan.Definitions.Count, first.DefinitionIds.Count);
    }

    [Fact]
    public async Task Commit_creates_definition_tree_and_draft_baseline()
    {
        var tenantContext = Resolved(TenantA);
        var baselineRepo = new FakeBaselineReleaseRepository();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var importService = new QmsBaselineImportService([new StubParser(ValidRows())], Validator(), new DottedOutlineTreeBuilder());
        var handler = new CommitQmsBaselineImportHandler(importService, baselineRepo, definitionRepo, tenantContext);

        var response = await handler.Handle(
            new CommitQmsBaselineImportCommand("qms.xlsx", "xlsx", "Zm9v", SourceKey, "1.0", null, Correlation),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(Correlation, response.CorrelationId);
        Assert.True(response.Data!.Summary.Committed);
        Assert.Single(baselineRepo.Created);
        Assert.Equal(BaselineReleaseStatus.Draft, baselineRepo.Created[0].Status);
        Assert.Equal(3, definitionRepo.Created.Count);
        Assert.All(definitionRepo.Created, d => Assert.Equal(TenantA, d.TenantId));
    }

    [Fact]
    public async Task Publish_creates_immutable_manifest_with_deterministic_hash()
    {
        var tenantContext = Resolved(TenantA);
        var plan = Validator().BuildPlan(ValidRows(), TenantA, SourceKey);
        var baseline = DraftBaseline();
        var baselineRepo = new FakeBaselineReleaseRepository();
        baselineRepo.Created.Add(baseline);
        var definitionRepo = new FakeCollectionDefinitionRepository();
        definitionRepo.Created.AddRange(ToEntities(plan, TenantA, baseline.Id));
        var manifestRepo = new FakeManifestRepository();
        var handler = new PublishQmsBaselineHandler(baselineRepo, definitionRepo, manifestRepo, new BaselineSnapshotHasher(), tenantContext);

        var response = await handler.Handle(new PublishQmsBaselineCommand(baseline.Id, 1, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("PUBLISHED", response.Data!.Status);
        Assert.Single(manifestRepo.Created);
        Assert.False(string.IsNullOrWhiteSpace(response.Data.SnapshotHash));
        Assert.Equal(BaselineReleaseStatus.Published, baseline.Status);
    }

    [Fact]
    public async Task Publish_non_draft_baseline_is_rejected()
    {
        var tenantContext = Resolved(TenantA);
        var baseline = DraftBaseline();
        baseline.Status = BaselineReleaseStatus.Published;
        var baselineRepo = new FakeBaselineReleaseRepository();
        baselineRepo.Created.Add(baseline);
        var handler = new PublishQmsBaselineHandler(
            baselineRepo, new FakeCollectionDefinitionRepository(), new FakeManifestRepository(), new BaselineSnapshotHasher(), tenantContext);

        var response = await handler.Handle(new PublishQmsBaselineCommand(baseline.Id, 0, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, response.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_baseline_detail_is_404_non_leakage()
    {
        // Repo returns null for another tenant's id (tenant-filtered in production); fake mirrors that.
        var tenantContext = Resolved(TenantB);
        var handler = new PublishQmsBaselineHandler(
            new FakeBaselineReleaseRepository(), new FakeCollectionDefinitionRepository(), new FakeManifestRepository(), new BaselineSnapshotHasher(), tenantContext);

        var response = await handler.Handle(new PublishQmsBaselineCommand(Guid.NewGuid(), 0, Correlation), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Dry_run_controlled_failure_carries_reason_code_and_correlation()
    {
        var tenantContext = Resolved(TenantA);
        var importService = new QmsBaselineImportService([new StubParser([])], Validator(), new DottedOutlineTreeBuilder());
        var handler = new DryRunQmsBaselineImportHandler(importService, tenantContext);

        // Empty base64 → invalid payload (VALIDATION_FAILED) with reason_code + correlation.
        var response = await handler.Handle(
            new DryRunQmsBaselineImportCommand("qms.xlsx", "xlsx", string.Empty, SourceKey, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, response.ReasonCode);
        Assert.Equal(Correlation, response.CorrelationId);
        Assert.DoesNotContain(response.Errors, e => e.Contains("Exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_permission_returns_403_perm_denied()
    {
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelationId(Correlation);
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICorrelationContext>(correlationContext)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("actor_type", "tenant_user")], "Test")),
            RequestServices = serviceProvider
        };
        var filterContext = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);

        await new HasPermissionAttribute(QmsBaselinePermissions.Import).OnAuthorizationAsync(filterContext);

        var result = Assert.IsType<ObjectResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void Xlsx_parser_reads_header_and_rows()
    {
        var bytes = BuildMinimalXlsx(["path"], [["Quality"], ["Quality/Manuals"]]);
        var parser = new XlsxQmsFolderImportParser();

        Assert.True(parser.Supports("xlsx", "qms.xlsx"));
        var rows = parser.ParseAsync(bytes).GetAwaiter().GetResult();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Quality", rows[0].Path);
        Assert.Equal("Quality/Manuals", rows[1].Path);
    }

    [Fact]
    public void Xlsx_parser_reads_real_workbook_level_columns_and_dry_run_is_valid()
    {
        // Mirrors the real "Configuraiton of QMS folders v2" Arkusz1 shape: 1st/2nd/3rd/4th level columns.
        var bytes = BuildMinimalXlsx(
            ["1st", "2nd", "3rd", "4th"],
            [
                ["GMP QMS", "", "", ""],
                ["GMP QMS", "Certificates and permits", "", ""],
                ["GMP QMS", "Certificates and permits", "CPP", ""]
            ]);
        var parser = new XlsxQmsFolderImportParser();

        var rows = parser.ParseAsync(bytes).GetAwaiter().GetResult();

        Assert.Equal(3, rows.Count);
        Assert.Equal("GMP QMS", rows[0].Path);
        Assert.Equal("GMP QMS/Certificates and permits", rows[1].Path);
        Assert.Equal("GMP QMS/Certificates and permits/CPP", rows[2].Path);

        var plan = Validator().BuildPlan(rows, TenantA, SourceKey);
        Assert.True(plan.Summary.IsValid);
        Assert.Equal(3, plan.Definitions.Count);
        Assert.Equal("CPP", plan.Definitions[2].Name);
    }

    // --- Canonical "last version" dotted-outline format ---

    [Fact]
    public void Parser_selects_last_version_sheet_and_reads_dotted_outline_codes()
    {
        var bytes = BuildMinimalXlsx(
            ["Folder (full path)", "Folder name"],
            [["0", "META & STANDARDS"], ["0.01", "Information Architecture"], ["00.01.01", "Folder Model"]],
            sheetName: XlsxQmsFolderImportParser.CanonicalSheetName);
        var parser = new XlsxQmsFolderImportParser();

        var rows = parser.ParseAsync(bytes).GetAwaiter().GetResult();

        Assert.Equal(3, rows.Count);
        Assert.Equal("0", rows[0].OutlineCode);
        Assert.Equal("META & STANDARDS", rows[0].Name);
        Assert.Equal("00.01.01", rows[2].OutlineCode);
        Assert.Null(rows[0].Path); // dotted code is NOT a path
    }

    [Fact]
    public void Dotted_codes_build_a_nested_tree_not_a_flat_list()
    {
        var plan = new DottedOutlineTreeBuilder().BuildPlan(
            [Dotted(1, "0", "Root"), Dotted(2, "0.01", "Child A"), Dotted(3, "00.01.01", "Grandchild")],
            TenantA, SourceKey);

        Assert.True(plan.Summary.IsValid);
        Assert.Equal(3, plan.Definitions.Count);
        // Nested, server-derived full paths from names (not codes).
        Assert.Equal("Root", plan.Definitions[0].FullPath);
        Assert.Equal("Root/Child A", plan.Definitions[1].FullPath);
        Assert.Equal("Root/Child A/Grandchild", plan.Definitions[2].FullPath);
        // Parent linkage via numeric code normalization (00.01.01 -> parent 0.01 -> parent 0).
        Assert.Null(plan.Definitions[0].ParentCanonicalId);
        Assert.Equal(plan.Definitions[0].CanonicalId, plan.Definitions[1].ParentCanonicalId);
        Assert.Equal(plan.Definitions[1].CanonicalId, plan.Definitions[2].ParentCanonicalId);
        // The dotted code is never used as the name/segment.
        Assert.Equal("Grandchild", plan.Definitions[2].PathSegment);
    }

    [Fact]
    public void Missing_parent_dotted_code_is_a_validation_failed_gap()
    {
        var plan = new DottedOutlineTreeBuilder().BuildPlan(
            [Dotted(1, "0", "Root"), Dotted(2, "00.07.01", "Orphan")], // parent key 0.7 absent
            TenantA, SourceKey);

        Assert.False(plan.Summary.IsValid);
        Assert.NotEmpty(plan.Summary.InvalidHierarchyFindings);
        var failure = QmsBaselineMapping.ClassifyFailure(plan.Summary);
        Assert.Equal(400, failure!.Value.Status);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, failure.Value.ReasonCode);
    }

    [Fact]
    public void Duplicate_sibling_under_same_dotted_parent_is_a_conflict()
    {
        var plan = new DottedOutlineTreeBuilder().BuildPlan(
            [Dotted(1, "0", "Root"), Dotted(2, "0.01", "Child"), Dotted(3, "0.02", "child")], // same parent 0, same name
            TenantA, SourceKey);

        Assert.NotEmpty(plan.Summary.DuplicatePathConflicts);
        var failure = QmsBaselineMapping.ClassifyFailure(plan.Summary);
        Assert.Equal(409, failure!.Value.Status);
        Assert.Equal(QmsBaselineReasonCodes.Conflict, failure.Value.ReasonCode);
    }

    [Fact]
    public void Depth_two_decimal_outline_codes_are_normalized_to_two_digit_segments()
    {
        // Real workbook rows can store depth-2 outline codes as Excel decimals: "0.1" is the parent for
        // "00.10.01", while "0.01" remains a distinct sibling.
        var plan = new DottedOutlineTreeBuilder().BuildPlan(
            [
                Dotted(1, "0", "Section"),
                Dotted(2, "0.01", "Information Architecture"),
                Dotted(3, "00.01.01", "Folder Model"),
                Dotted(4, "0.1", "Glossary"),
                Dotted(5, "00.10.01", "Corporate Glossary")
            ],
            TenantA, SourceKey);

        Assert.True(plan.Summary.IsValid);
        Assert.Empty(plan.Summary.DuplicatePathConflicts);
        Assert.Equal(5, plan.Definitions.Count);

        var section = plan.Definitions.Single(d => d.Name == "Section");
        var infoArch = plan.Definitions.Single(d => d.Name == "Information Architecture");
        var grandchild = plan.Definitions.Single(d => d.Name == "Folder Model");
        var glossary = plan.Definitions.Single(d => d.Name == "Glossary");
        var corporateGlossary = plan.Definitions.Single(d => d.Name == "Corporate Glossary");
        Assert.Equal(infoArch.CanonicalId, grandchild.ParentCanonicalId);   // 00.01.01 -> 0.01
        Assert.Equal(section.CanonicalId, glossary.ParentCanonicalId);      // 0.1 -> 0.10 -> parent 0
        Assert.Equal(glossary.CanonicalId, corporateGlossary.ParentCanonicalId);
    }

    [Fact]
    public void Floating_artifact_outline_codes_resolve_to_padded_dotted_parent_keys()
    {
        var plan = new DottedOutlineTreeBuilder().BuildPlan(
            [
                Dotted(1, "0", "Root"),
                Dotted(2, "7.0000000000000007E-2", "Integration & API Standards"),
                Dotted(3, "00.07.01", "API Design Guidelines"),
                Dotted(4, "1", "Governance"),
                Dotted(5, "1.1000000000000001", "Ethics, Investigations & Sanctions"),
                Dotted(6, "01.10.01", "Case Intake & Triage"),
                Dotted(7, "1.1299999999999999", "Director & Officer Administration"),
                Dotted(8, "01.13.01", "D&O Insurance"),
                Dotted(9, "6", "People"),
                Dotted(10, "6.03", "Onboarding & Offboarding"),
                Dotted(11, "06.3.03", "Probation Management")
            ],
            TenantA, SourceKey);

        Assert.True(plan.Summary.IsValid);
        Assert.Empty(plan.Summary.InvalidHierarchyFindings);

        var apiStandards = plan.Definitions.Single(d => d.Name == "Integration & API Standards");
        var apiGuidelines = plan.Definitions.Single(d => d.Name == "API Design Guidelines");
        var ethics = plan.Definitions.Single(d => d.Name == "Ethics, Investigations & Sanctions");
        var caseIntake = plan.Definitions.Single(d => d.Name == "Case Intake & Triage");
        var directorAdmin = plan.Definitions.Single(d => d.Name == "Director & Officer Administration");
        var insurance = plan.Definitions.Single(d => d.Name == "D&O Insurance");
        var onboarding = plan.Definitions.Single(d => d.Name == "Onboarding & Offboarding");
        var probation = plan.Definitions.Single(d => d.Name == "Probation Management");
        Assert.Equal(apiStandards.CanonicalId, apiGuidelines.ParentCanonicalId);
        Assert.Equal(ethics.CanonicalId, caseIntake.ParentCanonicalId);
        Assert.Equal(directorAdmin.CanonicalId, insurance.ParentCanonicalId);
        Assert.Equal(onboarding.CanonicalId, probation.ParentCanonicalId);
    }

    [Fact]
    public void Atomic_folder_name_with_slash_is_preserved_not_split()
    {
        var plan = new DottedOutlineTreeBuilder().BuildPlan(
            [Dotted(1, "0", "Root"), Dotted(2, "0.01", "Versioning & Check-in/Check-out")],
            TenantA, SourceKey);

        Assert.True(plan.Summary.IsValid);
        Assert.Equal("Versioning & Check-in/Check-out", plan.Definitions[1].Name);
        Assert.Equal("Root/Versioning & Check-in/Check-out", plan.Definitions[1].FullPath);
    }

    [Fact]
    public void Dotted_import_is_deterministic_for_canonical_id_and_snapshot_hash()
    {
        var builder = new DottedOutlineTreeBuilder();
        var first = builder.BuildPlan(DottedSample(), TenantA, SourceKey);
        var second = builder.BuildPlan(DottedSample(), TenantA, SourceKey);

        Assert.Equal(first.Definitions.Select(d => d.CanonicalId), second.Definitions.Select(d => d.CanonicalId));

        var hasher = new BaselineSnapshotHasher();
        var h1 = hasher.Compute(ToEntities(first, TenantA));
        var h2 = hasher.Compute(ToEntities(second, TenantA));
        Assert.Equal(h1.SnapshotHash, h2.SnapshotHash);
    }

    [Fact]
    public void Parser_throws_when_canonical_sheet_is_absent()
    {
        var bytes = BuildMinimalXlsx(
            ["Folder (full path)", "Folder name"],
            [["0", "Root"]],
            sheetName: "Arkusz1"); // not the canonical "last version" sheet
        var parser = new XlsxQmsFolderImportParser();

        var ex = Assert.Throws<QmsWorkbookFormatException>(() =>
        {
            _ = parser.ParseAsync(bytes).GetAwaiter().GetResult();
        });
        Assert.Equal("canonical_sheet_not_found", ex.Message);
    }

    [Fact]
    public async Task Dotted_workbook_dry_run_through_service_is_valid_and_persists_nothing()
    {
        var bytes = BuildMinimalXlsx(
            ["Folder (full path)", "Folder name"],
            [["0", "Root"], ["0.01", "Child"], ["00.01.01", "Grandchild"]]);
        var contentBase64 = Convert.ToBase64String(bytes);
        var importService = new QmsBaselineImportService(
            [new XlsxQmsFolderImportParser()], Validator(), new DottedOutlineTreeBuilder());

        var plan = await importService.BuildPlanAsync("qms.xlsx", "xlsx", contentBase64, SourceKey, TenantA);

        Assert.True(plan.Summary.IsValid);
        Assert.Equal(3, plan.Definitions.Count);
        Assert.Equal("Root/Child/Grandchild", plan.Definitions[2].FullPath);
    }

    private static QmsFolderImportRow Dotted(int rowNumber, string code, string name) =>
        new(rowNumber, null, null, name, null, null, null, null, null, null, null, null, null, null, null, code);

    private static IReadOnlyList<QmsFolderImportRow> DottedSample() =>
    [
        Dotted(1, "0", "META & STANDARDS"),
        Dotted(2, "0.01", "Information Architecture"),
        Dotted(3, "00.01.01", "Folder Model"),
        Dotted(4, "00.01.02", "Controlled Vocabulary"),
        Dotted(5, "0.02", "Master Data")
    ];

    // --- helpers ---

    private static TenantContext Resolved(Guid tenantId)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    private static BaselineRelease DraftBaseline() => new()
    {
        TenantId = TenantA,
        BaselineReleaseId = "BR-TEST000000001",
        SourceBaselineKey = SourceKey,
        BaselineVersion = "1.0",
        Status = BaselineReleaseStatus.Draft
    };

    private static List<CollectionDefinition> ToEntities(QmsBaselineImportPlan plan, Guid tenantId, Guid? baselineId = null)
    {
        var bid = baselineId ?? Guid.NewGuid();
        return plan.Definitions.Select(d => new CollectionDefinition
        {
            TenantId = tenantId,
            CanonicalId = d.CanonicalId,
            ParentCanonicalId = d.ParentCanonicalId,
            BaselineReleaseId = bid,
            Name = d.Name,
            RequiredByScope = d.RequiredByScope,
            PathSegment = d.PathSegment,
            FullPath = d.FullPath,
            DisplayOrder = d.DisplayOrder,
            DefinitionHash = d.DefinitionHash
        }).ToList();
    }

    /// <summary>
    /// Builds a minimal but real .xlsx: a workbook with one named sheet (default "last version", the FU02 canonical
    /// sheet) wired through workbook relationships, plus inline-string cells. Header is row 1.
    /// </summary>
    private static byte[] BuildMinimalXlsx(
        IReadOnlyList<string> header,
        IReadOnlyList<IReadOnlyList<string>> dataRows,
        string sheetName = "last version")
    {
        static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        static string CellsXml(IReadOnlyList<string> values, int rowNumber)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < values.Count; i++)
            {
                var col = (char)('A' + i);
                sb.Append($"<c r=\"{col}{rowNumber}\" t=\"inlineStr\"><is><t>{Esc(values[i])}</t></is></c>");
            }

            return sb.ToString();
        }

        var rowsXml = new StringBuilder();
        rowsXml.Append($"<row r=\"1\">{CellsXml(header, 1)}</row>");
        for (var r = 0; r < dataRows.Count; r++)
        {
            rowsXml.Append($"<row r=\"{r + 2}\">{CellsXml(dataRows[r], r + 2)}</row>");
        }

        var sheet =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            $"<sheetData>{rowsXml}</sheetData></worksheet>";

        var workbook =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            $"<sheets><sheet name=\"{Esc(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

        var rels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" " +
            "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" " +
            "Target=\"worksheets/sheet1.xml\"/></Relationships>";

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "xl/workbook.xml", workbook);
            AddEntry(archive, "xl/_rels/workbook.xml.rels", rels);
            AddEntry(archive, "xl/worksheets/sheet1.xml", sheet);
        }

        return ms.ToArray();

        static void AddEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    private sealed class StubParser(IReadOnlyList<QmsFolderImportRow> rows) : IQmsFolderImportParser
    {
        public bool Supports(string format, string fileName) => true;

        public Task<IReadOnlyList<QmsFolderImportRow>> ParseAsync(byte[] content, CancellationToken ct = default) =>
            Task.FromResult(rows);
    }

    private sealed class FakeBaselineReleaseRepository : IBaselineReleaseRepository
    {
        public List<BaselineRelease> Created { get; } = [];

        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default)
        {
            Created.Add(baseline);
            return Task.FromResult(baseline);
        }

        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Created.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BaselineRelease>>(Created);

        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default)
        {
            baseline.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Created { get; } = [];

        public Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default)
        {
            Created.Add(definition);
            return Task.FromResult(definition);
        }

        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
        {
            Created.AddRange(definitions);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default)
        {
            if (definition.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            definition.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }

        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
        {
            foreach (var definition in definitions)
            {
                definition.Version++;
            }

            return Task.CompletedTask;
        }

        public Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default)
        {
            if (definition.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            definition.IsDeleted = true;
            definition.DeletedAt = DateTimeOffset.UtcNow;
            definition.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionDefinition>>(Created.Where(x => x.BaselineReleaseId == baselineReleaseId && !x.IsDeleted).ToList());

        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) =>
            Task.FromResult(Created.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId && !x.IsDeleted));
    }

    private sealed class FakeManifestRepository : IBaselineSnapshotManifestRepository
    {
        public List<BaselineSnapshotManifest> Created { get; } = [];

        public Task<BaselineSnapshotManifest> CreateAsync(BaselineSnapshotManifest manifest, CancellationToken ct = default)
        {
            Created.Add(manifest);
            return Task.FromResult(manifest);
        }

        public Task<BaselineSnapshotManifest?> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult(Created.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId));
    }
}
