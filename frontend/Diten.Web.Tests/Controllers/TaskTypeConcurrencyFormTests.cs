using System.Reflection;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskTypes;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-PSS-MOD0024-TASK-TYPE-CONCURRENCY-01 (BL-375) — the task type EDIT form's optimistic-concurrency guard.
///
/// <para>Modelled on <see cref="TaskTypeReviewMeetingRequirementFormTests"/>, the sibling this WP was told to
/// copy: asserted against the REAL payload builder by reflection, and against the real resx files on disk.</para>
/// </summary>
public sealed class TaskTypeConcurrencyFormTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static object? PayloadValue(TaskTypeEditViewModel model, string property)
    {
        var method = typeof(TaskTypesController).GetMethod("ToUpdatePayload", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var payload = method!.Invoke(null, [model])!;
        var prop = payload.GetType().GetProperty(property);
        Assert.NotNull(prop);
        return prop!.GetValue(payload);
    }

    [Fact]
    public void The_hydrated_version_travels_to_the_update_payload_unchanged()
    {
        var model = new TaskTypeEditViewModel { Code = "CNC", Name = "Concurrency", Version = 7 };

        Assert.Equal(7, PayloadValue(model, "expectedVersion"));
    }

    [Fact]
    public void Create_has_no_version_to_send__the_type_does_not_exist_yet()
    {
        var method = typeof(TaskTypesController).GetMethod("ToCreatePayload", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var model = new TaskTypeEditViewModel { Code = "CNC", Name = "Concurrency" };
        var payload = method!.Invoke(null, [model])!;

        Assert.Null(payload.GetType().GetProperty("expectedVersion"));
    }

    [Fact]
    public void The_form_carries_the_version_as_a_hidden_field()
    {
        var form = File.ReadAllText(Path.Combine(
            RepoRoot(), "frontend", "Diten.Web", "Views", "Tasks", "TaskTypes", "_Form.cshtml"));

        Assert.Contains("asp-for=\"Version\"", form);
    }

    [Fact]
    public void The_refusal_code_maps_to_a_resx_message__the_existing_TaskFieldDefinition_code_reused()
    {
        // YAPMA: no new reason code — TaskReasonCodes.ConcurrencyConflict already exists and every other
        // MOD-0024 edit uses it (ChecklistHandlers, TaskFieldDefinitionHandlers). This is the WEB layer's own
        // mapping of that SAME wire value to a screen-specific sentence.
        var map = (IReadOnlyDictionary<string, string>)typeof(TaskTypesController)
            .GetField("ReasonCodeMessages", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Equal("ErrorConcurrencyConflict", map["TASK_CONCURRENCY_CONFLICT"]);
        Assert.Contains("ErrorConcurrencyConflict", Resx("en").Keys);
    }

    [Fact]
    public void All_seven_resx_files_carry_the_identical_key_set()
    {
        var english = Resx("en").Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var language in Languages)
        {
            Assert.True(english.SetEquals(Resx(language).Keys), $"{language} key set differs from en");
        }
    }

    [Fact]
    public void The_concurrency_message_is_a_REAL_translation_in_every_language()
    {
        var english = Resx("en");

        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.True(
                values.TryGetValue("ErrorConcurrencyConflict", out var value) && !string.IsNullOrWhiteSpace(value),
                $"{language}:ErrorConcurrencyConflict missing");
            Assert.NotEqual("ErrorConcurrencyConflict", value);
            if (language != "en")
            {
                // An English sentence copied into another language's file is a missing translation, not one.
                Assert.True(value != english["ErrorConcurrencyConflict"], $"{language}:ErrorConcurrencyConflict is still the English text");
            }
        }
    }

    [Fact]
    public void The_Turkish_message_names_the_reader_s_own_recovery__reload_and_retry()
    {
        // The exact sentence the prompt gave, pinned so a later edit cannot soften it into something vaguer.
        Assert.Equal(
            "Bu görev tipi siz düzenlerken başka biri tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin.",
            Resx("tr")["ErrorConcurrencyConflict"]);
    }

    private static Dictionary<string, string> Resx(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Tasks", "TaskTypes",
            $"TaskTypesIndex.{language}.resx");
        return XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
