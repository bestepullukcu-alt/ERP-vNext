using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AccountIndex = Diten.Web.Views.CRM.Accounts.AccountIndex;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-CRM-MOBILE-REQ CRM-ACC-EDIT — the Web account form: the edit carries the version it read, an untouched logo is not
/// sent (removed ⇒ removeLogo), a 409 version conflict shows the reload notice (7 languages), a coded 409 / 400 lands under
/// its field; form.js marks a picked / removed logo as changed (RunNode).
/// </summary>
public sealed class AccountEditWebTests
{
    private const string Tenant = "97c50000-0000-0000-0000-000000000001";
    private static readonly Guid Id = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    private static AccountEditViewModel Model(int? version = 4, string? logo = "data:image/png;base64,AAAA", bool changed = false) => new()
    {
        Id = Id, AccountName = "Eczane Şifa", AccountType = "pharmacy", Status = "active", LogoDataUri = logo, Version = version, LogoChanged = changed
    };

    [Fact]
    public void W1_the_edit_body_carries_the_version_and_sends_the_logo_only_when_the_user_changed_it()
    {
        var untouched = AccountsController.ToUpdatePayload(Model());
        var replaced = AccountsController.ToUpdatePayload(Model(logo: "data:image/png;base64,BBBB", changed: true));
        var removed = AccountsController.ToUpdatePayload(Model(logo: "", changed: true));

        Assert.Equal((4, (string?)null, false), (untouched.ExpectedVersion!.Value, untouched.LogoDataUri, untouched.RemoveLogo));
        Assert.Equal(("data:image/png;base64,BBBB", false), (replaced.LogoDataUri, replaced.RemoveLogo));
        Assert.Equal(((string?)null, true), (removed.LogoDataUri, removed.RemoveLogo));
        var json = JsonSerializer.Serialize(untouched, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"expectedVersion\":4", json);
        Assert.DoesNotContain("removeLogo", json);                                  // false is not sent
    }

    [Fact]
    public async Task W2_a_version_conflict_answers_the_form_again_with_the_reload_notice_and_no_summary_copy()
    {
        var gateway = new Gateway(HttpStatusCode.Conflict, """{"errors":["changed by someone else","account_version_conflict"],"statusCode":409,"isSuccessful":false}""");
        var controller = Controller(gateway);

        var result = Assert.IsType<ViewResult>(await controller.Edit(Id, Model()));

        var model = Assert.IsType<AccountEditViewModel>(result.Model);
        Assert.True(model.VersionConflict);
        Assert.Equal(0, controller.ModelState.ErrorCount);
        var put = Assert.Single(gateway.Requests, r => r.Method == "PUT");
        Assert.EndsWith($"/api/crm/accounts/{Id}", put.Uri);
        using var body = JsonDocument.Parse(put.Body!);
        Assert.Equal(4, body.RootElement.GetProperty("expectedVersion").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("logoDataUri", out var logo) && logo.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task W3_coded_conflicts_and_field_errors_land_under_their_fields_on_create_and_edit()
    {
        var create = Controller(new Gateway(HttpStatusCode.Conflict, """{"errors":["AccountCode already exists","account_code_taken"],"statusCode":409}"""));
        var edit = Controller(new Gateway(HttpStatusCode.BadRequest,
            """{"errors":["'garage' is not valid","account_type_invalid","email bad","account_email_invalid","free text"],"statusCode":400}"""));

        await create.Create(Model(version: null));
        await edit.Edit(Id, Model());

        Assert.Equal("Error.account_code_taken", Assert.Single(create.ModelState["AccountCode"]!.Errors).ErrorMessage);
        Assert.Equal("Error.account_type_invalid", Assert.Single(edit.ModelState["AccountType"]!.Errors).ErrorMessage);
        Assert.Equal("Error.account_email_invalid", Assert.Single(edit.ModelState["ResponsiblePersonEmail"]!.Errors).ErrorMessage);
        Assert.Equal("free text", Assert.Single(edit.ModelState[string.Empty]!.Errors).ErrorMessage);   // uncoded → summary
    }

    [Fact]
    public void W4_the_parser_pairs_messages_with_codes_and_maps_each_code_to_its_field()
    {
        var problems = AccountGatewayErrors.Parse(["a", "external_reference_taken", "b", "account_logo_too_large", "plain message"]);

        Assert.Equal(new[] { ("ExternalReference", "external_reference_taken"), ("LogoDataUri", "account_logo_too_large"), ((string?)null, (string?)null) }
            .Select(x => (x.Item1, x.Item2)), problems.Select(p => (p.Field, p.Code)));
        Assert.Equal("AccountCode", AccountGatewayErrors.FieldOf("account_code_taken"));
        Assert.Null(AccountGatewayErrors.FieldOf("account_version_conflict"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public void W5_the_conflict_notice_and_every_coded_message_exist_in_all_seven_languages(string lang)
    {
        var doc = XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "Accounts", $"AccountIndex.{lang}.resx"));
        var values = doc.Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
        var english = XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "Accounts", "AccountIndex.en.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

        foreach (var key in new[] { "Error.account_version_conflict", "VersionConflictReload", "Error.account_code_taken", "Error.external_reference_taken",
                     "Error.account_type_invalid", "Error.account_status_invalid", "Error.account_category_invalid", "Error.account_reference_set_missing",
                     "Error.account_logo_invalid", "Error.account_logo_too_large", "Error.account_logo_conflict", "Error.account_expected_version_required" })
        {
            Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text), $"{lang}: {key}");
            if (lang != "en")
            {
                Assert.NotEqual(english[key], text);
            }
        }
    }

    [Fact]
    public void W6_the_form_renders_the_version_the_logo_change_flag_and_the_reload_notice()
    {
        var form = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "Accounts", "_Form.cshtml"));

