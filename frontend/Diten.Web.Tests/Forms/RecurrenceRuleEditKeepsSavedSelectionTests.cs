using System.Globalization;
using System.Text.RegularExpressions;
using Diten.Web.Models.TaskRecurrenceRules;
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
/// THE EDIT FORM MUST COME BACK WITH WHAT WAS SAVED — owner, live, 2026-09-25. A monthly rule saved for a person with a
/// template opened on Edit with an empty person and template: the three pickers are rendered with no options (form.js
/// fills them from the lookups afterwards), so the tag helper could not mark the saved id, and form.js had nothing to
/// re-apply. A save from that screen would have blanked an assignment nobody touched.
///
/// <para>This renders the REAL <c>_Form.cshtml</c> with a saved model and reads the markup form.js starts from; the
/// JS half (re-applying <c>data-selected</c> after the options exist) is pinned by
/// <c>tests/recurrence-rule-assignable-people-envelope.test.js</c>.</para>
/// </summary>
public sealed class RecurrenceRuleEditKeepsSavedSelectionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Person = Guid.Parse("c8488c89-ba09-4ca3-902e-f1f8eb21abee");
    private static readonly Guid Position = Guid.Parse("5f5d1afb-37f7-45a3-b946-b6a79a24f490");
    private static readonly Guid Template = Guid.Parse("315fa08d-f970-49d6-8cc9-ed62bb5250e9");

    [Theory]
    [InlineData("AssigneeUserId", "c8488c89-ba09-4ca3-902e-f1f8eb21abee")]
    [InlineData("PoolPositionId", "5f5d1afb-37f7-45a3-b946-b6a79a24f490")]
    [InlineData("TaskTemplateId", "315fa08d-f970-49d6-8cc9-ed62bb5250e9")]
    public async Task A_saved_picker_value_reaches_the_markup_the_script_restores_it_from(string selectName, string savedId)
    {
        var html = await RenderAsync(new TaskRecurrenceRuleEditViewModel
        {
            Name = "Registrasyon süreci · aylık başlatma",
            AssigneeUserId = Person,
            PoolPositionId = Position,
            TaskTemplateId = Template
        });

        var select = Regex.Match(html, $"<select\\b[^>]*\\bname=\"{selectName}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(select.Success, $"{selectName} select not rendered");
        Assert.Contains($"data-selected=\"{savedId}\"", select.Value, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> RenderAsync(TaskRecurrenceRuleEditViewModel model)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en"); // BL-446: never the runner's invariant culture
        try
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var found = services.GetRequiredService<IRazorViewEngine>()
                .GetView(executingFilePath: null, viewPath: "/Views/Tasks/RecurrenceRules/_Form.cshtml", isMainPage: false);
            Assert.True(found.Success, "could not load the recurrence rule form");

            var httpContext = new DefaultHttpContext { RequestServices = services };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
            httpContext.Items[typeof(IUrlHelper)] = new FixedUrlHelper(actionContext);

            var viewData = new ViewDataDictionary<TaskRecurrenceRuleEditViewModel>(
                services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState) { Model = model };
            viewData["FormMode"] = "edit";

            using var writer = new StringWriter();
            var viewContext = new ViewContext(actionContext, found.View, viewData,
                new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions());
            await found.View.RenderAsync(viewContext);
            return writer.ToString();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
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
