using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// GSKU FIX1 (B2 a, B7, B4, A1): a refusal the maker can act on reaches the screen as a sentence in the reader's
/// language. Only the listed MDM codes are translated; any other MDM answer stays a status message, and the raw code
/// never reaches the browser. The real controller action is called; MDM is a recording handler.
/// </summary>
public sealed class GskuKnownFailureSentenceTests
{
    [Theory]
    [InlineData("GSKU_PARENT_NOT_APPROVED", "ErrorParentNotApproved")]
    [InlineData("GSKU_IDENTITY_SUBMIT_IN_FLIGHT", "ErrorSubmitInFlight")]
    [InlineData("GSKU_DEPENDENT_IDENTITIES_EXIST", "ErrorDependentIdentitiesExist")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_APPROVAL_CHANGING", "ErrorApprovalChanging")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY", "ErrorApprovalTemplateMissing")]
    [InlineData("GSKU_IDENTITY_RECONCILIATION_REQUIRED", "ErrorReconciliationRequired")]
    [InlineData("FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", "ErrorReconciliationRequired")]
    [InlineData("FIRST_GSKU_IDENTITY_SUBMISSION_ENDED", "ErrorSubmissionEnded")]
    [InlineData("FIRST_GSKU_IDENTITY_WORKFLOW_DRAFT_CHANGED", "ErrorDraftChanged")]
    public async Task A_known_refusal_is_said_in_the_readers_language_and_the_code_stays_behind(string code, string key)
    {
        var result = await SubmitAgainstAsync(HttpStatusCode.Conflict, Envelope(code));

        var (status, errors) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal($"localized:{key}", Assert.Single(errors));
        Assert.DoesNotContain(code, JsonSerializer.Serialize(((ObjectResult)result).Value));
    }

    // FIX3 item 4: a code with no sentence is a missing mapping — the status message is shown, the code stays behind,
    // and the browser is told (unmapped) so that its console warns.
    [Fact]
    public async Task An_unknown_code_stays_a_status_message_and_is_flagged()
    {
        var result = await SubmitAgainstAsync(HttpStatusCode.Conflict, Envelope("SOME_INTERNAL_DETAIL"));

        var (status, errors) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.DoesNotContain("SOME_INTERNAL_DETAIL", Assert.Single(errors));
        Assert.DoesNotContain("ErrorParentNotApproved", errors[0]);
        Assert.True(Unmapped(result));
    }

    // FIX3 item 4 (B18): FIX1/FIX2 translated only a single known code on a 409; every status and every code is now said.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "GSKU_CORRECTION_REQUEST_INVALID", "refusal:RefusalInvalidRequest")]
    [InlineData(HttpStatusCode.Forbidden, "GSKU_DIRECT_RETIREMENT_SYSTEM_ONLY", "refusal:RefusalForbidden")]
    [InlineData(HttpStatusCode.NotFound, "GSKU_NOT_FOUND", "refusal:RefusalNotFound")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE", "refusal:RefusalServiceUnavailable")]
    [InlineData(HttpStatusCode.Conflict, "GSKU_CORRECTION_RETRY_LIMIT_REACHED", "refusal:RefusalReconciliation")]
    [InlineData(HttpStatusCode.BadRequest, "GSKU_PARENT_NOT_APPROVED", "localized:ErrorParentNotApproved")]
    public async Task Every_status_and_every_mapped_code_is_said_as_a_sentence(HttpStatusCode mdmStatus, string code, string sentence)
    {
        var result = await SubmitAgainstAsync(mdmStatus, Envelope(code));

        var (status, errors) = Read(result);
        Assert.Equal((int)mdmStatus, status);
        Assert.Equal(sentence, Assert.Single(errors));
        Assert.False(Unmapped(result));
        Assert.DoesNotContain(code, JsonSerializer.Serialize(((ObjectResult)result).Value));
    }

    [Fact]
    public async Task Several_codes_are_each_said_once_and_an_unknown_one_adds_the_status_message()
    {
        var result = await SubmitAgainstAsync(HttpStatusCode.BadRequest,
            "{\"errors\":[\"PACK_UOM_INVALID\",\"PACK_QUANTITY_PRECISION_INVALID\",\"OTHER_CODE\"]}");

        var (_, errors) = Read(result);
        Assert.Equal(2, errors.Length);
        Assert.Equal("refusal:RefusalPackInvalid", errors[0]);
        Assert.DoesNotContain("OTHER_CODE", errors[1]);
        Assert.True(Unmapped(result));
    }

    [Fact]
    public async Task An_unreadable_answer_stays_a_status_message()
    {
        var result = await SubmitAgainstAsync(HttpStatusCode.Conflict, "not json");

        var (_, errors) = Read(result);
        Assert.NotEqual("localized:ErrorParentNotApproved", Assert.Single(errors));
        Assert.False(Unmapped(result));
    }

    private static bool Unmapped(IActionResult result)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(((ObjectResult)result).Value));
        return document.RootElement.TryGetProperty("unmapped", out var flag) && flag.GetBoolean();
    }

    // FIX2 item 8 (replaces FIX1's "repeat once with that command", which MDM always refused: the repeat carried
    // this request's reason and comment under the earlier command, and MDM keeps what the first request said). An
    // earlier withdrawal with another text is still under way: the requester is told so in words, nothing is resent.
    [Fact]
    public async Task A_withdrawal_pending_under_another_command_is_said_in_words_and_not_resent()
    {
        var wire = new ScriptedHandler((HttpStatusCode.Conflict,
            $"{{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"FIRST_GSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT\"],\"data\":{{\"pendingWithdrawalCommandId\":\"{Guid.NewGuid():D}\"}}}}"));
        var controller = Controller(wire, "mdm.gskus.withdraw", new()
        {
            ["ExpectedGskuVersion"] = "3", ["ReasonCode"] = "MAKER_CORRECTION", ["Comment"] = "",
            ["__RequestVerificationToken"] = "synthetic-csrf"
        });

        var result = await controller.WithdrawIdentityApproval(Guid.NewGuid(), 3, "MAKER_CORRECTION", "", CancellationToken.None);

        var (status, errors) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("localized:ErrorWithdrawalPending", Assert.Single(errors));
        Assert.Single(wire.Keys);
    }

    // The same on the Global Product screen (GP's withdrawal command is derived from the version alone, so this is
    // rare there — but a raw 409 is never what the requester sees).
    [Fact]
    public async Task A_global_product_withdrawal_pending_under_another_command_is_said_in_words_and_not_resent()
    {
        var wire = new ScriptedHandler((HttpStatusCode.Conflict,
            $"{{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"PRODUCT_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT\"],\"data\":{{\"pendingWithdrawalCommandId\":\"{Guid.NewGuid():D}\"}}}}"));
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        var controller = new GlobalProductsController(new HttpClient(wire), gateway,
            new PrefixLocalizer<Diten.Web.SharedResource>(), new PrefixLocalizer<Diten.Web.Views.MasterDataManagement.GlobalProducts.GlobalProductsIndex>(),
            NullLogger<GlobalProductsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = Context("mdm.global-products.withdraw",
                new() { ["ExpectedVersion"] = "3", ["__RequestVerificationToken"] = "synthetic-csrf" }) }
        };

        var result = await controller.WithdrawApproval(Guid.NewGuid(), 3, CancellationToken.None);

        var (status, errors) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("localized:ErrorWithdrawalPending", Assert.Single(errors));
        Assert.Single(wire.Keys);
    }

    // FIX3 item 6: Global Product's in-flight rule refuses an edit while a submit is on its way, and a submit whose
    // draft changed under it — both said in words on the Global Product screen.
    [Theory]
    [InlineData("GLOBAL_PRODUCT_IDENTITY_SUBMIT_IN_FLIGHT", "ErrorSubmitInFlight")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_DRAFT_CHANGED", "ErrorDraftChanged")]
    // BL-552 flow 3 (S2): a draft held by a submit stuck in manual reconciliation — the in-flight sentence.
    // BL-552 flow 3 FIX1 item 13: a STUCK submit has its own sentence (the same person submits again; else an
    // administrator); a submit whose start is not yet confirmed is on its way.
    [InlineData("GLOBAL_PRODUCT_IDENTITY_RECONCILIATION_REQUIRED", "ErrorSubmitStuck")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_START_LOOKUP_REFUSED", "ErrorSubmitInFlight")]
    public async Task A_global_product_in_flight_refusal_is_said_in_words(string code, string key)
    {
        var wire = new ScriptedHandler((HttpStatusCode.Conflict, Envelope(code)));
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        var controller = new GlobalProductsController(new HttpClient(wire), gateway,
            new PrefixLocalizer<Diten.Web.SharedResource>(), new PrefixLocalizer<Diten.Web.Views.MasterDataManagement.GlobalProducts.GlobalProductsIndex>(),
            NullLogger<GlobalProductsController>.Instance)
        {
            // MOD-0290-FU05 (T-4, CT decision): the draft-edit form always carries BrandId ("" = no brand).
            ControllerContext = new ControllerContext { HttpContext = Context("mdm.global-products.update",
                new() { ["ExpectedVersion"] = "3", ["GlobalProductName"] = "Renamed", ["BrandId"] = "", ["__RequestVerificationToken"] = "synthetic-csrf" }) }
        };

        var result = await controller.UpdateDraft(Guid.NewGuid(), 3, "Renamed", CancellationToken.None);

        var (status, errors) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal($"localized:{key}", Assert.Single(errors));
        Assert.Single(wire.Keys);
    }

    // BL-552 flow 3 (S2): the SUBMIT of a draft held by a stuck submit is refused in the same words (the screen's
    // submit button goes through this proxy too).
    [Theory]
    [InlineData("GLOBAL_PRODUCT_IDENTITY_RECONCILIATION_REQUIRED", "ErrorSubmitStuck")]
    [InlineData("PRODUCT_IDENTITY_WORKFLOW_START_LOOKUP_REFUSED", "ErrorSubmitInFlight")]
    public async Task A_global_product_submit_held_by_a_stuck_submit_is_said_in_words(string code, string key)
    {
        var wire = new ScriptedHandler((HttpStatusCode.Conflict, Envelope(code)));
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        var controller = new GlobalProductsController(new HttpClient(wire), gateway,
            new PrefixLocalizer<Diten.Web.SharedResource>(), new PrefixLocalizer<Diten.Web.Views.MasterDataManagement.GlobalProducts.GlobalProductsIndex>(),
            NullLogger<GlobalProductsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = Context("mdm.global-products.submit",
                new() { ["ExpectedVersion"] = "3", ["__RequestVerificationToken"] = "synthetic-csrf" }) }
        };

        var result = await controller.SubmitLifecycle(Guid.NewGuid(), 3, CancellationToken.None);

        var (status, errors) = Read(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("localized:" + key, Assert.Single(errors));
    }

    private static string Envelope(string code) =>
        $"{{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"{code}\"]}}";

    private static (int Status, string[] Errors) Read(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToArray();
        return (objectResult.StatusCode!.Value, errors);
    }

    private static async Task<IActionResult> SubmitAgainstAsync(HttpStatusCode mdmStatus, string body)
    {
        var controller = Controller(new ScriptedHandler((mdmStatus, body)), "mdm.gskus.submit", new()
        {
            ["ExpectedVersion"] = "3",
            ["__RequestVerificationToken"] = "synthetic-csrf"
        });
        return await controller.SubmitLifecycle(Guid.NewGuid(), 3, CancellationToken.None);
    }

    private static GskusController Controller(ScriptedHandler wire, string permission, Dictionary<string, StringValues> form)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        return new GskusController(new HttpClient(wire), gateway,
            new EphemeralDataProtectionProvider(), new PrefixLocalizer<Diten.Web.SharedResource>(),
            new PrefixLocalizer<GskusIndex>(), NullLogger<GskusController>.Instance, new PrefixLocalizer<GskuRefusals>("refusal"))
        { ControllerContext = new ControllerContext { HttpContext = Context(permission, form) } };
    }

    private static DefaultHttpContext Context(string permission, Dictionary<string, StringValues> form)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", Guid.NewGuid().ToString("D")),
                new Claim("permission", permission)
            ], "fixture"));
        var context = new DefaultHttpContext { User = principal };
        // Synthetic fixture credential: only seen by the scripted handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-gsku-known-failure";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(form);
        return context;
    }

    private sealed class ScriptedHandler(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        public List<string> Keys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : "");
            var (status, body) = answers[Math.Min(Keys.Count - 1, answers.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class PrefixLocalizer<T>(string prefix = "localized") : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, $"{prefix}:{name}");
        public LocalizedString this[string name, params object[] arguments] => new(name, $"{prefix}:{name}");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
