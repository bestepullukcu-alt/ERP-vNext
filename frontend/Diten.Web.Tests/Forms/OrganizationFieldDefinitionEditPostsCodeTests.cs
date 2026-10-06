using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Diten.Web.Models.OrganizationFieldDefinitions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Web.Tests.Forms;

/// <summary>
/// AN IMMUTABLE FIELD STILL HAS TO POST ITS VALUE — MOD-0288-FU04, owner report 2026-09-21.
///
/// <para><b>The measured defect.</b> The edit form rendered the code box as <c>disabled</c>. A disabled control
/// is not successful: the browser sends nothing for it. So every edit arrived at
/// <c>OrganizationFieldDefinitionsController.Edit</c> with an empty <c>Code</c>, its <c>[Required]</c> failed,
/// <c>ModelState.IsValid</c> was false, and the controller re-rendered the same page with the code box painted
/// red. This form could not save a single change since it was written — the owner found it on the first edit
/// they tried, and nothing in the product said why.</para>
///
/// <para><b>The cure, and why it is not "drop the Required".</b> Readonly is exactly as uneditable and it POSTS,
/// so the model's own immutability guard (<c>OriginalCode</c> compared to <c>Code</c>) finally has both sides to
/// compare. That guard exists because a crafted post can carry any code at all; removing the requirement would
/// have disarmed it and let the silent no-op it was written to prevent come back.</para>
///
/// <para><b>Why this renders the view.</b> The claim is about what the BROWSER sends, and that is decided by the
/// attributes on the tag the Razor view emits — not by anything a source-reading assertion could stand behind.</para>
/// </summary>
public sealed class OrganizationFieldDefinitionEditPostsCodeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string FormView = "/Views/Organization/FieldDefinitions/_Form.cshtml";
    private const string StoredCode = "PERM_OU_ID";

    private readonly WebApplicationFactory<Program> _factory;

    public OrganizationFieldDefinitionEditPostsCodeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task The_code_box_on_an_edit_form_posts_the_stored_code()
    {
        var html = await RenderEditAsync();
        var tag = CodeInput(html);

        Assert.False(
            Regex.IsMatch(tag, @"\bdisabled\b", RegexOptions.IgnoreCase),
            "the code box is disabled again — a disabled control posts nothing, so every save fails [Required]");
        Assert.True(
            Regex.IsMatch(tag, @"\breadonly\b", RegexOptions.IgnoreCase),
            "the code box must stay uneditable; readonly is the form of that which still posts");
        Assert.Equal(StoredCode, Attribute(tag, "value"));
        Assert.Equal("Code", Attribute(tag, "name"));
    }

    [Fact]
    public async Task And_the_reader_can_see_it_is_not_an_ordinary_box()
    {
        // Bootstrap 5 dropped the [readonly] background rule, so an unpainted readonly box reads as editable —
        // the owner clicked into one on the Users screen and found nothing happened. Same paint, same reason.
        Assert.Contains("bg-label-secondary", Attribute(CodeInput(await RenderEditAsync()), "class"));
    }

    [Theory]
    // What the disabled box actually produced on every save: no Code at all.
    [InlineData("", false, nameof(OrganizationFieldDefinitionEditViewModel.Code))]
    // What readonly produces: the stored code, unchanged.
    [InlineData(StoredCode, true, null)]
    // What a crafted post produces — refused, and visibly, which is the guard's whole purpose.
    [InlineData("SOMETHING_ELSE", false, nameof(OrganizationFieldDefinitionEditViewModel.Code))]
    public void The_model_answers_each_posted_code_the_way_the_controller_will(string posted, bool expectedValid, string? expectedMember)
    {
        var model = new OrganizationFieldDefinitionEditViewModel
        {
            Id = Guid.NewGuid(),
            Code = posted,
            OriginalCode = StoredCode,
            Name = "Kalıcı birim kimliği",
            DataType = "Text",
            ExpectedVersion = 3
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
        if (expectedMember is not null)
        {
            Assert.Contains(results, r => r.MemberNames.Contains(expectedMember));
        }
    }

    private static string CodeInput(string html)
    {
        var tag = Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .FirstOrDefault(t => Attribute(t, "name") == "Code" && Attribute(t, "type") != "hidden");
        Assert.NotNull(tag);
        return tag!;
    }

    private static string? Attribute(string tag, string attribute)
    {
        var match = Regex.Match(tag, $"\\b{Regex.Escape(attribute)}\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<string> RenderEditAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: FormView, isMainPage: false);
        if (!found.Success)
        {
            throw new InvalidOperationException(
                $"Could not load {FormView}. Searched: {string.Join(", ", found.SearchedLocations ?? [])}");
        }

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        httpContext.Items[typeof(IUrlHelper)] = new FixedUrlHelper(actionContext);

        var model = new OrganizationFieldDefinitionEditViewModel
        {
            Id = Guid.NewGuid(),
            Code = StoredCode,
            OriginalCode = StoredCode,
            Name = "Kalıcı birim kimliği",
            DataType = "Text",
            ExpectedVersion = 3
        };

        var viewData = new ViewDataDictionary<OrganizationFieldDefinitionEditViewModel>(
            services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState)
        {
            Model = model
        };
        viewData["FormMode"] = "edit";

        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        using var writer = new StringWriter();
        await found.View.RenderAsync(
            new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private sealed class FixedUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;
        public string Action(UrlActionContext actionContext) => "/rendered-in-a-test";
        public string? Content(string? contentPath) => contentPath?.TrimStart('~');
        public bool IsLocalUrl(string? url) => true;
        public string Link(string? routeName, object? values) => "/rendered-in-a-test";
        public string RouteUrl(UrlRouteContext routeContext) => "/rendered-in-a-test";
    }
}
