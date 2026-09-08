using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>
/// MOD-0288-FU02 — the two boundaries the pack says must be PROVED, not promised.
///
/// <list type="number">
///   <item><b>§21.2</b> — no FU02 code carries organization data through <c>SafeDisplayMetadata</c> or any
///     other untyped string dictionary. That path is tempting precisely because it breaks nothing: no DTO
///     changes and no test fails. This file is the test that fails.</item>
///   <item><b>§16 criterion 4</b> — no FU02 file references <c>TaskAssignmentScopeResolver</c>,
///     <c>TaskApprovalService</c> or the workflow candidate-resolution path. FU02 adds a REPORTING
///     relationship and changes approval behaviour not at all.</item>
/// </list>
/// </summary>
public sealed class OrganizationFu02BoundaryArchitectureTests
{
    /// <summary>Every FU02 source file, by the paths the pack's §5 allowlist names.</summary>
    private static IReadOnlyList<string> Fu02Sources()
    {
        var src = Path.Combine(RepoRoot(), "services", "Diten.Platform", "src");

        var roots = new[]
        {
            Path.Combine(src, "Diten.Platform.Application", "Features", "TenantOrganization"),
            Path.Combine(src, "Diten.Platform.Domain", "Entities", "Organization"),
            Path.Combine(src, "Diten.Platform.Domain", "Repositories"),
            Path.Combine(src, "Diten.Platform.Infrastructure", "Persistence", "Repositories"),
            Path.Combine(src, "Diten.Platform.Infrastructure", "Persistence", "Schema"),
            Path.Combine(src, "Diten.Platform.API", "Controllers", "Platform")
        };

        var files = roots
            .Where(Directory.Exists)
            .SelectMany(r => Directory.EnumerateFiles(r, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Replace('\\', '/').Contains("/obj/") && !f.Replace('\\', '/').Contains("/bin/"))
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return name.Contains("Organization", StringComparison.Ordinal)
                    || name.Contains("TenantOrganization", StringComparison.Ordinal);
            })
            .ToList();

