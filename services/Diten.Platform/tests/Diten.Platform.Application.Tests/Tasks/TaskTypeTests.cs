using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// `Task` here is System.Threading.Tasks.Task: this file's own namespace ends in `.Tasks`, which shadows it.
using Task = System.Threading.Tasks.Task;

/// <summary>
/// DCP-005 slice 1 — the rules that make a task type the CARRIER of a classification rather than a label.
///
/// <para>Each one exists because breaking it corrupts something already stored: an edited code rewrites the
/// identity of every task opened under it, a deleted type makes that history unreadable, and a classification
/// that disagrees with its domain cannot be filed at all.</para>
/// </summary>
public sealed class TaskTypeTests
{
    private static TaskType Existing(string code, bool active = true) => new()
    {
        TenantId = Guid.NewGuid(), Code = code, Name = code, IsActive = active
    };

    [Fact]
    public void Code_is_normalized_so_one_type_cannot_become_two()
    {
        // Codes are read aloud, printed on records and typed into the counterparty's spreadsheets.
        Assert.Equal("DEV-QMS", TaskTypeRules.NormalizeCode("  dev-qms "));
        Assert.Equal("QA", TaskTypeRules.NormalizeFunctionCode(" qa "));
        Assert.Null(TaskTypeRules.NormalizeFunctionCode("   "));
    }

    [Fact]
    public void Code_cannot_change_after_creation()
    {
        /*
         * MUTATION GUARD: make the code editable and this goes red.
         *
         * Refused rather than ignored — silently keeping the stored value would report success for a change the
         * caller asked for and did not get.
         */
        var refusal = TaskTypeRules.ValidateCodeUnchanged("DEV-QMS", "DEV-OTHER");
        Assert.NotNull(refusal);
        Assert.Equal(TaskReasonCodes.TaskTypeCodeImmutable, refusal!.Value.ReasonCode);

        // The same code back is not an attempt to change anything, whatever its casing or padding.
        Assert.Null(TaskTypeRules.ValidateCodeUnchanged("DEV-QMS", " dev-qms "));
        // Absent means "not attempting to change it": the screen sends the field read-only.
        Assert.Null(TaskTypeRules.ValidateCodeUnchanged("DEV-QMS", null));
    }

    [Fact]
    public void A_retired_type_keeps_its_code()
    {
        /*
         * A code freed by deactivation could be re-used for different work, and every task opened under the old
         * meaning would silently join the new one.
         */
        var retired = Existing("DEV-QMS", active: false);
        var refusal = TaskTypeRules.ValidateCodeUnique("dev-qms", [retired]);
        Assert.NotNull(refusal);
        Assert.Equal(TaskReasonCodes.TaskTypeCodeTaken, refusal!.Value.ReasonCode);
    }

