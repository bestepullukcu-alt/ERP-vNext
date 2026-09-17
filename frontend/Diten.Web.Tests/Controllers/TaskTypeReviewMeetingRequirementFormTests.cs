using System.Reflection;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskTypes;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-PSS-MOD0024-REVIEW-MEETING-POLICY-01 — the task type form's review meeting requirement.
///
/// <para>Asserted against the REAL payload builders by reflection, and against the real resx files on disk.</para>
/// </summary>
public sealed class TaskTypeReviewMeetingRequirementFormTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] Options = ["NotAllowed", "Optional", "Required"];

    private static object? PayloadValue(TaskTypeEditViewModel model, string builder)
    {
        var method = typeof(TaskTypesController).GetMethod(builder, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var payload = method!.Invoke(null, [model])!;
        var property = payload.GetType().GetProperty("reviewMeetingRequirement");
        Assert.NotNull(property);
        return property!.GetValue(payload);
    }

    [Theory]
    [InlineData("ToCreatePayload")]
    [InlineData("ToUpdatePayload")]
    public void The_selected_code_travels_unchanged(string builder)
    {
        var model = new TaskTypeEditViewModel { Code = "REV", Name = "Review", ReviewMeetingRequirement = "Required" };

        Assert.Equal("Required", PayloadValue(model, builder));
    }

    [Theory]
    [InlineData("ToCreatePayload")]
    [InlineData("ToUpdatePayload")]
    public void Nothing_posted_sends_NULL__the_server_keeps_what_it_has(string builder)
    {
        // A default of "Optional" here would reset a Required type from any post that did not carry the select.
        var model = new TaskTypeEditViewModel { Code = "REV", Name = "Review" };

        Assert.Null(PayloadValue(model, builder));
    }

    [Fact]
    public void The_refusal_code_maps_to_a_resx_message()
    {
        var map = (IReadOnlyDictionary<string, string>)typeof(TaskTypesController)
            .GetField("ReasonCodeMessages", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Equal("ErrorReviewMeetingRequirementInvalid", map["TASK_TYPE_REVIEW_MEETING_REQUIREMENT_INVALID"]);
        Assert.Contains("ErrorReviewMeetingRequirementInvalid", Resx("en").Keys);
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
    public void The_label_hint_and_three_options_are_REAL_translations_in_every_language()
    {
        var keys = new[] { "ReviewMeetingRequirement", "ReviewMeetingRequirementHint", "ErrorReviewMeetingRequirementInvalid" }
            .Concat(Options.Select(option => "ReviewMeetingRequirement" + option))
            .ToArray();
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
                    // An English word copied into another language's file is a missing translation, not a translation.
                    Assert.True(value != english[key], $"{language}:{key} is still the English text");
                }
            }
        }
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
