using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Models.Lskus;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX1 B1 / B3 / B5 — the refusals a maker can act on reach the screen as their own sentence
/// (a duplicate market, a submit the engine refused before starting it, an approval that could not be applied), and
/// every sentence key is in all seven languages. Other refusals keep the status sentence (S5, FIX2).
/// </summary>
public sealed class LskuRefusalSentenceTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string[] Keys =
    [
        "ErrorMarketAlreadyExists", "ErrorSubmitAbandoned", "ErrorDecisionNotApplicable", "ErrorSubmitTargetChanged",
        // S5b-FIX3: the submit-during-withdrawal sentence.
        "ErrorSubmitDuringWithdrawal",
        // MOD-0290-FU04 S3b FIX1 (R5): the stock-flag gate's sentence, its flag labels and the action list's reason.
        "ErrorStockFlagsRequired", "StockFlagSerialControlled", "StockFlagGskuLotControlled", "SubmitNeedsStockFlags"
    ];

    [Fact]
    public async Task A_duplicate_market_is_said_in_its_own_sentence()
    {
        var controller = Controller(Conflict("LSKU_MARKET_ALREADY_EXISTS"));
        var token = FormToken(controller);

        var result = await controller.Create(new CreateLskuViewModel { GskuId = Guid.NewGuid(), MarketCode = "TR" },
            token, CancellationToken.None);

        Assert.Equal("ErrorMarketAlreadyExists", FirstError(result, 409));
    }

    [Fact]
    public async Task An_abandoned_submit_is_said_in_its_own_sentence_and_the_reason_is_not_shown_raw()
    {
        var controller = Controller(Conflict("LSKU_IDENTITY_SUBMIT_ABANDONED", "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH"));
        controller.ControllerContext.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
        controller.ControllerContext.HttpContext.Request.Form = new FormCollection(new()
        {
            ["ExpectedVersion"] = "0",
            ["__RequestVerificationToken"] = "x"
        });

        var result = await controller.SubmitLifecycle(Guid.NewGuid(), 0, CancellationToken.None);

        Assert.Equal("ErrorSubmitAbandoned", FirstError(result, 409));
    }

    // FIX2 item 12: a submit that ended because its DRAFT changed is not "the approval service refused" — its own sentence.
    [Fact]
    public async Task A_submit_whose_draft_changed_is_said_in_its_own_sentence()
    {
        var controller = Controller(Conflict("LSKU_IDENTITY_SUBMIT_TARGET_CHANGED"));
        controller.ControllerContext.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
        controller.ControllerContext.HttpContext.Request.Form = new FormCollection(new()
        {
            ["ExpectedVersion"] = "0",
            ["__RequestVerificationToken"] = "x"
        });

        var result = await controller.SubmitLifecycle(Guid.NewGuid(), 0, CancellationToken.None);

        Assert.Equal("ErrorSubmitTargetChanged", FirstError(result, 409));
    }

    // S5b-FIX3: a submit refused while its withdrawal is under way (LSKU_IDENTITY_WITHDRAWAL_UNDER_WAY) is finished by
    // RE-SENDING THE WITHDRAWAL — its own sentence (en pinned), in all seven languages; never the status sentence.
    [Fact]
    public async Task A_submit_during_withdrawal_is_said_in_its_own_sentence_telling_the_user_to_resend_the_withdrawal()
    {
        var controller = Controller(Conflict("LSKU_IDENTITY_WITHDRAWAL_UNDER_WAY"));
        controller.ControllerContext.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
        controller.ControllerContext.HttpContext.Request.Form = new FormCollection(new()
        {
            ["ExpectedVersion"] = "0",
            ["__RequestVerificationToken"] = "x"
        });

        var result = await controller.SubmitLifecycle(Guid.NewGuid(), 0, CancellationToken.None);

        Assert.Equal("ErrorSubmitDuringWithdrawal", FirstError(result, 409));
        var english = ResxValues("en")["ErrorSubmitDuringWithdrawal"];
        Assert.Contains("re-send the withdrawal", english, StringComparison.Ordinal);
        Assert.Equal("A withdrawal of this submit is under way; re-send the withdrawal to finish it before submitting again.", english);
        foreach (var language in Languages)
        {
            Assert.True(ResxValues(language).TryGetValue("ErrorSubmitDuringWithdrawal", out var text)
                && !string.IsNullOrWhiteSpace(text), $"ErrorSubmitDuringWithdrawal is missing in {language}");
        }
    }

    [Fact]
    public async Task Any_other_conflict_keeps_the_status_sentence()
    {
        var controller = Controller(Conflict("LSKU_IDENTITY_TOMBSTONED"));
        var token = FormToken(controller);

        var result = await controller.Create(new CreateLskuViewModel { GskuId = Guid.NewGuid(), MarketCode = "TR" },
            token, CancellationToken.None);

        Assert.Equal("ErrorConflict", FirstError(result, 409));
    }

    [Fact]
    public void Every_refusal_sentence_is_in_all_seven_languages()
    {
        foreach (var language in Languages)
        {
            var values = ResxValues(language);
            foreach (var key in Keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"{key} is missing in {language}");
            }
        }
    }

    private static Dictionary<string, string> ResxValues(string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                "Lskus", $"LskusIndex.{language}.resx")).Root!.Elements("data")
            .ToDictionary(x => x.Attribute("name")!.Value, x => x.Element("value")?.Value ?? string.Empty);

    private static string FirstError(IActionResult result, int status)
    {
        var body = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, body.StatusCode);
        var json = JsonSerializer.SerializeToElement(body.Value);
        return json.GetProperty("errors")[0].GetString()!;
    }

    private static string FormToken(LskusController controller)
    {
        controller.Index();
        return Assert.IsType<string>(controller.ViewData["LskuFormAttemptToken"]);
    }

    private static HttpResponseMessage Conflict(params string[] errors) => new(HttpStatusCode.Conflict)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { isSuccessful = false, statusCode = 409, errors }),
            Encoding.UTF8, "application/json")
    };

    private static LskusController Controller(HttpResponseMessage answer)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.lsku.test" }).Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("permission", "mdm.lskus.read"),
                new Claim("permission", "mdm.lskus.create"),
                new Claim("permission", "mdm.lskus.submit")
            ], "fixture"))
        };
        return new LskusController(new HttpClient(new Answer(answer)), gateway, new EphemeralDataProtectionProvider(),
            new EchoLocalizer(), NullLogger<LskusController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }

    private sealed class Answer(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(response);
    }

    private sealed class EchoLocalizer : IStringLocalizer<LskusIndex>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
