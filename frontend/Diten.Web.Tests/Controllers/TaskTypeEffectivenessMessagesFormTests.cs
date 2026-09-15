using System.Reflection;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskTypes;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-DM-DCP005-KURAL4-UI-01 (Kural 4 v2, sahip 2026-09-15, Blueprint/SAP/Oracle kıyasıyla doğrulandı —
/// WP-CT-DECISION-BENCHMARK-01) — the task type screen's own reading of Kural 4 v2: create's success message
/// (never a ModelState error — create no longer refuses) and the two reason codes an ACTIVE type's document-
/// changing edit can still be refused with. Modelled on <see cref="TaskTypeConcurrencyFormTests"/>, the sibling
/// this WP copies: asserted against the REAL payload/message builders by reflection, and against the real resx
/// files on disk.
/// </summary>
public sealed class TaskTypeEffectivenessMessagesFormTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static string InvokeBuildCreateSuccessMessage(CreateTaskTypeResultApiModel? data)
    {
        var method = typeof(TaskTypesController).GetMethod(
            "BuildCreateSuccessMessage", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, [data, "RECORD_CREATED", "UNVERIFIED", "BLOCKED_DOCS"])!;
    }

    [Fact]
    public void A_null_result_ie_the_wire_shape_before_this_WP_still_says_record_created()
    {
        Assert.Equal("RECORD_CREATED", InvokeBuildCreateSuccessMessage(null));
    }

    [Fact]
    public void An_active_result_says_record_created_regardless_of_anything_else_it_carries()
    {
        var data = new CreateTaskTypeResultApiModel
        {
            Id = Guid.NewGuid(), IsActive = true,
            BlockingDocuments = ["should be ignored"], EffectivenessUnavailable = true // contradictory on purpose — IsActive wins
        };

        Assert.Equal("RECORD_CREATED", InvokeBuildCreateSuccessMessage(data));
    }

    [Fact]
    public void An_inactive_result_with_blocking_documents_names_them_after_the_sentence()
    {
        var data = new CreateTaskTypeResultApiModel
        {
            Id = Guid.NewGuid(), IsActive = false,
            BlockingDocuments = ["UID-0000104: Blocked (Draft)", "UID-0000118: Unresolved"],
            EffectivenessUnavailable = false
        };

        var message = InvokeBuildCreateSuccessMessage(data);

        Assert.StartsWith("BLOCKED_DOCS", message);
        Assert.Contains("UID-0000104: Blocked (Draft)", message);
        Assert.Contains("UID-0000118: Unresolved", message);
    }

    [Fact]
    public void An_inactive_result_with_NO_blocking_documents_listed_still_says_the_sentence_alone()
    {
        var data = new CreateTaskTypeResultApiModel { Id = Guid.NewGuid(), IsActive = false, BlockingDocuments = [], EffectivenessUnavailable = false };

        Assert.Equal("BLOCKED_DOCS", InvokeBuildCreateSuccessMessage(data));
    }

    [Fact]
    public void An_inactive_result_because_the_register_was_unreachable_says_UNVERIFIED_never_the_blocked_sentence()
    {
        var data = new CreateTaskTypeResultApiModel
        {
            Id = Guid.NewGuid(), IsActive = false,
            BlockingDocuments = [], EffectivenessUnavailable = true
        };

        Assert.Equal("UNVERIFIED", InvokeBuildCreateSuccessMessage(data));
    }

    [Fact]
    public void The_two_active_type_reason_codes_map_to_their_own_resx_messages()
    {
        var map = (IReadOnlyDictionary<string, string>)typeof(TaskTypesController)
            .GetField("ReasonCodeMessages", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Equal("ErrorTaskTypeActiveDocumentBlocked", map["task_type_enable_blocked_documents"]);
        Assert.Equal("ErrorTaskTypeEnableRegisterUnavailable", map["task_type_enable_register_unavailable"]);
        Assert.Contains("ErrorTaskTypeActiveDocumentBlocked", Resx("en").Keys);
        Assert.Contains("ErrorTaskTypeEnableRegisterUnavailable", Resx("en").Keys);
    }

    /// <summary>
    /// The SAME wire code (task_type_enable_blocked_documents) means a DIFFERENT thing depending on which
    /// action asked — "cannot activate" (index.js's own client-side mapping) vs. "cannot attach to an active
    /// type" (this controller's, for the edit-time refusal). The server-side dictionary must map to the
    /// EDIT-TIME sentence, never accidentally to the activation one.
    /// </summary>
    [Fact]
    public void The_server_side_blocked_documents_message_is_the_EDIT_TIME_sentence_not_the_activation_one()
    {
        var editTime = Resx("en")["ErrorTaskTypeActiveDocumentBlocked"];
        var activation = Resx("en")["ErrorTaskTypeEnableBlockedDocuments"];

        Assert.NotEqual(editTime, activation);
        Assert.Contains("active task type", editTime, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void All_five_Kural4_keys_exist_with_REAL_translations_in_every_language()
    {
        var keys = new[]
        {
            "InfoTaskTypeSavedInactiveBlockedDocuments",
            "InfoTaskTypeSavedInactiveUnverified",
            "ErrorTaskTypeEnableBlockedDocuments",
            "ErrorTaskTypeEnableRegisterUnavailable",
            "ErrorTaskTypeActiveDocumentBlocked"
        };
        var english = Resx("en");

        foreach (var language in Languages)
        {
            var values = Resx(language);
            foreach (var key in keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{language}:{key} missing");
                if (language != "en")
                {
                    Assert.True(value != english[key], $"{language}:{key} is still the English text");
                }
            }
        }
    }

    [Fact]
    public void The_Turkish_sentences_match_the_prompts_own_pinned_wording()
    {
        var tr = Resx("tr");
        Assert.Equal(
            "Görev türü pasif olarak kaydedildi: bağlı belgelerin hepsi yürürlükte değil.",
            tr["InfoTaskTypeSavedInactiveBlockedDocuments"]);
        Assert.Equal(
            "Belge durumu doğrulanamadı; tür pasif kaydedildi. Daha sonra aktif etmeyi deneyin.",
            tr["InfoTaskTypeSavedInactiveUnverified"]);
        Assert.Equal(
            "Şu belgeler yürürlükte olmadığı için tür aktif edilemez:",
            tr["ErrorTaskTypeEnableBlockedDocuments"]);
        Assert.Equal(
            "Belge durumu doğrulanamadı, daha sonra tekrar deneyin.",
            tr["ErrorTaskTypeEnableRegisterUnavailable"]);
        Assert.Equal(
            "Aktif bir türe yürürlükte olmayan belge bağlanamaz. Önce türü pasife alın ya da belgenin yürürlüğe girmesini bekleyin.",
            tr["ErrorTaskTypeActiveDocumentBlocked"]);
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
