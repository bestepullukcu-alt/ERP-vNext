using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskFieldDefinitions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-388 — "Sıra" (SortOrder) left empty on the task field definition form.
///
/// <para>Driven through the REAL Create/Edit POST actions with a recording gateway handler, so what is asserted is
/// the JSON that actually leaves the controller (payload builder + serializer options + action together) and the
/// error text that actually lands in ModelState — not a copy of either.</para>
/// </summary>
public sealed class TaskFieldDefinitionGatewayRoundTripTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string FieldDefinitionsUrl = GatewayUrl + "/api/v1/tasks/field-definitions";

    private const string SortOrderConversionMessage =
        "The JSON value could not be converted to System.Int32. Path: $.sortOrder | LineNumber: 0 | BytePositionInLine: 171.";

    // The shape an [ApiController] answers with when the request body cannot be bound — the 400 the form hit when
    // sortOrder travelled as null: a ProblemDetails whose "errors" is a field -> messages DICTIONARY.
    private const string ModelBindingProblemDetails =
        "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"One or more validation errors occurred.\"," +
        "\"status\":400,\"errors\":{\"request\":[\"The request field is required.\"],\"$.sortOrder\":[\"" + SortOrderConversionMessage + "\"]}," +
        "\"traceId\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}";

    [Theory]
    [InlineData(null, 0)]
    [InlineData(7, 7)]
    public async Task Create_WhenSortOrderIsEmpty_SendsSortOrderZeroAndSaves(int? formValue, int expectedOnTheWire)
    {
        var gateway = new RecordingGateway(HttpStatusCode.Created, "{\"data\":null,\"isSuccessful\":true,\"statusCode\":201}");
        var controller = ControllerWith(gateway);

        var result = await controller.Create(Model(formValue));

        Assert.IsType<RedirectToActionResult>(result);
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(FieldDefinitionsUrl, request.Uri);
        AssertSortOrderOnTheWire(request.Body, expectedOnTheWire);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(7, 7)]
    public async Task Edit_WhenSortOrderIsEmpty_SendsSortOrderZeroAndSaves(int? formValue, int expectedOnTheWire)
    {
        var gateway = new RecordingGateway(HttpStatusCode.OK, "{\"data\":null,\"isSuccessful\":true,\"statusCode\":200}");
        var controller = ControllerWith(gateway);
        var id = Guid.NewGuid();

        var result = await controller.Edit(id, Model(formValue));

        Assert.IsType<RedirectToActionResult>(result);
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"{FieldDefinitionsUrl}/{id}", request.Uri);
        AssertSortOrderOnTheWire(request.Body, expectedOnTheWire);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Edit")]
    public async Task SaveAction_WhenGatewayReturnsValidationProblemDetails_ShowsFieldMessagesNotRawJson(string action)
    {
        var gateway = new RecordingGateway(HttpStatusCode.BadRequest, ModelBindingProblemDetails, "application/problem+json");
        var controller = ControllerWith(gateway);

        var result = await Save(controller, action);

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal(new[] { "The request field is required.", SortOrderConversionMessage }, messages);
        AssertNoRawJson(messages);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Edit")]
    public async Task SaveAction_WhenProblemDetailsCarriesNoFieldMessages_ShowsSharedGatewayErrorNotRawJson(string action)
    {
        const string body = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.1\",\"title\":\"Bad Request\",\"status\":400," +
                            "\"traceId\":\"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00\"}";
        var gateway = new RecordingGateway(HttpStatusCode.BadRequest, body, "application/problem+json");
        var controller = ControllerWith(gateway);

        var result = await Save(controller, action);

        Assert.IsType<ViewResult>(result);
        var messages = FormErrors(controller);
        Assert.Equal(new[] { Localized("GatewayError") }, messages);
        AssertNoRawJson(messages);
    }

    [Fact]
    public async Task Create_WhenGatewayReturnsPlatformEnvelopeErrors_StillShowsThemUnchanged()
    {
        // Non-vacuity for the ProblemDetails step: the Platform envelope is read FIRST and must keep winning.
        var gateway = new RecordingGateway(HttpStatusCode.Conflict,
            "{\"data\":null,\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"Code already exists.\"]}");
        var controller = ControllerWith(gateway);

        var result = await controller.Create(Model(null));

        Assert.IsType<ViewResult>(result);
        Assert.Equal(new[] { "Code already exists." }, FormErrors(controller));
    }

    // ── plumbing ───────────────────────────────────────────────────────────────────────────────────────────

    private static Task<IActionResult> Save(TaskFieldDefinitionsController controller, string action) => action switch
    {
        "Create" => controller.Create(Model(null)),
        "Edit" => controller.Edit(Guid.NewGuid(), Model(null)),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    private static TaskFieldDefinitionEditViewModel Model(int? sortOrder) => new()
    {
        Code = "bl388.sort.order",
        LabelText = "Sort order left empty",
        Section = "General",
        SortOrder = sortOrder,
        ExpectedVersion = 1
    };

    private static void AssertSortOrderOnTheWire(string body, int expected)
    {
        using var json = JsonDocument.Parse(body);
        var sortOrder = json.RootElement.GetProperty("sortOrder");
        Assert.Equal(JsonValueKind.Number, sortOrder.ValueKind);
        Assert.Equal(expected, sortOrder.GetInt32());
    }

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

    private static TaskFieldDefinitionsController ControllerWith(RecordingGateway gateway)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl })
            .Build();

        var controller = new TaskFieldDefinitionsController(
            new HttpClient(gateway),
            configuration,
            new PrefixingSharedLocalizer(),
            NullLogger<TaskFieldDefinitionsController>.Instance);

        // A tenant claim has to be present, or the action refuses before it ever reaches the gateway call under test.
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", Guid.NewGuid().ToString())], "Test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new InMemoryTempDataProvider());
        return controller;
    }

    private sealed class RecordingGateway(HttpStatusCode status, string body, string mediaType = "application/json")
        : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.ToString(), sent));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        }
    }

    // Values are prefixed so a generic shared message is distinguishable from text that came from the gateway body.
    private sealed class PrefixingSharedLocalizer : IStringLocalizer<Diten.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, Localized(name), resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, Localized(name), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