        // A guard that inspects nothing passes vacuously; this is the floor that stops that.
        Assert.True(files.Count > 20, $"only {files.Count} organization sources were found — the paths are wrong");
        return files;
    }

    [Fact]
    public void No_organization_code_carries_data_through_an_untyped_string_dictionary()
    {
        /*
         * ⚠ WHY A FORBIDDEN NAME AND NOT A FORBIDDEN TYPE. The reference is not a compile dependency —
         * SafeDisplayMetadata lives in HCM, and organization code cannot even see it. What is being stopped is
         * the NEXT step: someone adding an HCM reference in order to write to it. A name check is the only
         * check that fires before that reference exists.
         *
         * The untyped-dictionary ban is wider than that one property, because the reason is wider: an untyped
         * dictionary is not a contract. A key placed in one is undocumented six months later, has no version,
         * no validator and no consumer test. New information gets an explicit field, an explicit DTO and an
         * explicit version.
         */
        var forbidden = new[]
        {
            "SafeDisplayMetadata",
            "ReferenceValidationItem"
        };

        // …and the shape, not just the one name: an untyped string map declared on an organization contract.
        var untypedMap = new Regex(
            @"(IReadOnlyDictionary|IDictionary|Dictionary)\s*<\s*string\s*,\s*string\s*>",
            RegexOptions.Compiled);

        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Fu02Sources())
        {
            var text = File.ReadAllText(file);
            var name = Path.GetFileName(file);

            foreach (var token in forbidden.Where(t => text.Contains(t, StringComparison.Ordinal)))
            {
                offenders.Add($"{name}: writes to or names '{token}'");
            }

            if (untypedMap.IsMatch(text))
            {
                offenders.Add($"{name}: declares an untyped string dictionary");
            }
        }

        Assert.True(offenders.Count == 0,
            "MOD-0288-FU02 §21.2 — organization data must travel as an explicit field on an explicit DTO, "
            + "never through an untyped string dictionary:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void No_organization_code_references_the_approval_or_task_scope_path()
    {
        /*
         * §16 criterion 4. Task SCOPE resolution walks Position.ReportsToPositionId; APPROVER determination is
         * a different mechanism inside MOD-0023, which resolves through RuntimeAssignmentSnapshot —
         * TaskApprovalService passes only a candidate hint and says so in its own comment: "MOD-0024 never
         * decides authority". FU02 adds a reporting relationship and touches neither.
         *
         * MUTATION GUARD: add a `using` for TaskApprovalService anywhere under TenantOrganization and this
         * goes red with the file name.
         */
        var forbidden = new[]
        {
            "TaskAssignmentScopeResolver",
            "TaskApprovalService",
            "WorkflowTaskTransitionSupport",
            "RuntimeAssignmentSnapshot",
            "ApprovalManagerUserId"
        };

        var offenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Fu02Sources())
        {
            var text = File.ReadAllText(file);
            foreach (var token in forbidden.Where(t => text.Contains(t, StringComparison.Ordinal)))
            {
                offenders.Add($"{Path.GetFileName(file)}: references '{token}'");
            }
        }

        Assert.True(offenders.Count == 0,
            "MOD-0288-FU02 §16.4 — a reporting line must not reach the approval or task-scope path:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void No_organization_code_takes_a_compile_dependency_on_the_tasks_feature()
    {
        // §7 and §10: the TaskFieldDefinition mechanics are ADAPTED, never imported. A shared collection or a
        // shared rules class would make Tasks an owner of organization semantics.
        var offenders = Fu02Sources()
            .Where(f => File.ReadAllText(f).Contains("Features.Tasks", StringComparison.Ordinal)
                     || File.ReadAllText(f).Contains("Entities.Tasks", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Cast<string>()
            // The manifest legitimately declares every profile's collections, Tasks included.
            .Where(f => !f.StartsWith("PlatformSchemaManifest", StringComparison.Ordinal))
            .ToList();

        Assert.True(offenders.Count == 0,
            "organization code must not depend on the Tasks feature:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Adding_changing_or_clearing_a_reporting_line_leaves_task_routing_byte_identical()
    {
        /*
         * §16 criterion 4, stated as behaviour rather than as a name check.
         *
         * ⚠ WHAT THIS CAN AND CANNOT PROVE. Approval routing reads a POSITION's ReportsToPositionId and its
         * assignments; it never reads an OrganizationUnit's parent on either line. So the honest way to show
         * "byte-identical" is to show that the inputs routing consumes are untouched by every reporting-line
         * mutation — which is exactly what is asserted here, field by field, rather than by re-running a
         * routing engine that would silently pass if it never read the field in the first place.
         */
        var before = new OrganizationUnit
        {
            TenantId = Guid.NewGuid(),
            Code = "QC",
            Name = "Quality Control",
            LegalEntityId = Guid.NewGuid(),
            ManagerPositionId = Guid.NewGuid()
        };

        var snapshot = RoutingRelevantState(before);

        before.AdministrativeParentOrganizationUnitId = Guid.NewGuid();   // added
        Assert.Equal(snapshot, RoutingRelevantState(before));

        before.AdministrativeParentOrganizationUnitId = Guid.NewGuid();   // changed
        Assert.Equal(snapshot, RoutingRelevantState(before));

        before.AdministrativeParentOrganizationUnitId = null;             // cleared
        Assert.Equal(snapshot, RoutingRelevantState(before));

        // And the DTO the API returns carries the line WITHOUT renaming or displacing anything that existed.
        var dto = TenantOrganizationMapper.ToDto(before);
        Assert.Equal(before.ManagerPositionId, dto.ManagerPositionId);
        Assert.Equal(before.ParentOrganizationUnitId, dto.ParentOrganizationUnitId);
    }

    [Fact]
    public void The_dto_never_offers_an_unqualified_parent_or_manager_alongside_two_lines()
    {
        /*
         * §21.1 — two lines produce two different managers for the same employee, and a single unqualified
         * "Manager" label is a defect once both exist. The contract-level form of that rule: every parent
         * property names its line, and the pre-FU02 name stays as it is for backward compatibility.
         */
        var names = typeof(OrganizationUnitDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("ParentOrganizationUnitId", names);
        Assert.Contains("AdministrativeParentOrganizationUnitId", names);

        // Nothing called just "Parent" or just "Manager".
        Assert.DoesNotContain("Parent", names);
        Assert.DoesNotContain("Manager", names);
        Assert.DoesNotContain("ManagerId", names);
    }

    [Fact]
    public void The_reporting_line_permission_is_separate_from_the_ordinary_update_permission()
    {
        // §14 — a permission that lets someone rename a unit must not silently let them re-parent it. The
        // command's default is the enforcement, so the default is what is pinned.
        var command = new UpdateOrganizationUnitCommand(
            Guid.NewGuid(),
            new OrganizationUnitRequest("C", "N", Guid.NewGuid(), null));

        Assert.False(command.AllowReportingLineChange);
    }

    [Fact]
    public void The_guard_depth_is_the_one_number_the_rest_of_the_codebase_uses()
        => Assert.Equal(32, OrganizationUnitCycleGuard.MaxDepth);

    [Fact]
    public void No_organization_field_type_permits_a_free_form_or_executable_value()
    {
        // §4 — no free-form JSON, no expression, no executable/script type, no arbitrary reference target.
        var names = Enum.GetNames<OrganizationFieldDataType>();
        foreach (var banned in new[] { "Json", "Expression", "Script", "Formula", "Code", "Object" })
        {
            Assert.DoesNotContain(banned, names);
        }

        // And the constraint object carries no regex — the one "declarative" constraint that is a program.
        Assert.DoesNotContain(
            typeof(OrganizationFieldConstraints).GetProperties(),
            p => p.Name.Contains("Pattern", StringComparison.OrdinalIgnoreCase)
              || p.Name.Contains("Regex", StringComparison.OrdinalIgnoreCase)
              || p.Name.Contains("Expression", StringComparison.OrdinalIgnoreCase));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Everything task assignment and approval actually read off an Organization Unit.</summary>
    private static string RoutingRelevantState(OrganizationUnit unit) =>
        string.Join("|", unit.Id, unit.TenantId, unit.LegalEntityId, unit.ManagerPositionId,
            unit.ParentOrganizationUnitId, unit.Status, unit.IsArchived, unit.IsDeleted);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