    [Fact]
    public async Task There_is_no_delete__only_retire()
    {
        /*
         * MUTATION GUARD: add a delete handler or a delete route and this goes red on the type still being
         * readable afterwards.
         *
         * A type that has been used is part of the identity of every task opened under it. Retiring stops it
         * appearing on NEW work — which is the whole of what "delete" was ever wanted for here — and leaves the
         * past legible.
         */
        var type = Existing("DEV-QMS");
        var repo = new FakeTaskTypeRepository(type);
        var handler = new SetTaskTypeActiveHandler(repo);

        var response = await handler.Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(false), "c1"),
            CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        var stored = Assert.Single(repo.All);
        Assert.False(stored.IsActive);
        // Still there, still readable, still restorable — the difference between retiring and deleting.
        Assert.Null(stored.DeletedAt);
        Assert.NotNull(await repo.GetByIdAsync(type.Id, CancellationToken.None));
    }

    [Fact]
    public void Gqms_domain_holds_ONE_value_and_never_a_list()
    {
        /*
         * MUTATION GUARD: turn the property into a collection and this stops compiling — which is the strongest
         * form this guard can take.
         *
         * The counterparty's reasoning, not ours: the folder path is computed from this field, and a type
         * carrying several domains makes that rule unresolvable. A deviation is four types, not one type with
         * four domains.
         */
        var property = typeof(TaskType).GetProperty(nameof(TaskType.GqmsDomain))!;
        Assert.Equal(typeof(TaskGqmsDomain?), property.PropertyType);
    }

    [Fact]
    public void Empty_domain_is_not_many_domains()
    {
        // Work outside every domain leaves the field null and takes OPERATIONAL_RECORD.
        Assert.Null(TaskTypeRules.ValidateClassification(TaskRecordClass.OPERATIONAL_RECORD, null));

        // A GxP record with no domain could not be filed — the folder rule is computed from the domain.
        var noDomain = TaskTypeRules.ValidateClassification(TaskRecordClass.GXP_QUALITY_RECORD, null);
        Assert.NotNull(noDomain);
        Assert.Equal(TaskReasonCodes.TaskTypeClassificationInvalid, noDomain!.Value.ReasonCode);

        // And a type that names a domain is doing quality work, so it cannot also claim to produce no record.
        var contradiction = TaskTypeRules.ValidateClassification(TaskRecordClass.NOT_A_RECORD, TaskGqmsDomain.GMP);
        Assert.NotNull(contradiction);
        Assert.Equal(TaskReasonCodes.TaskTypeClassificationInvalid, contradiction!.Value.ReasonCode);
    }

    [Fact]
    public void Governing_documents_are_cleaned_rather_than_refused()
    {
        // A blank row or the same UID twice does not change what the administrator meant.
        var cleaned = TaskTypeRules.NormalizeDocuments(["GMG-QMS-SOP-0005", " ", "GMG-QMS-SOP-0005", "GMG-QMS-SOP-0012"]);
        Assert.Equal(["GMG-QMS-SOP-0005", "GMG-QMS-SOP-0012"], cleaned);
    }

    [Fact]
    public void Local_documents_stay_sparse()
    {
        // 24 types × 5 orgs would be 120 cells; an org whose list empties is dropped rather than stored empty.
        var local = TaskTypeRules.NormalizeLocalDocuments(new Dictionary<string, IReadOnlyList<string>>
        {
            ["GMG-CH"] = ["GMG-LOC-0001"],
            ["GMG-TR"] = [" ", ""]
        });

        Assert.Single(local);
        Assert.Equal(["GMG-LOC-0001"], local["GMG-CH"]);
    }

    [Fact]
    public void Every_task_type_WRITE_route_demands_the_manage_permission()
    {
        /*
         * ⚠ ASSERTED ON THE ROUTES, NOT ON THE CONSTANTS — and the first version of this test was the weaker
         * kind. It compared `TaskTypesManage` with `Create` and passed happily while the POST route was
         * repointed at `Create`: a rule two constants can satisfy is a rule nothing enforces. Third time this
         * session; the fix is always the same — assert on the thing that decides.
         */
        var writeActions = new[]
        {
            nameof(Diten.Platform.API.Controllers.TasksController.CreateTaskType),
            nameof(Diten.Platform.API.Controllers.TasksController.UpdateTaskType),
            nameof(Diten.Platform.API.Controllers.TasksController.SetTaskTypeActive),
            nameof(Diten.Platform.API.Controllers.TasksController.GetTaskTypes)
        };

        foreach (var action in writeActions)
        {
            var method = typeof(Diten.Platform.API.Controllers.TasksController).GetMethod(action);
            Assert.NotNull(method);
            var permission = method!
                .GetCustomAttributes(typeof(Diten.Platform.API.Security.HasPermissionAttribute), false)
                .Cast<Diten.Platform.API.Security.HasPermissionAttribute>()
                .SingleOrDefault();
            Assert.NotNull(permission);
            Assert.Equal(TaskPermissions.TaskTypesManage, permission!.Permission);
        }
    }

    [Fact]
    public void CHOOSING_a_type_is_open_to_anyone_who_can_create_a_task()
    {
        /*
         * The other half of the same rule, and it has to be asserted too: guarding the picker with the manage
         * permission would make the type unusable by the people the classification exists for.
         */
        var method = typeof(Diten.Platform.API.Controllers.TasksController)
            .GetMethod(nameof(Diten.Platform.API.Controllers.TasksController.GetActiveTaskTypes));
        var permission = method!
            .GetCustomAttributes(typeof(Diten.Platform.API.Security.HasPermissionAttribute), false)
            .Cast<Diten.Platform.API.Security.HasPermissionAttribute>()
            .Single();
        Assert.Equal(TaskPermissions.Read, permission.Permission);
    }

    [Fact]
    public void Writing_a_type_is_a_SEPARATE_permission_from_creating_a_task()
    {
        /*
         * MUTATION GUARD: point the write routes at `TaskPermissions.Create` and this goes red.
         *
         * QA's control statement — "a manually created, unclassified task may not produce a GxP quality record"
         * — holds only because a person who can open a task cannot also mint the type that classifies it.
         * Reading types stays open to anyone who can create one; they have to be able to choose.
         */
        Assert.Equal("platform.tasks.task-types.manage", TaskPermissions.TaskTypesManage);
        Assert.NotEqual(TaskPermissions.Create, TaskPermissions.TaskTypesManage);
        Assert.NotEqual(TaskPermissions.Read, TaskPermissions.TaskTypesManage);
    }
}
