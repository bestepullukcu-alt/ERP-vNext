using System.Diagnostics;
using System.Text.Json;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 — the provider's own output, run through the <b>REAL</b> <c>validateWorkItem</c> from
/// <c>fixture-contract.js</c>.
///
/// <para><b>The defect that made this necessary.</b> <c>Dependencies</c> entered the projection in BL-028; the
/// matching <c>dependencies</c> capability was never added — <c>git log -S</c> finds no commit that ever added it.
/// The contract's CAPABILITY_REQUIRED_FOR_DATA rejects a container whose capability is undeclared, and
/// <c>validateItems</c> does not repair a rejected item, it <b>DROPS</b> it. So from the day dependencies existed,
/// every task that had one was invisible in the Task Center — title, actions and all. Two were being dropped in
/// production when this was measured, and 2000-odd green tests had nothing to say about it.</para>
///
/// <para><b>Why the contract is executed rather than restated.</b> Every C# assertion about the contract is a
/// COPY of it, and a copy is exactly what was already wrong: the provider "knew" the rule and still shipped a
/// half. This runs the actual JavaScript the browser runs, over the actual JSON the server serializes, in a Node
/// process. If the contract changes and the provider does not, this fails — which no C# mirror can promise.</para>
///
/// <para><b>Why the item carries data.</b> An empty task satisfies every conditional rule vacuously: no
/// container, no capability, nothing to disagree. The guard below runs on ONE item carrying a dependency, a
/// subtask, a checklist, a configurable value and a comment, so all five conditional pairs are live at once. The
/// vacuity test at the bottom proves that matters — the empty item passes the same guard even with a capability
/// deliberately removed.</para>
/// </summary>
public sealed class TaskWorkItemContractGuardTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    // ── the guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_item_carrying_EVERY_kind_of_data_passes_the_real_contract()
    {
        /*
         * RED before this ticket: `dependencies` container present, capability absent →
         * CAPABILITY_REQUIRED_FOR_DATA @ dependencies, and the Task Center drops the item.
         */
        var item = await ProjectDataCarryingItemAsync();

        var verdict = ValidateWithRealContract(item);

        Assert.True(verdict.Valid, $"The real contract rejected the provider's output: {verdict.Report}");
    }

    [Fact]
    public async Task The_data_carrying_item_really_does_carry_all_five_containers()
    {
        /*
         * Non-vacuity for the guard itself, and the assertion that keeps it honest as the fixture is edited: if a
         * future change quietly stops seeding a dependency, the guard above keeps passing while covering nothing.
         * That is precisely the shape of the original defect — a rule that was never exercised.
         */
        var item = await ProjectDataCarryingItemAsync();
        var element = JsonDocument.Parse(JsonSerializer.Serialize(item, WebOptions)).RootElement;

        var capabilities = element.GetProperty("workItemCapabilities")
            .EnumerateArray().Select(x => x.GetString()).ToList();

        foreach (var capability in new[] { "dependencies", "subtasks", "checklist", "businessContext", "activity" })
        {
            Assert.Contains(capability, capabilities);
        }

        Assert.NotEqual(JsonValueKind.Null, element.GetProperty("dependencies").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, element.GetProperty("subtasks").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, element.GetProperty("checklist").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, element.GetProperty("businessContext").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, element.GetProperty("activity").ValueKind);
    }

    [Fact]
    public async Task The_guard_CATCHES_a_missing_capability_which_is_what_makes_it_a_guard()
    {
        /*
         * The vacuity protection the ticket asks for, run as a test rather than claimed in a report: strip
         * `dependencies` from the capability list of a real projection and the real contract must reject it.
         *
         * If this passes-through, the guard above proves nothing — it would be green for the exact payload that
         * was disappearing from production for months.
         */
        var item = await ProjectDataCarryingItemAsync();
        var stripped = WithoutCapability(item, "dependencies");

        var verdict = ValidateWithRealContract(stripped);

        Assert.False(verdict.Valid, "Removing `dependencies` was accepted — the guard is vacuous.");
        Assert.Contains("CAPABILITY_REQUIRED_FOR_DATA", verdict.Report);
    }

    [Fact]
    public async Task An_EMPTY_item_passes_even_with_a_capability_removed_which_is_why_data_is_required()
    {
        /*
         * The measurement behind "test with a data-carrying item". A bare task has no dependency container, so
         * removing the capability breaks no pair and the contract is satisfied — a guard built on an empty item
         * would have been green throughout the entire lifetime of this defect.
         */
        var bare = await ProjectBareItemAsync();

        Assert.True(ValidateWithRealContract(bare).Valid);
        Assert.True(
            ValidateWithRealContract(WithoutCapability(bare, "dependencies")).Valid,
            "An empty item DID detect the removal — if this ever becomes true, say so rather than deleting the test.");
    }

    // ── the four conditional pairs, both halves, both directions ─────────────

    [Theory]
    [InlineData("dependencies")]
    [InlineData("checklist")]
    [InlineData("businessContext")]
    [InlineData("subtasks")]
    public async Task A_declared_capability_always_comes_with_its_container(string capability)
    {
        // The half the contract calls CAPABILITY_CONTAINER_REQUIRED.
        var element = JsonDocument.Parse(
            JsonSerializer.Serialize(await ProjectDataCarryingItemAsync(), WebOptions)).RootElement;

        Assert.Contains(
            capability,
            element.GetProperty("workItemCapabilities").EnumerateArray().Select(x => x.GetString()));
        Assert.NotEqual(JsonValueKind.Null, element.GetProperty(capability).ValueKind);
    }

    [Theory]
    [InlineData("dependencies")]
    [InlineData("checklist")]
    [InlineData("businessContext")]
    public async Task Without_the_data_NEITHER_half_appears(string capability)
    {
        /*
         * The other direction — "half is never right" cuts both ways. `subtasks` is deliberately absent from this
         * list: it follows the task's POSITION, not its data, so a childless parent still gets both halves. That
         * asymmetry is asserted in TaskWorkItemProviderTests; repeating it here would only re-state it.
         */
        var element = JsonDocument.Parse(
            JsonSerializer.Serialize(await ProjectBareItemAsync(), WebOptions)).RootElement;

        Assert.DoesNotContain(
            capability,
            element.GetProperty("workItemCapabilities").EnumerateArray().Select(x => x.GetString()));

        /*
         * ABSENT, not `null`. The serializer omits these properties entirely when they have no value, and the
         * contract treats the two differently everywhere it can (a display label must have `key === undefined`;
         * a serialized "key": null fails). Accepting either here would let a regression that starts writing
         * explicit nulls pass this test and then be dropped by validateItems in the browser.
         */
        Assert.False(
            element.TryGetProperty(capability, out var container) && container.ValueKind != JsonValueKind.Null,
            $"`{capability}` container was emitted without its capability — the contract drops the whole item.");
    }

    // ── running the real contract ────────────────────────────────────────────

    /// <summary>
    /// Pipes the serialized item into Node and runs <c>WorkCenterNextContract.validateWorkItem</c> on it.
    ///
    /// <para>The script is written to a temp file rather than passed with <c>-e</c> so quoting can never corrupt
    /// it, and the payload goes over STDIN rather than the command line so a large item cannot hit an argument
    /// limit. A missing Node is a FAILURE, not a skip: this is the only test that executes the contract, and a
    /// silent skip is how the defect survived in the first place.</para>
    /// </summary>
    private static (bool Valid, string Report) ValidateWithRealContract(object item)
    {
        var contractPath = ContractPath();
        var scriptPath = Path.Combine(Path.GetTempPath(), $"wcn-contract-{Guid.NewGuid():N}.js");

        File.WriteAllText(scriptPath, $$"""
            const fs = require('fs');
            require({{JsonSerializer.Serialize(contractPath)}});
            const payload = JSON.parse(fs.readFileSync(0, 'utf8'));
            const verdict = globalThis.WorkCenterNextContract.validateWorkItem(payload);
            process.stdout.write(JSON.stringify(verdict));
            """);

        try
        {
            var process = Process.Start(new ProcessStartInfo("node", $"\"{scriptPath}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new InvalidOperationException("Node could not be started.");

            process.StandardInput.Write(JsonSerializer.Serialize(item, WebOptions));
            process.StandardInput.Close();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);

            Assert.True(
                process.ExitCode == 0,
                $"Node failed to run the contract (exit {process.ExitCode}). stderr:\n{stderr}");

            var verdict = JsonDocument.Parse(stdout).RootElement;
            return (verdict.GetProperty("valid").GetBoolean(), stdout);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    /// <summary>Walks up to the repository root so this reads the SHIPPED contract, not a copy.</summary>
    private static string ContractPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(
            dir!.FullName, "frontend", "Diten.Web", "wwwroot", "assets", "js", "WorkCenterNext",
            "fixture-contract.js");
        Assert.True(File.Exists(path), $"fixture-contract.js not found at {path}");
        return path;
    }

    /// <summary>Round-trips through JSON so the removal happens on the WIRE shape the browser would receive.</summary>
    private static object WithoutCapability(object item, string capability)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(item, WebOptions), WebOptions)!;

        map["workItemCapabilities"] = JsonSerializer.SerializeToElement(
            map["workItemCapabilities"].EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => x != capability)
                .ToList());

        return map;
    }

    // ── fixtures ─────────────────────────────────────────────────────────────

    /// <summary>ONE item carrying a dependency, a subtask, a checklist, a configurable value and a comment.</summary>
    private static async Task<WorkItemProjectionDto> ProjectDataCarryingItemAsync()
    {
        var task = SelfTask("Bağımlılığı olan görev");
        task.FieldValues.Add(new TaskFieldValue
        {
            DefinitionCode = "regulatory.phase",
            ValueType = TaskFieldValueType.Text,
            Value = "Phase 1"
        });

        var predecessor = SelfTask("Önce biten görev");
        var child = SelfTask("Alt görev");
        child.ParentTaskItemId = task.Id;

        var provider = Provider(
            new FakeTaskItemRepository(task, predecessor, child),
            new FakeTaskDependencyRepository(new TaskDependency
            {
                TenantId = TaskTestData.Tenant,
                TaskItemId = task.Id,
                DependsOnTaskItemId = predecessor.Id,
                DependencyType = TaskDependencyType.FinishToStart
            }),
            new FakeChecklistRunRepository(new ChecklistRun
            {
                TenantId = TaskTestData.Tenant,
                TaskItemId = task.Id,
                Items =
                [
                    new ChecklistRunItem
                    {
                        Code = "step-1",
                        LabelText = "Belgeyi kontrol et",
                        Requirement = ChecklistItemRequirement.Optional,
                        SortOrder = 1
                    }
                ]
            }),
            new FakeTaskCommentRepository(new TaskComment
            {
                TenantId = TaskTestData.Tenant,
                TaskItemId = task.Id,
                Text = "İlk yorum",
                AuthorUserId = TaskTestData.Me,
                AuthorDisplayName = TaskTestData.MeDisplayName
            }),
            new FakeTaskFieldDefinitionRepository(new TaskFieldDefinition
            {
                TenantId = TaskTestData.Tenant,
                Code = "regulatory.phase",
                LabelText = "Faz",
                ValueType = TaskFieldValueType.Text,
                Section = "Düzenleyici",
                SortOrder = 1
            }));

        var items = await provider.GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()),
            CancellationToken.None);

        return items.Single(x => x.Id == task.Id.ToString());
    }

    /// <summary>A task with none of the optional data — the vacuous case, kept to prove it IS vacuous.</summary>
    private static async Task<WorkItemProjectionDto> ProjectBareItemAsync()
    {
        var task = SelfTask("Yalın görev");
        var items = await Provider(new FakeTaskItemRepository(task)).GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()),
            CancellationToken.None);

        return Assert.Single(items);
    }

    private static TaskWorkItemProvider Provider(
        FakeTaskItemRepository tasks,
        FakeTaskDependencyRepository? dependencies = null,
        FakeChecklistRunRepository? checklists = null,
        FakeTaskCommentRepository? comments = null,
        FakeTaskFieldDefinitionRepository? fieldDefinitions = null)
        => new(
            tasks,
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            checklists ?? new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            dependencies ?? new FakeTaskDependencyRepository(),
            comments ?? new FakeTaskCommentRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            fieldDefinitions ?? new FakeTaskFieldDefinitionRepository());

    private static TaskItem SelfTask(string title) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = title,
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Rival,
        OrganizationUnitId = Guid.NewGuid(),
        Version = 1
    };
}
