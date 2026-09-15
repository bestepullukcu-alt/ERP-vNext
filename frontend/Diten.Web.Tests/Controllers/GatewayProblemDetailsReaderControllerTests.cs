using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskChecklistTemplates;
using Diten.Web.Models.TaskRecurrenceRules;
using Diten.Web.Models.TaskTemplates;
using Diten.Web.Models.TaskTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-398 (WP-PSS-MOD0024-FOLLOWUPS-02) — the four Task-screen controllers that did NOT have
/// <c>TaskFieldDefinitionsController</c>'s ProblemDetails handling (<see cref="GatewayProblemDetailsReaderTests"/>
/// measures the extracted rule itself; <c>TaskFieldDefinitionGatewayRoundTripTests</c> already covers the
/// original). Before this WP each of these four printed ASP.NET's own model-binding 400 verbatim on the page —
/// literal <c>{"errors":{...</c> — the moment a request body could not even be bound.
///
/// <para>Same technique as the original: a REAL POST through a recording gateway, asserting the JSON that
/// actually leaves ModelState — not a copy of the parsing logic.</para>
/// </summary>
public sealed class GatewayProblemDetailsReaderControllerTests
{
    private const string GatewayUrl = "http://gateway.test";

    private const string SortOrderConversionMessage =
        "The JSON value could not be converted to System.Int32. Path: $.sortOrder | LineNumber: 0 | BytePositionInLine: 171.";

    private const string ModelBindingProblemDetails =
        "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"One or more validation errors occurred.\"," +
        "\"status\":400,\"errors\":{\"request\":[\"The request field is required.\"],\"$.sortOrder\":[\"" + SortOrderConversionMessage + "\"]}," +
        "\"traceId\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}";

    private const string EmptyProblemDetails =
        "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"Bad Request\",\"status\":400," +
        "\"traceId\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}";

    // ── TaskTypesController ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TaskTypes_Create_WithFieldBindingProblemDetails_ShowsFieldMessagesNotRawJson()
    {
        var gateway = new RecordingGateway(ModelBindingProblemDetails);
        var controller = TaskTypesControllerWith(gateway);

        var result = await controller.Create(ValidTaskType());

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal(["The request field is required.", SortOrderConversionMessage], messages);
        AssertNoRawJson(messages);
    }

    [Fact]
    public async Task TaskTypes_Create_WithFieldlessProblemDetails_ShowsSharedGatewayErrorNotRawJson()
    {
        var gateway = new RecordingGateway(EmptyProblemDetails);
        var controller = TaskTypesControllerWith(gateway);

        var result = await controller.Create(ValidTaskType());

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal([Localized("GatewayError")], messages);
        AssertNoRawJson(messages);
    }

    // ── TaskChecklistTemplatesController ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChecklistTemplates_Create_WithFieldBindingProblemDetails_ShowsFieldMessagesNotRawJson()
    {
        var gateway = new RecordingGateway(ModelBindingProblemDetails);
        var controller = ChecklistTemplatesControllerWith(gateway);

        var result = await controller.Create(ValidChecklistTemplate());

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal(["The request field is required.", SortOrderConversionMessage], messages);
        AssertNoRawJson(messages);
    }

    [Fact]
    public async Task ChecklistTemplates_Create_WithFieldlessProblemDetails_ShowsSharedGatewayErrorNotRawJson()
    {
        var gateway = new RecordingGateway(EmptyProblemDetails);
        var controller = ChecklistTemplatesControllerWith(gateway);

        var result = await controller.Create(ValidChecklistTemplate());

        Assert.IsType<ViewResult>(result);
        Assert.Equal([Localized("GatewayError")], FormErrors(controller));
    }

    // ── TaskRecurrenceRulesController ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecurrenceRules_Create_WithFieldBindingProblemDetails_ShowsFieldMessagesNotRawJson()
    {
        var gateway = new RecordingGateway(ModelBindingProblemDetails);
        var controller = RecurrenceRulesControllerWith(gateway);

        var result = await controller.Create(ValidRecurrenceRule());

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal(["The request field is required.", SortOrderConversionMessage], messages);
        AssertNoRawJson(messages);
    }

    [Fact]
    public async Task RecurrenceRules_Create_WithFieldlessProblemDetails_ShowsSharedGatewayErrorNotRawJson()
    {
        var gateway = new RecordingGateway(EmptyProblemDetails);
        var controller = RecurrenceRulesControllerWith(gateway);

        var result = await controller.Create(ValidRecurrenceRule());

        Assert.IsType<ViewResult>(result);
        Assert.Equal([Localized("GatewayError")], FormErrors(controller));
    }

