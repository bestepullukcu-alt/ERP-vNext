using System.Globalization;
using System.Text.RegularExpressions;
using Diten.Web.Models.Governance;
using Diten.Web.Models.TaskFieldDefinitions;
using Diten.Web.Models.TaskRecurrenceRules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Web.Tests.Forms;

/// <summary>
/// A form switch must post what the user left on screen.
///
/// <para><b>The measured defect this pins.</b> Several forms carried a hand-written
/// <c>&lt;input type="hidden" name="IsActive" value="false" /&gt;</c> placed BEFORE the switch. MVC's
/// <c>SimpleTypeModelBinder</c> reads <c>ValueProviderResult.FirstValue</c> for a repeated name, so the
/// hand-written "false" won every time and every record saved passive however the user left the switch. The form
/// still said "saved", so nothing on screen contradicted it.</para>
///
/// <para><b>Two shapes, one rule.</b> Where the switch uses <c>asp-for</c> the cure is to delete the hand-written
/// hidden, because the tag helper already emits its own in the right place. Where the switch is a plain checkbox
/// (the tenant Users offcanvas was) nobody emits a companion, so deleting the hidden would instead make turning a
/// switch OFF post nothing at all — there the hidden has to move BEHIND the checkbox. Both cures are asserted the
/// same way here, through the payload, so neither has to be taken on trust.</para>
///
/// <para><b>Why this test renders the view instead of reading its source.</b> The companion hidden that makes a
/// checkbox round-trip is not written in the .cshtml at all — <c>asp-for</c> queues it into
/// <c>FormContext.EndOfFormContent</c>, so it is emitted at the CLOSING form tag, after the switch. A test that
/// only read Razor source could not see the very element that decides the outcome; it could assert a coding
/// convention, never the posted payload. So the view is rendered for real, the browser's submission is rebuilt
/// from the rendered controls in document order, and the REAL model binder is asked what it makes of it. That
/// chain measures the actual claim: a ticked switch binds true.</para>
///
/// <para>Restoring the hand-written hidden turns <see cref="A_ticked_switch_binds_TRUE"/> red.</para>
/// </summary>
public sealed class ActiveSwitchBindingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RecurrenceRuleForm = "/Views/Tasks/RecurrenceRules/_Form.cshtml";
    private const string FieldDefinitionForm = "/Views/Tasks/FieldDefinitions/_Form.cshtml";
    private const string UsersOffcanvas = "/Views/Governance/Users/_CreateEditOffcanvas.cshtml";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public ActiveSwitchBindingTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    // ── the claim ────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RecurrenceRuleForm, nameof(TaskRecurrenceRuleEditViewModel.IsActive))]
    [InlineData(FieldDefinitionForm, nameof(TaskFieldDefinitionEditViewModel.IsActive))]
    [InlineData(FieldDefinitionForm, nameof(TaskFieldDefinitionEditViewModel.IsRequired))]
    [InlineData(UsersOffcanvas, nameof(UserEditViewModel.IsActive))]
    public async Task A_ticked_switch_binds_TRUE(string viewPath, string switchName)
    {
        var posted = await PostedValuesAsync(viewPath, switchName, ticked: true);

        _output.WriteLine($"{viewPath} — ticked switch posts {switchName}={string.Join(",", posted)}");

        var bound = await BindBooleanAsync(viewPath, switchName, posted);

        Assert.True(
            bound,
            $"The user left the {switchName} switch ON, the browser posted {switchName}={string.Join(",", posted)}, "
            + "and model binding produced FALSE. The record would save with nothing on screen saying so.");
    }

    // ── non-vacuity: a binder that answered "true" to everything would satisfy the test above ─────────────

    [Theory]
    [InlineData(RecurrenceRuleForm, nameof(TaskRecurrenceRuleEditViewModel.IsActive))]
    [InlineData(FieldDefinitionForm, nameof(TaskFieldDefinitionEditViewModel.IsActive))]
    [InlineData(FieldDefinitionForm, nameof(TaskFieldDefinitionEditViewModel.IsRequired))]
    [InlineData(UsersOffcanvas, nameof(UserEditViewModel.IsActive))]
    public async Task An_untouched_switch_binds_FALSE(string viewPath, string switchName)
    {
        var posted = await PostedValuesAsync(viewPath, switchName, ticked: false);

        _output.WriteLine($"{viewPath} — untouched switch posts {switchName}={string.Join(",", posted)}");

        // An unticked checkbox is not a successful control, so SOMETHING must still carry the "false" — that is
        // the whole job of the companion hidden. If this list came back empty the property would keep whatever
        // the model was seeded with, and turning the switch off would silently do nothing. This is the assertion
        // that stops the plain-checkbox forms from being "fixed" by deleting their hidden outright.
        Assert.NotEmpty(posted);

        var bound = await BindBooleanAsync(viewPath, switchName, posted);

        Assert.False(bound, $"The user turned the {switchName} switch OFF and model binding produced TRUE.");
    }

    // ── the shape that caused the defect, pinned directly ────────────────────────────────────────────────

    [Theory]
    [InlineData(RecurrenceRuleForm, nameof(TaskRecurrenceRuleEditViewModel.IsActive))]
    [InlineData(FieldDefinitionForm, nameof(TaskFieldDefinitionEditViewModel.IsActive))]
    [InlineData(FieldDefinitionForm, nameof(TaskFieldDefinitionEditViewModel.IsRequired))]
    [InlineData(UsersOffcanvas, nameof(UserEditViewModel.IsActive))]
    public async Task The_FIRST_control_with_the_switch_name_is_the_switch_itself(string viewPath, string switchName)
    {
        /*
         * Model binding takes the FIRST value for a repeated name, so whichever control appears first decides the
         * answer. Naming that directly means the failure message points at the cause rather than at a bool.
         */
        var html = await RenderAsync(viewPath);
        var controls = ControlsNamed(html, switchName);

        Assert.NotEmpty(controls);

        var order = string.Join(" then ", controls.Select(c => $"{c.Type}={c.Value}"));
        _output.WriteLine($"{viewPath} — controls named {switchName}: {order}");

        Assert.True(
            string.Equals(controls[0].Type, "checkbox", StringComparison.OrdinalIgnoreCase),
            $"A '{controls[0].Type}' input named {switchName} comes BEFORE the switch ({order}). Model binding "
            + "takes the first value for a repeated name, so that input — not the user — decides what gets saved. "
            + "Where the switch uses asp-for, delete the hand-written hidden and let the tag helper emit its own; "
            + "where it is a plain checkbox, move the hidden BEHIND it.");
    }

    // ── rebuilding the browser's submission ──────────────────────────────────────────────────────────────

    /// <summary>
    /// What the browser would send for <paramref name="name"/>, in document order. Mirrors the HTML rule for
    /// successful controls: hidden inputs are always sent; a checkbox is sent only when it is ticked.
    /// </summary>
    private async Task<string[]> PostedValuesAsync(string viewPath, string name, bool ticked)
    {
        var html = await RenderAsync(viewPath);

        return ControlsNamed(html, name)
            .Where(c => !string.Equals(c.Type, "checkbox", StringComparison.OrdinalIgnoreCase) || ticked)
            .Select(c => c.Value)
            .ToArray();
    }

    /// <summary>Runs the values through the REAL MVC model binder, onto the REAL view model for that screen.</summary>
    private async Task<bool> BindBooleanAsync(string viewPath, string name, string[] postedValues)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            [name] = new StringValues(postedValues)
        });

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ControllerActionDescriptor(), new ModelStateDictionary());

        var binder = new BindingProbeController
        {
            ControllerContext = new ControllerContext(actionContext),
            MetadataProvider = services.GetRequiredService<IModelMetadataProvider>(),
            ModelBinderFactory = services.GetRequiredService<IModelBinderFactory>(),
            ObjectValidator = services.GetRequiredService<IObjectModelValidator>()
        };

        var model = NewModel(viewPath);

        var property = model.GetType().GetProperty(name)
            ?? throw new InvalidOperationException($"{model.GetType().Name} has no {name} property.");

        // Seeded to the OPPOSITE of every assertion's expectation, so a binder that wrote nothing at all cannot
        // be mistaken for one that bound correctly.
        property.SetValue(model, !postedValues.Contains("true"));

        await binder.Bind(model, new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture));

        return (bool)property.GetValue(model)!;
    }

    /// <summary>A real view model per screen — so a property that changed type or name fails here, not silently.</summary>
    private static object NewModel(string viewPath) => viewPath switch
    {
        RecurrenceRuleForm => new TaskRecurrenceRuleEditViewModel(),
        FieldDefinitionForm => new TaskFieldDefinitionEditViewModel(),
        UsersOffcanvas => new UserEditViewModel(),
        _ => throw new ArgumentOutOfRangeException(nameof(viewPath), viewPath, "No model registered for this view.")
    };

    /// <summary>Exposes the protected binding entry point; nothing about it is faked.</summary>
    private sealed class BindingProbeController : Controller
    {
        public Task<bool> Bind(object model, IValueProvider valueProvider) =>
            TryUpdateModelAsync(model, model.GetType(), prefix: string.Empty, valueProvider, propertyFilter: _ => true);
    }

    // ── rendering the view for real ──────────────────────────────────────────────────────────────────────

    private async Task<string> RenderAsync(string viewPath)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var found = engine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        if (!found.Success)
        {
            throw new InvalidOperationException(
                $"Could not load {viewPath}. Searched: {string.Join(", ", found.SearchedLocations ?? [])}");
        }

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());

        // The breadcrumb anchors ask for a URL, and generating one needs a live routing pipeline this test has no
        // reason to stand up. Stubbing it is safe precisely because nothing about hrefs decides the claim: the
        // input tag helper, the form context and the end-of-form content it queues the companion hidden into are
        // all the real thing.
        httpContext.Items[typeof(IUrlHelper)] = new FixedUrlHelper(actionContext);

        var model = NewModel(viewPath);
        var viewDataType = typeof(ViewDataDictionary<>).MakeGenericType(model.GetType());
        var viewData = (ViewDataDictionary)Activator.CreateInstance(
            viewDataType,
            services.GetRequiredService<IModelMetadataProvider>(),
            actionContext.ModelState)!;
        viewData.Model = model;
        viewData["FormMode"] = "create";

        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());

        using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, found.View, viewData, tempData, writer, new HtmlHelperOptions());
        await found.View.RenderAsync(viewContext);

        return writer.ToString();
    }

    /// <summary>Answers every URL question with the same placeholder. See the note at its only usage.</summary>
    private sealed class FixedUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;

        public string Action(UrlActionContext actionContext) => "/rendered-in-a-test";

        public string? Content(string? contentPath) => contentPath?.TrimStart('~');

        public bool IsLocalUrl(string? url) => true;

        public string Link(string? routeName, object? values) => "/rendered-in-a-test";

        public string RouteUrl(UrlRouteContext routeContext) => "/rendered-in-a-test";
    }

    // ── reading the rendered controls ────────────────────────────────────────────────────────────────────

    private static readonly Regex InputTag = new("<input\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static List<(string Type, string Value)> ControlsNamed(string html, string name)
    {
        var controls = new List<(string, string)>();

        foreach (Match tag in InputTag.Matches(html))
        {
            if (!string.Equals(Attribute(tag.Value, "name"), name, StringComparison.Ordinal))
            {
                continue;
            }

            var type = Attribute(tag.Value, "type") ?? "text";
            // An HTML checkbox with no value attribute submits "on"; every other control submits "".
            var value = Attribute(tag.Value, "value")
                        ?? (string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) ? "on" : string.Empty);

            controls.Add((type, value));
        }

        return controls;
    }

    private static string? Attribute(string tag, string attribute)
    {
        var match = Regex.Match(tag, $"\\b{Regex.Escape(attribute)}\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
