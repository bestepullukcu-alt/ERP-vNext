using System.Reflection;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskFieldDefinitions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-PSS-MOD0024-CLOSURE-ENVELOPE-2A-01 — the field-definition form's <c>Stage</c> picker.
///
/// <para>Asserted against the REAL payload builders by reflection, and against the real resx files on disk —
/// the same pattern <see cref="TaskTypeClosureOutcomeEditorTests"/> and
/// <c>TaskTypeReviewMeetingRequirementFormTests</c> already established for this controller's siblings.</para>
/// </summary>
public sealed class TaskFieldDefinitionStageFormTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static object? PayloadValue(TaskFieldDefinitionEditViewModel model, string builder)
    {
        var method = typeof(TaskFieldDefinitionsController).GetMethod(builder, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var payload = method!.Invoke(null, [model])!;
        var property = payload.GetType().GetProperty("stage");
        Assert.NotNull(property);
        return property!.GetValue(payload);
    }

    [Theory]
    [InlineData("ToCreatePayload")]
    [InlineData("ToUpdatePayload")]
    public void The_selected_stage_travels_unchanged(string builder)
    {
        var model = new TaskFieldDefinitionEditViewModel { Code = "closure.note", Section = "s", Stage = "Closure" };

        Assert.Equal("Closure", PayloadValue(model, builder));
    }

    [Theory]
    [InlineData("ToCreatePayload")]
    [InlineData("ToUpdatePayload")]
    public void A_form_that_never_touches_the_picker_still_sends_Entry__the_view_model_s_own_default(string builder)
    {
        var model = new TaskFieldDefinitionEditViewModel { Code = "entry.one", Section = "s" };

        Assert.Equal("Entry", PayloadValue(model, builder));
    }

    [Fact]
    public void All_seven_resx_files_carry_the_identical_Stage_key_set()
    {
        var english = Resx("en").Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var language in Languages)
        {
            Assert.True(english.SetEquals(Resx(language).Keys), $"{language} key set differs from en");
        }
    }

    [Fact]
    public void The_label_hint_and_two_options_are_REAL_translations_in_every_language()
    {
        string[] keys = ["Stage", "StageHint", "StageEntry", "StageClosure"];
        var english = Resx("en");

        foreach (var language in Languages)
        {
            var values = Resx(language);
            foreach (var key in keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}:{key} missing");
                Assert.NotEqual(key, value);
                if (language != "en")
                {
                    Assert.True(value != english[key], $"{language}:{key} is still the English text");
                }
            }
        }
    }

    private static Dictionary<string, string> Resx(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Tasks", "FieldDefinitions",
            $"TaskFieldDefinitionsIndex.{language}.resx");
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

/// <summary>
/// The THREE new closure-block strings in <c>WorkCenterNextIndex.*.resx</c> — checked on their own, never as a
/// full-file key-set parity (that resx is large and shared; a pre-existing mismatch anywhere else in it is not
/// this slice's to fix or to hide a real one behind).
/// </summary>
public sealed class WorkCenterNextClosureBlockL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] Keys = ["ClosureSectionTitle", "ClosureDeliverableCount", "ClosureEvidenceCount"];

    [Fact]
    public void The_three_closure_block_keys_are_REAL_translations_in_every_language()
    {
        var english = Resx("en");

        foreach (var language in Languages)
        {
            var values = Resx(language);
            foreach (var key in Keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}:{key} missing");
                Assert.NotEqual(key, value);
                if (language != "en")
                {
                    Assert.True(value != english[key], $"{language}:{key} is still the English text");
                }
            }
        }
    }

    [Fact]
    public void The_two_count_sentences_carry_the_0_placeholder_app_js_fills_with_the_actual_count()
    {
        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.Contains("{0}", values["ClosureDeliverableCount"]);
            Assert.Contains("{0}", values["ClosureEvidenceCount"]);
        }
    }

    private static Dictionary<string, string> Resx(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "WorkCenterNext",
            $"WorkCenterNextIndex.{language}.resx");
        return XDocument.Load(path).Root!
            .Elements("data")
            .Where(e => Keys.Contains((string)e.Attribute("name")!))
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