    // ── TaskTemplatesController ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Templates_Create_WithFieldBindingProblemDetails_ShowsFieldMessagesNotRawJson()
    {
        var gateway = new RecordingGateway(ModelBindingProblemDetails);
        var controller = TemplatesControllerWith(gateway);

        var result = await controller.Create(ValidTaskTemplate());

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal(["The request field is required.", SortOrderConversionMessage], messages);
        AssertNoRawJson(messages);
    }

    [Fact]
    public async Task Templates_Create_WithFieldlessProblemDetails_ShowsSharedGatewayErrorNotRawJson()
    {
        var gateway = new RecordingGateway(EmptyProblemDetails);
        var controller = TemplatesControllerWith(gateway);

        var result = await controller.Create(ValidTaskTemplate());

        Assert.IsType<ViewResult>(result);
        Assert.Equal([Localized("GatewayError")], FormErrors(controller));
    }

    // ── models ───────────────────────────────────────────────────────────────────────────────────────────────

    private static TaskTypeEditViewModel ValidTaskType() => new()
    {
        Code = "bl398.type", Name = "BL-398 probe"
    };

    private static ChecklistTemplateEditViewModel ValidChecklistTemplate() => new()
    {
        Code = "bl398.checklist", Name = "BL-398 probe",
        Items = [new ChecklistTemplateItemViewModel { Code = "step-1", LabelText = "Step one", Requirement = "Optional" }]
    };

    private static TaskRecurrenceRuleEditViewModel ValidRecurrenceRule() => new()
    {
        Name = "BL-398 probe", Frequency = "Monthly", AssignmentTarget = "Person", AssigneeUserId = Guid.NewGuid()
    };

    private static TaskTemplateEditViewModel ValidTaskTemplate() => new()
    {
        Code = "bl398.template", Name = "BL-398 probe"
    };

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────

    private static void AssertNoRawJson(IEnumerable<string> messages) =>
        Assert.All(messages, message =>
        {
            Assert.DoesNotContain("{", message);
            Assert.DoesNotContain("\"errors\"", message);
            Assert.DoesNotContain("traceId", message);
        });

    private static List<string> FormErrors(Controller controller) =>
        controller.ModelState[string.Empty]?.Errors.Select(error => error.ErrorMessage).ToList() ?? [];

    private static string Localized(string key) => $"L:{key}";

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl })
        .Build();

    private static void AttachHttpContext(Controller controller)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", Guid.NewGuid().ToString())], "Test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new InMemoryTempDataProvider());
    }

    private static TaskTypesController TaskTypesControllerWith(RecordingGateway gateway)
    {
        var controller = new TaskTypesController(
            new HttpClient(gateway), Configuration(), new PrefixingSharedLocalizer(),
            new PrefixingLocalizer<Diten.Web.Views.Tasks.TaskTypes.TaskTypesIndex>(),
            NullLogger<TaskTypesController>.Instance);
        AttachHttpContext(controller);
        return controller;
    }

    private static TaskChecklistTemplatesController ChecklistTemplatesControllerWith(RecordingGateway gateway)
    {
        var controller = new TaskChecklistTemplatesController(
            new HttpClient(gateway), Configuration(), new PrefixingSharedLocalizer(),
            NullLogger<TaskChecklistTemplatesController>.Instance);
        AttachHttpContext(controller);
        return controller;
    }

    private static TaskRecurrenceRulesController RecurrenceRulesControllerWith(RecordingGateway gateway)
    {
        var controller = new TaskRecurrenceRulesController(
            new HttpClient(gateway), Configuration(), new PrefixingSharedLocalizer(),
            NullLogger<TaskRecurrenceRulesController>.Instance);
        AttachHttpContext(controller);
        return controller;
    }

    private static TaskTemplatesController TemplatesControllerWith(RecordingGateway gateway)
    {
        var controller = new TaskTemplatesController(
            new HttpClient(gateway), Configuration(), new PrefixingSharedLocalizer(),
            NullLogger<TaskTemplatesController>.Instance);
        AttachHttpContext(controller);
        return controller;
    }

    /// <summary>
    /// One body for every request this test fires, regardless of URL — the create POST under test, and (for
    /// TaskTypesController only) the closure-outcome-catalog GET its Create action preloads. That preload is
    /// written defensively (any non-success answer is swallowed to an empty list), so a shared 400 body cannot
    /// make it crash — only the create POST's own handling of that body is what these tests measure.
    /// </summary>
    private sealed class RecordingGateway(string body, HttpStatusCode status = HttpStatusCode.BadRequest)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
            });
    }

    private sealed class PrefixingSharedLocalizer : IStringLocalizer<Diten.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, Localized(name), resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, Localized(name), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class PrefixingLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, Localized(name), resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, Localized(name), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