        Assert.Contains("asp-for=\"Version\"", form);
        Assert.Contains("id=\"accountLogoChanged\"", form);
        Assert.Contains("Localizer[\"Error.account_version_conflict\"]", form);
        Assert.Contains("Localizer[\"VersionConflictReload\"]", form);
        foreach (var field in new[] { "AccountCode", "ExternalReference", "AccountCategory", "LogoDataUri", "ResponsiblePersonPhone" })
        {
            Assert.Contains($"asp-validation-for=\"{field}\"", form);
        }
    }

    [Fact]
    public void W7_form_js_marks_the_logo_changed_only_when_a_file_is_picked_or_the_logo_removed()
    {
        var script = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "Accounts", "form.js").Replace("\\", "/");
        var output = RunNode($$"""
            const listeners = {};
            const el = (id, extra) => Object.assign({ id, value: '', classList: { add() {}, remove() {} }, getAttribute: () => 'x', textContent: '', files: null,
              addEventListener(type, fn) { listeners[id + ':' + type] = fn; } }, extra || {});
            const nodes = { accountLogoBox: el('accountLogoBox'), accountLogoFile: el('accountLogoFile'), accountLogoData: el('accountLogoData', { value: 'data:old' }),
              accountLogoPreview: el('accountLogoPreview'), accountLogoPlaceholder: el('accountLogoPlaceholder'), accountLogoRemove: el('accountLogoRemove'),
              accountLogoError: el('accountLogoError'), accountLogoChanged: el('accountLogoChanged', { value: 'false' }) };
            global.document = { getElementById: id => nodes[id] || null, addEventListener(type, fn) { if (type === 'DOMContentLoaded') global.ready = fn; } };
            global.$ = () => ({ length: 0 });
            global.FileReader = function () { this.readAsDataURL = () => { this.result = 'data:image/png;base64,NEW'; this.onload(); }; };
            require('{{script}}');
            global.ready();
            const out = { start: nodes.accountLogoChanged.value };
            nodes.accountLogoFile.files = [{ type: 'image/png', size: 10 }];
            listeners['accountLogoFile:change']();
            out.afterPick = [nodes.accountLogoChanged.value, nodes.accountLogoData.value];
            nodes.accountLogoChanged.value = 'false';
            nodes.accountLogoFile.files = [{ type: 'application/pdf', size: 10 }];
            listeners['accountLogoFile:change']();
            out.afterRefused = nodes.accountLogoChanged.value;
            listeners['accountLogoRemove:click']();
            out.afterRemove = [nodes.accountLogoChanged.value, nodes.accountLogoData.value];
            console.log(JSON.stringify(out));
            """);
        if (output is null)
        {
            return; // node not installed on this machine
        }

        using var json = JsonDocument.Parse(output);
        Assert.Equal("false", json.RootElement.GetProperty("start").GetString());
        Assert.Equal(new[] { "true", "data:image/png;base64,NEW" }, json.RootElement.GetProperty("afterPick").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("false", json.RootElement.GetProperty("afterRefused").GetString());   // a refused file changes nothing
        Assert.Equal(new[] { "true", "" }, json.RootElement.GetProperty("afterRemove").EnumerateArray().Select(e => e.GetString()));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static AccountsController Controller(Gateway gateway)
    {
        var controller = new AccountsController(new HttpClient(gateway), new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "http://gateway.test", ["GatewayUrl"] = "http://gateway.test" }).Build(),
            new KeyLocalizer<SharedResource>(), new KeyLocalizer<AccountIndex>(), NullLogger<AccountsController>.Instance);
        var claims = new List<Claim> { new("tenantId", Tenant), new("permission", "crm.account.update"), new("permission", "crm.account.create"),
            new("permission", "crm.account.read") };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new NoTempData());
        return controller;
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "acc-edit-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>The Gateway: the account write answers the scripted status; every other call (reference sets, parent
    /// options) answers an empty 404.</summary>
    private sealed class Gateway(HttpStatusCode writeStatus, string writeBody) : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), body));
            var isWrite = request.Method != HttpMethod.Get && request.RequestUri!.AbsolutePath.StartsWith("/api/crm/accounts", StringComparison.Ordinal);
            return isWrite
                ? new HttpResponseMessage(writeStatus) { Content = new StringContent(writeBody, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
