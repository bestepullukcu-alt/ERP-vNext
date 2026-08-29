using Diten.Platform.Application.Features.Tasks.SelfRegistration;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// FIX-TASKS-MODULE-NAME — the module was renamed "Görevler / Tasks" → "Görev Tanımları / Task Settings" because
// every page it publishes to the sidebar is a definition screen (the work surfaces are nav-invisible on purpose),
// and because "Görevler" sat next to "Görev Merkezi" with nothing to tell a user which one held their work.
//
// The catalog's DisplayName is a SOFT (operator-owned) field that manifest re-pushes never overwrite, so the
// rename needs a migration to reach an already-registered environment. These tests pin the three things that can
// silently undo it: the manifest string itself, the migration's agreement with that string, and the rule that
// stops the migration from clobbering a name an operator typed.
public sealed class TaskModuleDisplayNameRenameMigrationTests
{
    private static ModuleCatalogItem Row(string moduleCode, string displayName) =>
        new() { Id = Guid.NewGuid(), ModuleCode = moduleCode, DisplayName = displayName };

    [Fact]
    public void The_manifest_declares_the_settings_name_not_the_work_list_name()
    {
        var manifest = new TaskManifestProvider().GetManifest();

        Assert.Equal(TaskModuleDisplayNameRenameMigration.NewDisplayName, manifest.DisplayName);
        Assert.NotEqual(TaskModuleDisplayNameRenameMigration.OldDisplayName, manifest.DisplayName);
    }

    [Fact]
    public void An_untouched_old_seed_is_renamed()
    {
        var row = Row("tasks", TaskModuleDisplayNameRenameMigration.OldDisplayName);

        var renames = TaskModuleDisplayNameRenameMigration.Plan([row], out var skipped);

        Assert.Equal(row.Id, Assert.Single(renames).ItemId);
        Assert.Empty(skipped);
    }

    [Fact]
    public void A_second_run_plans_nothing_so_the_migration_is_idempotent()
    {
        var row = Row("tasks", TaskModuleDisplayNameRenameMigration.NewDisplayName);

        var renames = TaskModuleDisplayNameRenameMigration.Plan([row], out var skipped);

        Assert.Empty(renames);
        Assert.Empty(skipped); // already the new name → not a rename AND not a warning
    }

    [Fact]
    public void An_operator_rename_is_left_alone_and_reported()
    {
        // DisplayName is SOFT: this value belongs to whoever typed it, and overwriting it would be the migration
        // taking a field the registration handler deliberately refuses to take.
        var row = Row("tasks", "Görev Ayarları (bizim adımız)");

        var renames = TaskModuleDisplayNameRenameMigration.Plan([row], out var skipped);

        Assert.Empty(renames);
        Assert.Equal("Görev Ayarları (bizim adımız)", Assert.Single(skipped));
    }

    [Fact]
    public void Other_modules_holding_the_same_text_are_never_touched()
    {
        // The filter is the module code, not the string — otherwise any module that happened to carry the old
        // text would be dragged along by a migration that has no business renaming it.
        var renames = TaskModuleDisplayNameRenameMigration.Plan(
            [Row("work-aggregation", TaskModuleDisplayNameRenameMigration.OldDisplayName)],
            out var skipped);

        Assert.Empty(renames);
        Assert.Empty(skipped);
    }
}
