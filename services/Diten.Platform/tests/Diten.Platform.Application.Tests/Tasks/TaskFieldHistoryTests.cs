using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// "Who changed the due date?" — the question that had no answer anywhere before 2026-08-23.
///
/// <para>The mechanism was already half built: the write uses <c>FindOneAndReplace</c> +
/// <c>ReturnDocument.Before</c>, so the previous document is in hand for free. What was missing was the diff.</para>
///
/// <para>Two rules shape everything here, and both come from the objection the old code raised against field
/// logging — an objection that was answered rather than dismissed:</para>
/// <list type="number">
///   <item><b>ONE entry per SAVE</b>, listing the fields that moved. Five rows for one act would bury the six
///   entries that tell the task's story.</item>
///   <item><b>A CLOSED set of fields.</b> Counters, version stamps and internal state are not history.</item>
/// </list>
/// </summary>
public sealed class TaskFieldHistoryTests
{
    // ── The diff itself ──────────────────────────────────────────────────────

    [Fact]
    public void A_moved_due_date_is_recorded_with_both_ends()
    {
        var before = Task(t => t.DueAt = new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.FromHours(3)));
        var after = Task(t => t.DueAt = new DateTimeOffset(2026, 8, 20, 17, 0, 0, TimeSpan.FromHours(3)));

        var change = Assert.Single(TaskFieldDiff.Between(before, after));

