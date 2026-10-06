using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers.MasterData;
using Diten.Web.Models.MasterData;
using Diten.Web.Views.MasterData.Brands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX1 — the Brand screens on the production web code: the real controller actions with MDM as a
/// recording handler (the register's server page, the localized refusals, the anti-forgery gate, the edit's version), and
/// the real views executed by the application's own Razor engine (the 403 face, references by name, never an id).
/// </summary>
public sealed class BrandsWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private static readonly Guid OwnerId = Guid.Parse("a0a0a0a0-1111-2222-3333-444444444444");
    private static readonly Guid UnitId = Guid.Parse("b0b0b0b0-1111-2222-3333-444444444444");
    private static readonly Guid AreaId = Guid.Parse("c0c0c0c0-1111-2222-3333-444444444444");
    private static readonly Guid CreatorId = Guid.Parse("d0d0d0d0-1111-2222-3333-444444444444");
    private readonly WebApplicationFactory<Program> _factory;

    public BrandsWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── item 6 — the register's server page ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_request_becomes_MDMs_paged_query_and_the_page_becomes_the_component_envelope()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK,
            "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"items\":[{\"brandId\":\"11111111-1111-1111-1111-111111111111\",\"brandCode\":\"B-1\"}],\"totalCount\":41,\"start\":40,\"length\":20,\"archivedWithLiveProducts\":3}}");
        var controller = Controller(wire, "mdm.brands.read",
            "?start=40&length=20&search=Asp%201&orderBy=brandName&orderDir=desc&draw=7&brandStatus=active&includeArchived=true&columns%5B0%5D%5Bdata%5D=brandId");

        var result = await controller.List(CancellationToken.None);

        Assert.Equal(
            $"{Gateway}/api/mdm/brands?start=40&length=20&search=Asp%201&orderBy=brandName&orderDir=desc&brandStatus=active&includeArchived=true",
            Assert.Single(wire.Urls));
        using var body = JsonDocument.Parse(Assert.IsType<ContentResult>(result).Content!);
        var data = body.RootElement.GetProperty("data");
        Assert.Equal("B-1", Assert.Single(data.GetProperty("items").EnumerateArray()).GetProperty("brandCode").GetString());
        Assert.Equal(41, data.GetProperty("total").GetInt64());
        Assert.Equal(41, data.GetProperty("filteredTotal").GetInt64());
        Assert.Equal(3, data.GetProperty("archivedWithLiveProducts").GetInt32());
    }

    [Theory]
    [InlineData("?start=0&length=500", "start=0&length=100")]
    [InlineData("?start=-5&length=0", "start=0&length=20")]
    [InlineData("", "start=0&length=20")]
    public async Task The_page_size_is_capped_at_100_and_a_bad_start_or_length_is_the_first_page(string query, string expected)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[],\"totalCount\":0}}");

        await Controller(wire, "mdm.brands.read", query).List(CancellationToken.None);

        Assert.Equal($"{Gateway}/api/mdm/brands?{expected}", Assert.Single(wire.Urls));
    }

    [Fact]
    public async Task A_page_without_a_count_is_a_gateway_failure_not_an_empty_list()
    {
        var result = await Controller(new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[]}}"), "mdm.brands.read", "?length=20")
            .List(CancellationToken.None);
        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ── item 7 — every refusal is a sentence in the reader's language ───────────────────────────────────────────

    [Fact]
    public async Task An_MDM_refusal_reaches_the_reader_as_its_sentence_never_the_code_or_MDMs_english()
    {
        var wire = new RecordingHandler(HttpStatusCode.Conflict,
            "{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"version_conflict: The brand has changed since it was loaded.\"]}");

        var result = await Controller(wire, "mdm.brands.archive", "", localizer: Sentences()).Archive(Guid.NewGuid(), CancellationToken.None);

        var refusal = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, refusal.StatusCode);
        var json = JsonSerializer.Serialize(refusal.Value);
        Assert.Contains("\"errors\":[\"S:Reason_version_conflict\"]", json);
        Assert.Contains("\"unmapped\":false", json);
        Assert.DoesNotContain("has changed since", json);
    }

    [Fact]
    public async Task A_refusal_whose_code_has_no_sentence_falls_to_the_status_sentence_and_is_marked_unmapped()
    {
        var wire = new RecordingHandler(HttpStatusCode.Conflict,
            "{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"brand_new_rule: Something new.\"]}");

        var result = await Controller(wire, "mdm.brands.archive", "", localizer: Sentences()).Archive(Guid.NewGuid(), CancellationToken.None);

        var json = JsonSerializer.Serialize(Assert.IsType<ObjectResult>(result).Value);
        Assert.Contains("\"errors\":[\"S:RefusedStatus_409\"]", json);
        Assert.Contains("\"unmapped\":true", json);
        Assert.DoesNotContain("brand_new_rule", json.Replace("\"reasonCode\":\"brand_new_rule\"", string.Empty));
    }

    [Fact]
    public async Task A_refused_form_save_shows_the_sentence_in_the_form()
    {
        var wire = new RecordingHandler(HttpStatusCode.Conflict,
            "{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"brand_code_duplicate: A brand with this code already exists in this tenant.\"]}");
        var controller = Controller(wire, "mdm.brands.create", "", localizer: Sentences());

        await controller.Create(new BrandEditViewModel { BrandCode = "B", BrandName = "B", EffectiveFrom = DateTimeOffset.Now }, CancellationToken.None);

        var errors = controller.ModelState[string.Empty]!.Errors.Select(x => x.ErrorMessage).ToList();
        Assert.Contains("S:Reason_brand_code_duplicate", errors);
        Assert.DoesNotContain(errors, x => x.Contains("brand_code_duplicate:", StringComparison.Ordinal));
    }

    // ── item 11 — a cross-site POST cannot change a brand ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(BrandsController.Archive))]
    [InlineData(nameof(BrandsController.Create))]
    [InlineData(nameof(BrandsController.Edit))]
    public void Every_state_changing_brand_action_validates_the_anti_forgery_token(string action)
    {
        var writes = typeof(BrandsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.Name == action && x.GetCustomAttributes<HttpPostAttribute>().Any())
            .ToList();
        Assert.NotEmpty(writes);
        Assert.All(writes, x => Assert.NotNull(x.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    // ── item 2 — the edit carries the read it was made on ───────────────────────────────────────────────────────

    [Fact]
    public async Task An_edit_sends_the_version_it_was_made_on()
    {
        var wire = new RecordingHandler(HttpStatusCode.NoContent, string.Empty);
        var controller = Controller(wire, "mdm.brands.update", "");

        await controller.Edit(Guid.NewGuid(),
            new BrandEditViewModel { BrandCode = "B", BrandName = "B", EffectiveFrom = DateTimeOffset.Now, ExpectedVersion = 7 },
            CancellationToken.None);

        using var sent = JsonDocument.Parse(Assert.Single(wire.Bodies));
        Assert.Equal(7, sent.RootElement.GetProperty("expectedVersion").GetInt32());
    }

    [Fact]
    public async Task The_editor_of_an_archived_brand_is_not_opened()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK,
            "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"brandId\":\"11111111-1111-1111-1111-111111111111\",\"brandCode\":\"B\",\"isArchived\":true,\"version\":3}}");
        var controller = Controller(wire, "mdm.brands.update", "");

        var result = await controller.Edit(Guid.Parse("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        Assert.Equal(nameof(BrandsController.Details), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task A_hand_written_body_naming_a_tenant_is_refused_before_MDM_is_asked()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{}");
        var proxy = new TenantProbe(wire);

        var result = await proxy.SendAsync(JsonDocument.Parse("{\"tenantId\":\"evil\",\"brandName\":\"x\"}").RootElement);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Urls);
    }

    // ── item 5 — UAS-001: the page's own 403, never a redirect, nothing of the page ─────────────────────────────

    [Fact]
    public async Task Every_brand_page_without_its_permission_answers_403_with_its_own_view_and_asks_MDM_nothing()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{}");
        var id = Guid.NewGuid();
        var pages = new (string View, Func<BrandsController, Task<IActionResult>> Act)[]
        {
            ("Index", c => Task.FromResult(c.Index())),
            ("Create", c => c.Create(CancellationToken.None)),
            ("Edit", c => c.Edit(id, CancellationToken.None)),
            ("Details", c => c.Details(id, CancellationToken.None))
        };

        foreach (var (view, act) in pages)
        {
            var controller = Controller(wire, "crm.account.read", "");
            var result = await act(controller);
            Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
            Assert.Equal($"~/Views/MasterData/Brands/{view}.cshtml", Assert.IsType<ViewResult>(result).ViewName);
        }

        Assert.Empty(wire.Urls);
    }

    [Theory]
    [InlineData("Index", "mdm.brands.read")]
    [InlineData("Create", "mdm.brands.create")]
    [InlineData("Edit", "mdm.brands.update")]
    [InlineData("Details", "mdm.brands.read")]
    public async Task The_unauthorized_face_of_each_brand_page_is_the_access_denied_surface_and_nothing_else(string page, string _)
    {
        var html = await RenderAsync($"/Views/MasterData/Brands/{page}.cshtml", "crm.account.read", model: null);

        Assert.Contains("diten-access-denied", html);
        Assert.DoesNotContain("dt-brands", html);
        Assert.DoesNotContain("brandForm", html);
        Assert.DoesNotContain("brandDetailsPage", html);
        Assert.DoesNotContain("brand-l10n", html);
    }

    [Fact]
    public async Task A_reader_gets_the_list_shell_in_server_mode_and_a_wildcard_grant_opens_it_too()
    {
        foreach (var permission in new[] { "mdm.brands.read", "*" })
        {
            var html = await RenderAsync("/Views/MasterData/Brands/Index.cshtml", permission, model: null);
            Assert.Contains("id=\"dt-brands\"", html);
            Assert.Contains("data-dt-data-mode=\"server\"", html);
            Assert.DoesNotContain("diten-access-denied", html);
        }
    }

    // BL-440 — through the real layout, both ways: the refused face carries no list script, the reader gets it.
    [Fact]
    public async Task With_the_layout_the_unauthorized_face_carries_no_list_script_and_the_reader_gets_it()
    {
        var denied = await LayoutRenderer.RenderAsync(_factory, "/Views/MasterData/Brands/Index.cshtml", "Brands", "crm.account.read");
        var reader = await LayoutRenderer.RenderAsync(_factory, "/Views/MasterData/Brands/Index.cshtml", "Brands", "mdm.brands.read");

        Assert.Contains("diten-access-denied", denied);
        Assert.DoesNotContain("Brands/index.js", denied);
        Assert.DoesNotContain("dt-brands", denied);
        Assert.Contains("Brands/index.js", reader);
        Assert.Contains("brand-l10n", reader);
        Assert.Contains("id=\"dt-brands\"", reader);
    }

    // ── item 8 — references by name; none of them as an id ──────────────────────────────────────────────────────

    [Fact]
    public async Task The_brand_page_shows_the_owner_by_name_and_no_reference_or_person_as_an_id()
    {
        var model = new BrandPageViewModel
        {
            Brand = new BrandDetailViewModel
            {
                BrandId = Guid.NewGuid(), BrandCode = "B-1", BrandName = "Brand", BrandStatus = "active",
                OwnerCompanyId = OwnerId, BusinessUnitId = UnitId, TherapeuticAreaId = AreaId,
                CreatedBy = CreatorId.ToString("D"), UpdatedBy = CreatorId.ToString("D"),
                EffectiveFrom = DateTimeOffset.Now, CreatedAt = DateTimeOffset.Now, LiveProductCount = 2
            },
            OwnerCompanyName = "LE-1 — Acme Pharma"
        };

        var html = await RenderAsync("/Views/MasterData/Brands/Details.cshtml", "mdm.brands.read", model);

        // The name is a C# value, so Razor writes the dash encoded; the code and the name are what the reader sees.
        Assert.Contains("LE-1", html);
        Assert.Contains("Acme Pharma", html);
        Assert.Contains("Could not be resolved", html);
        foreach (var id in new[] { OwnerId, UnitId, AreaId, CreatorId })
        {
            Assert.DoesNotContain(id.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_form_offers_the_owner_by_name_and_keeps_the_other_references_without_showing_them()
    {
        var model = new BrandEditViewModel
        {
            BrandCode = "B-1", BrandName = "Brand", OwnerCompanyId = OwnerId, BusinessUnitId = UnitId, TherapeuticAreaId = AreaId,
            EffectiveFrom = DateTimeOffset.Now,
            OwnerCompanies = [new LegalEntityOptionViewModel(OwnerId, "LE-1", "Acme Pharma")]
        };

        var html = await RenderAsync("/Views/MasterData/Brands/_Form.cshtml", "mdm.brands.create", model);

        Assert.Contains("LE-1 — Acme Pharma", html);
        Assert.Contains("type=\"hidden\"", html);
        // The business unit and area ids round-trip as hidden values only — never as a field anybody types an id into.
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex($"<input(?![^>]*type=\"hidden\")[^>]*value=\"{UnitId}\""), html);
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex($"<input(?![^>]*type=\"hidden\")[^>]*value=\"{AreaId}\""), html);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<string> RenderAsync(string path, string permission, object? model)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = services, User = Principal(permission) };
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var view = engine.GetView(executingFilePath: null, viewPath: path, isMainPage: true);
        var page = engine.GetPage(executingFilePath: null, pagePath: path).Page
            ?? throw new InvalidOperationException($"Could not load {path}.");
        var actionContext = new ActionContext(httpContext,
            new RouteData { Values = { ["controller"] = "Brands", ["action"] = "Index" } },
            new ActionDescriptor(), new ModelStateDictionary());
        var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState)
        {
            Model = model
        };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, view.View!, viewData, tempData, writer, new HtmlHelperOptions())
        {
            ExecutingFilePath = path
        };
        page.ViewContext = viewContext;
        services.GetRequiredService<IRazorPageActivator>().Activate(page, viewContext);
        await page.ExecuteAsync();
        return writer.ToString();
    }

    private static BrandsController Controller(
        RecordingHandler wire, string permission, string query, IStringLocalizer<BrandsIndex>? localizer = null)
    {
        var context = new DefaultHttpContext { User = Principal(permission) };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-brand-list";
        context.Request.QueryString = new QueryString(query);
        return new BrandsController(new HttpClient(wire), GatewayConfiguration(),
            new EchoLocalizer<Diten.Web.SharedResource>(), localizer ?? new EchoLocalizer<BrandsIndex>(),
            NullLogger<BrandsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new NullTempDataProvider())
        };
    }

    private static IConfiguration GatewayConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static ClaimsPrincipal Principal(string permission) => new(new ClaimsIdentity(
    [
        new Claim("tenant_id", Guid.NewGuid().ToString("D")),
        new Claim("actor_type", "tenant_user"),
        new Claim("sub", Guid.NewGuid().ToString("D")),
        new Claim("permission", permission)
    ], "fixture"));

    /// <summary>A localizer that knows the Brand reason and status sentences and nothing else (like a real resx).</summary>
    private static SentenceLocalizer Sentences() => new(name =>
        name.StartsWith("Reason_", StringComparison.Ordinal) && name != "Reason_brand_new_rule"
        || name.StartsWith("RefusedStatus_", StringComparison.Ordinal));

    private sealed class SentenceLocalizer(Func<string, bool> knows) : IStringLocalizer<BrandsIndex>
    {
        public LocalizedString this[string name] => knows(name) ? new(name, $"S:{name}") : new(name, name, resourceNotFound: true);
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class EchoLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    /// <summary>The shared proxy's tenant guard, reached through a subclass (the Brand pages never send a body).</summary>
    private sealed class TenantProbe : MasterDataGatewayController
    {
        public TenantProbe(HttpMessageHandler wire)
            : base(new HttpClient(wire), GatewayConfiguration(), NullLogger.Instance)
        {
            var context = new DefaultHttpContext { User = Principal("mdm.brands.update") };
            context.Request.Headers.Cookie = "access_token=synthetic-brand-list";
            ControllerContext = new ControllerContext { HttpContext = context };
        }

        public Task<IActionResult> SendAsync(JsonElement body)
            => ProxyJsonAsync(HttpMethod.Put, "/api/mdm/brands/x", body, "mdm.brands.update", CancellationToken.None);
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.OriginalString);
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            // Only the brand reads answer with the fixture body; the contract and legal-entity reads answer "not there".
            var path = request.RequestUri.AbsolutePath;
            if (path.Contains("contract", StringComparison.Ordinal) || path.Contains("legal-entities", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