        Assert.Equal(TaskFieldChangeCodes.DueAt, change.Field);
        // A DATE, not an instant: "from the 15th to the 20th" is the change a reader means.
        Assert.Equal("2026-08-15", change.From);
        Assert.Equal("2026-08-20", change.To);
        Assert.False(change.ValuesOmitted);
    }

    /// <summary>
    /// MUTATION TARGET (one record per save). Five fields changed together is ONE act — that is how the person
    /// who did it remembers it, and it is what keeps the feed readable.
    /// </summary>
    [Fact]
    public void Five_fields_changed_in_one_save_are_ONE_record_listing_five()
    {
        var before = Task();
        var after = Task(t =>
        {
            t.Title = "Yeni başlık";
            t.DueAt = new DateTimeOffset(2026, 9, 1, 17, 0, 0, TimeSpan.FromHours(3));
            t.Priority = TaskPriority.High;
            t.EstimateHours = 8m;
            t.Tags = ["mali", "acil"];
        });

        var changes = TaskFieldDiff.Between(before, after);

        Assert.Equal(5, changes.Count);
        Assert.Equal(
            new[]
            {
                TaskFieldChangeCodes.Title, TaskFieldChangeCodes.DueAt, TaskFieldChangeCodes.Priority,
                TaskFieldChangeCodes.EstimateHours, TaskFieldChangeCodes.Tags
            }.Order(),
            changes.Select(c => c.Field).Order());
    }

    /// <summary>
    /// MUTATION TARGET (the closed set). Reflecting over the entity would sweep in the version stamp, the
    /// timestamps, the spent-hours counter and the reminder claim key — a history row each, none of them
    /// anybody's question.
    /// </summary>
    [Theory]
    [InlineData("version")]
    [InlineData("updatedAt")]
    [InlineData("spentHours")]
    public void Bookkeeping_is_never_recorded(string _)
    {
        var before = Task();
        var after = Task(t =>
        {
            t.Version = 99;
            t.UpdatedAt = DateTimeOffset.UtcNow;
            t.SpentHours = 12m;
            t.LastDueSoonReminderKey = "2026-09-01";
        });

        Assert.Empty(TaskFieldDiff.Between(before, after));
    }

    [Fact]
    public void Every_recorded_code_is_one_the_vocabulary_declares()
    {
        // The guard that keeps the differ and the vocabulary from drifting: a field added to one and not the
        // other would produce a code the screen has no sentence for.
        var before = Task();
        var after = Task(t =>
        {
            t.Title = "x"; t.Description = "y"; t.Priority = TaskPriority.Low;
            t.DueAt = DateTimeOffset.UtcNow; t.StartAt = DateTimeOffset.UtcNow;
            t.PlannedDate = DateTimeOffset.UtcNow; t.EstimateHours = 1m; t.Tags = ["z"];
            t.AssigneeUserId = TaskTestData.Other;
            t.FieldValues = [Value("regulatory.phase", "II")];
        });

        var changes = TaskFieldDiff.Between(before, after);

        Assert.Equal(10, changes.Count);
        Assert.All(changes, c => Assert.Contains(c.Field, TaskFieldChangeCodes.All));
    }

    [Fact]
    public void A_long_value_is_recorded_as_CHANGED_without_its_two_versions()
    {
        var before = Task(t => t.Description = new string('a', 200));
        var after = Task(t => t.Description = new string('b', 200));

        var change = Assert.Single(TaskFieldDiff.Between(before, after));

        Assert.True(change.ValuesOmitted, "a 200-character before/after pair was kept in the log");
        Assert.Null(change.From);
        Assert.Null(change.To);
    }

    [Fact]
    public void A_short_description_keeps_its_two_versions()
    {
        // Non-vacuity: omitting ALWAYS would pass the test above and lose every readable value.
        var before = Task(t => t.Description = "kısa");
        var after = Task(t => t.Description = "daha kısa");

        var change = Assert.Single(TaskFieldDiff.Between(before, after));

        Assert.False(change.ValuesOmitted);
        Assert.Equal("kısa", change.From);
    }

    [Fact]
    public void Reordering_the_same_tags_is_not_a_change()
    {
        var before = Task(t => t.Tags = ["a", "b"]);
        var after = Task(t => t.Tags = ["b", "a"]);

        Assert.Empty(TaskFieldDiff.Between(before, after));
    }

    [Fact]
    public void A_configurable_value_carries_its_definition_code()
    {
        var before = Task(t => t.FieldValues = [Value("regulatory.phase", "I")]);
        var after = Task(t => t.FieldValues = [Value("regulatory.phase", "II")]);

        var change = Assert.Single(TaskFieldDiff.Between(before, after));

        Assert.Equal(TaskFieldChangeCodes.CustomField, change.Field);
        // The code is what lets the READ path find the definition and apply BL-024.
        Assert.Equal("regulatory.phase", change.DefinitionCode);
    }

    // ── The read path: a hidden field's history is hidden too ────────────────

    /// <summary>
    /// MUTATION TARGET (the back door). BL-024 hides a field's VALUE from a caller without its view permission.
    /// A history that reported "changed from 45.000 to 52.000" would hand the same number back through a
    /// different door — and nobody would call it a permission bug, because the permission still works.
    ///
    /// <para>⚠ The NAME goes too. "Salary band" tells you the task carries salary data even with the numbers
    /// removed.</para>
    /// </summary>
    [Fact]
    public async Task A_reader_without_the_field_permission_sees_neither_the_values_nor_the_name()
    {
        var fixture = new Fixture();
        await fixture.EditConfigurableFieldAsync("45000", "52000");

        var change = Assert.Single(await fixture.ProjectFieldChangesAsync(mayViewRestrictedField: false));

        Assert.True(change.Redacted);
        Assert.Null(change.From);
        Assert.Null(change.To);
        Assert.Null(change.Field);
        Assert.Null(change.Label);
    }

    [Fact]
    public async Task A_reader_WITH_the_permission_sees_both_ends()
    {
        // Non-vacuity for the rule above: redacting everybody would pass it and make the feature useless.
        var fixture = new Fixture();
        await fixture.EditConfigurableFieldAsync("45000", "52000");

        var change = Assert.Single(await fixture.ProjectFieldChangesAsync(mayViewRestrictedField: true));

        Assert.False(change.Redacted);
        Assert.Equal("45000", change.From);
        Assert.Equal("52000", change.To);
    }

    /// <summary>
    /// The ROW survives redaction. Dropping it would give two readers of one task two different histories — and
    /// the entry's actor and timestamp are not secret, so "somebody edited something" is already public.
    /// </summary>
    [Fact]
    public async Task A_redacted_change_still_leaves_a_row_in_the_feed()
    {
        var fixture = new Fixture();
        await fixture.EditConfigurableFieldAsync("45000", "52000");

        var entries = await fixture.ProjectActivityAsync(mayViewRestrictedField: false);

        var edited = Assert.Single(entries.Where(e => e.Event?.Code == "edited"));
        Assert.Single(edited.Event!.FieldChanges!);
        Assert.NotNull(edited.Actor ?? "");   // the actor and instant stay whatever they were
    }

    /// <summary>
    /// ⚠ FOUND LIVE, not by a test: the field-history rows rendered "İsim bulunamadı" as the actor while the
    /// `created` row beside them named a person. The repository writes history at the COMMIT and holds no user
    /// context — the actor has always had to be DECLARED, and the edit handler never declared anything because
    /// before field logging it produced no entry at all.
    ///
    /// <para>"Who changed the due date" is the whole question; an entry that cannot name a person answers half
    /// of it.</para>
    /// </summary>
    [Fact]
    public async Task An_edit_records_WHO_made_it()
    {
        var fixture = new Fixture();

        await fixture.EditDueDateThroughTheHandlerAsync();

        var entry = Assert.Single(fixture.Transitions.Events.Where(e => e.Kind == TaskTransitionKind.Edited));
        Assert.Equal(TaskTestData.Me, entry.ActorUserId);
    }

    /// <summary>
    /// And a save that changed nothing recorded still writes nothing — the edit handler declares an intent on
    /// EVERY save, so without this rule pressing "Save" on an untouched form would add a row saying so.
    /// </summary>
    [Fact]
    public async Task Saving_an_untouched_form_records_nothing()
    {
        /*
         * ⚠ ON A TASK WITH NO CONFIGURABLE VALUES, and the reason is a real behaviour of the update handler
         * rather than a convenience: `UpdateTaskItemRequest` is a FULL REPLACE, so a save that sends no
         * `fieldValues` genuinely clears them. The first version of this test used the fixture's salary-band
         * task and failed correctly — the "unchanged" save HAD changed something, and the diff said so.
         */
        var fixture = new Fixture(withConfigurableField: false);

        await fixture.SaveUnchangedThroughTheHandlerAsync();

        Assert.Empty(fixture.Transitions.Events.Where(e => e.Kind == TaskTransitionKind.Edited));
    }

    [Fact]
    public async Task An_unrestricted_field_is_never_redacted()
    {
        var fixture = new Fixture();
        await fixture.EditDueDateAsync();

        var change = Assert.Single(await fixture.ProjectFieldChangesAsync(mayViewRestrictedField: false));

        Assert.False(change.Redacted);
        Assert.Equal(TaskFieldChangeCodes.DueAt, change.Field);
    }

    private const string RestrictedCode = "hr.salaryband";
    private const string RestrictedPermission = "platform.tasks.fields.salary.view";

    private static TaskFieldValue Value(string code, string value) => new()
    {
        DefinitionCode = code, ValueType = TaskFieldValueType.Text, Value = value
    };

    private static TaskItem Task(Action<TaskItem>? mutate = null)
    {
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant,
            Title = "CT probe",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me,
            CreatedByUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.InProgress,
            Version = 1
        };
        mutate?.Invoke(task);
        return task;
    }

    private sealed class Fixture
    {
        private readonly TaskItem _task;
        private readonly FakeTaskItemRepository _tasks;
        private readonly TaskFieldDefinition _restricted = new()
        {
            TenantId = TaskTestData.Tenant,
            Code = RestrictedCode,
            LabelText = "Maaş bandı",
            ValueType = TaskFieldValueType.Text,
            Section = "İK",
            ViewPermission = RestrictedPermission
        };

        public Fixture(bool withConfigurableField = true)
        {
            _task = withConfigurableField
                ? Task(t => t.FieldValues = [Value(RestrictedCode, "45000")])
                : Task();
            _tasks = new FakeTaskItemRepository(_task);
        }

        public async System.Threading.Tasks.Task EditConfigurableFieldAsync(string from, string to)
        {
            var stored = await _tasks.GetByIdAsync(_task.Id);
            Assert.Equal(from, stored!.FieldValues.Single().Value);
            stored.FieldValues = [Value(RestrictedCode, to)];
            Assert.True(await _tasks.UpdateAsync(stored, stored.Version));
        }

        public FakeTaskTransitionRepository Transitions => _tasks.Transitions;

        /// <summary>Through the REAL handler, because the actor is declared there and nowhere else.</summary>
        public System.Threading.Tasks.Task EditDueDateThroughTheHandlerAsync()
            => UpdateAsync(dueAt: new DateTimeOffset(2026, 9, 9, 17, 0, 0, TimeSpan.FromHours(3)));

        public System.Threading.Tasks.Task SaveUnchangedThroughTheHandlerAsync()
            => UpdateAsync(dueAt: _task.DueAt);

        private async System.Threading.Tasks.Task UpdateAsync(DateTimeOffset? dueAt)
        {
            var stored = await _tasks.GetByIdAsync(_task.Id);
            var handler = new Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers
                .UpdateTaskItemHandler(
                    _tasks,
                    new FakeOrganizationUnitRepository(),
                    new TaskFieldDefinitionService(
                        new FakeTaskFieldDefinitionRepository(_restricted),
                        TaskRecordSourceDoubles.None,
                        TaskActors.PermitAll()),
                    new FakeCurrentUserContext(TaskTestData.Me),
                    new FakeTaskApprovalService(),
                    new FakeTaskReviewService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<
                        Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers
                            .UpdateTaskItemHandler>.Instance);

            var response = await handler.Handle(
                new Diten.Platform.Application.Features.Tasks.Commands.UpdateTaskItemCommand(
                    _task.Id,
                    new Diten.Platform.Application.Features.Tasks.UpdateTaskItemRequest(
                        Title: stored!.Title,
                        Description: stored.Description,
                        Priority: stored.Priority,
                        OrganizationUnitId: null,
                        DueAt: dueAt,
                        StartAt: stored.StartAt,
                        PlannedDate: stored.PlannedDate,
                        EstimateHours: stored.EstimateHours,
                        Tags: stored.Tags,
                        ReviewRequired: stored.ReviewRequired,
                        EmailNotificationsEnabled: stored.EmailNotificationsEnabled,
                        DelegationAllowed: stored.DelegationAllowed,
                        FieldValues: null,
                        ExpectedVersion: stored.Version),
                    "corr"),
                CancellationToken.None);

            Assert.Equal(204, response.StatusCode);
        }

        public async System.Threading.Tasks.Task EditDueDateAsync()
        {
            var stored = await _tasks.GetByIdAsync(_task.Id);
            stored!.DueAt = new DateTimeOffset(2026, 9, 5, 17, 0, 0, TimeSpan.FromHours(3));
            Assert.True(await _tasks.UpdateAsync(stored, stored.Version));
        }

        public async Task<IReadOnlyList<WorkItemFieldChangeDto>> ProjectFieldChangesAsync(
            bool mayViewRestrictedField)
        {
            var entries = await ProjectActivityAsync(mayViewRestrictedField);
            return Assert.Single(entries.Where(e => e.Event?.Code == "edited")).Event!.FieldChanges!;
        }

        public async Task<IReadOnlyList<WorkItemActivityEntryDto>> ProjectActivityAsync(
            bool mayViewRestrictedField)
        {
            /*
             * The actor's permissions are the ONLY thing that differs between the two readers — same task, same
             * log, same definition. Anything else varying would make the assertion about something other than
             * the rule under test.
             */
            var permissions = mayViewRestrictedField
                ? TaskActors.Holding(RestrictedPermission)
                : TaskActors.None();

            var provider = new TaskWorkItemProvider(
                _tasks,
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver((TaskTestData.Me, TaskTestData.MeDisplayName)),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                new FakeTaskCommentRepository(),
                _tasks.Transitions,
                new FakeTaskPersonalOverlayRepository(),
                new FakeTaskWatcherRepository(),
                permissions,
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(),
                SlaForTests.Real(),
                new FakeTaskFieldDefinitionRepository(_restricted), new FakeTaskTypeRepository());

            var items = await provider.GetWorkItemsAsync(
                new WorkItemActor(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>()),
                CancellationToken.None);
            return Assert.Single(items.Where(i => i.Id == _task.Id.ToString())).Activity!;
        }
    }
}
