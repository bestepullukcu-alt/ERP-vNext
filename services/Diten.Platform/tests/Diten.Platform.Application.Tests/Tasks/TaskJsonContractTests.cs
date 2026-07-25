using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 — the HTTP/JSON layer. Every other task test calls handlers directly, which is exactly why a broken
/// wire contract shipped: the browser sends assignmentTarget:"SelfAssigned", and without a string enum converter
/// System.Text.Json accepts only integers, so create failed with 400 before any handler ran.
///
/// These options mirror the runtime: Platform's Program.cs calls AddControllers() with no AddJsonOptions, so MVC
/// serializes with JsonSerializerDefaults.Web. If that ever changes, these tests must be revisited.
/// </summary>
public class TaskJsonContractTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    // ── The exact failure that reached production ─────────────────────────────

    [Fact]
    public void CreateRequest_deserializes_the_body_the_browser_actually_sends()
    {
        // Verbatim shape produced by wwwroot/assets/js/Tasks/form.js buildCreatePayload.
        const string body = """
        {
          "title": "Prepare filing",
          "description": null,
          "priority": "Medium",
          "assignmentTarget": "SelfAssigned",
          "assigneeUserId": null,
          "poolPositionId": null,
          "organizationUnitId": null,
          "dueAt": "2026-08-01",
          "startAt": null,
          "plannedDate": null,
          "estimateHours": null,
          "tags": [],
          "reviewRequired": false,
          "approvalRequired": false,
          "approvalManagerUserId": null,
          "emailNotificationsEnabled": true,
          "delegationAllowed": false,
          "fieldValues": [],
          "watchers": []
        }
        """;

        var request = JsonSerializer.Deserialize<CreateTaskItemRequest>(body, WebOptions);

        Assert.NotNull(request);
        Assert.Equal("Prepare filing", request!.Title);
        Assert.Equal(TaskPriority.Medium, request.Priority);
        Assert.Equal(TaskAssignmentTarget.SelfAssigned, request.AssignmentTarget);
    }

    [Theory]
    [InlineData("SelfAssigned", TaskAssignmentTarget.SelfAssigned)]
    [InlineData("Person", TaskAssignmentTarget.Person)]
    [InlineData("PositionPool", TaskAssignmentTarget.PositionPool)]
    public void Every_assignment_target_the_form_offers_deserializes(string wire, TaskAssignmentTarget expected)
    {
        var json = $$"""{"title":"t","priority":"High","assignmentTarget":"{{wire}}"}""";

        var request = JsonSerializer.Deserialize<CreateTaskItemRequest>(json, WebOptions)!;

        Assert.Equal(expected, request.AssignmentTarget);
    }

    [Theory]
    [InlineData("Low", TaskPriority.Low)]
    [InlineData("Medium", TaskPriority.Medium)]
    [InlineData("High", TaskPriority.High)]
    public void Every_priority_the_form_offers_deserializes(string wire, TaskPriority expected)
    {
        var json = $$"""{"title":"t","priority":"{{wire}}","assignmentTarget":"SelfAssigned"}""";

        var request = JsonSerializer.Deserialize<CreateTaskItemRequest>(json, WebOptions)!;

        Assert.Equal(expected, request.Priority);
    }

    [Fact]
    public void Nested_watcher_and_field_value_enums_deserialize_as_strings()
    {
        // Nested collections are the easy place to miss a converter: the outer record deserializes fine and the
        // failure only shows up when a caller actually sends watchers or custom fields.
        const string json = """
        {
          "title": "t",
          "priority": "High",
          "assignmentTarget": "Person",
          "watchers": [ { "userId": "11111111-1111-1111-1111-111111111111", "role": "Consultant", "positionId": null } ],
          "fieldValues": [ { "definitionCode": "invoice-total", "valueType": "Currency", "value": "100" } ]
        }
        """;

        var request = JsonSerializer.Deserialize<CreateTaskItemRequest>(json, WebOptions)!;

        Assert.Equal(TaskWatcherRole.Consultant, Assert.Single(request.Watchers!).Role);
        Assert.Equal(TaskFieldValueType.Currency, Assert.Single(request.FieldValues!).ValueType);
    }

    [Fact]
    public void Update_request_deserializes_its_string_enums_too()
    {
        const string json = """
        {
          "title": "t",
          "priority": "Low",
          "expectedVersion": 3,
          "fieldValues": [ { "definitionCode": "note", "valueType": "Text", "value": "x" } ]
        }
        """;

        var request = JsonSerializer.Deserialize<UpdateTaskItemRequest>(json, WebOptions)!;

        Assert.Equal(TaskPriority.Low, request.Priority);
        Assert.Equal(TaskFieldValueType.Text, Assert.Single(request.FieldValues!).ValueType);
    }

    // ── Outbound: the browser compares these against strings ──────────────────

    [Fact]
    public void Response_enums_serialize_as_strings_not_integers()
    {
        // Tasks/details-page.js does `task.assignmentTarget === 'PositionPool'`, so a number here silently breaks
        // every comparison rather than throwing.
        var dto = new TaskFieldValueDto("invoice-total", TaskFieldValueType.Currency, "100");

        var json = JsonSerializer.Serialize(dto, WebOptions);

        Assert.Contains("\"valueType\":\"Currency\"", json);
        Assert.DoesNotContain("\"valueType\":2", json);
    }

    [Fact]
    public void Round_trip_survives_serialize_then_deserialize()
    {
        var original = new TaskWatcherRequest(Guid.NewGuid(), TaskWatcherRole.Watcher, null);

        var restored = JsonSerializer.Deserialize<TaskWatcherRequest>(
            JsonSerializer.Serialize(original, WebOptions), WebOptions);

        Assert.Equal(original, restored);
    }

    // ── The guard that keeps Phases 2–5 from reintroducing this ───────────────

    [Fact]
    public void Every_enum_reachable_from_a_request_or_response_type_is_string_serializable()
    {
        var rootTypes = new[]
        {
            typeof(CreateTaskItemRequest),
            typeof(UpdateTaskItemRequest),
            typeof(TaskWatcherRequest),
            typeof(TaskFieldValueDto),
            typeof(BulkDeleteTaskItemRequest),
            typeof(ClaimTaskItemRequest),
            typeof(TaskTransitionRequest),
            typeof(TaskItemListItemDto),
            typeof(TaskItemDetailDto),
            typeof(TaskWatcherDto),
            typeof(TaskDependencyDto),
            typeof(AssignablePositionDto)
        };

        var offenders = new List<string>();
        var reached = new List<Type>();
        var visited = new HashSet<Type>();

        foreach (var root in rootTypes)
        {
            Walk(root, visited, offenders, reached);
        }

        // Guard against a vacuous pass: if the walk stopped reaching enums (a refactor, a new wrapper type), the
        // assertion below would trivially hold while covering nothing.
        Assert.Contains(typeof(TaskPriority), reached);
        Assert.Contains(typeof(TaskAssignmentTarget), reached);
        Assert.Contains(typeof(TaskWatcherRole), reached);
        Assert.Contains(typeof(TaskFieldValueType), reached);

        // An enum on the wire without JsonStringEnumConverter is a 400 on input and an opaque integer on output.
        Assert.Empty(offenders);
    }

    private static void Walk(Type type, HashSet<Type> visited, List<string> offenders, List<Type> reached)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsEnum)
        {
            reached.Add(type);
            var hasConverter = type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
                == typeof(JsonStringEnumConverter);
            if (!hasConverter)
            {
                offenders.Add(type.Name);
            }

            return;
        }

        if (!visited.Add(type) || type.IsPrimitive || type == typeof(string) || type == typeof(Guid) ||
            type == typeof(decimal) || type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            return;
        }

        // Descend through collections so a nested DTO's enum cannot hide.
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Walk(argument, visited, offenders, reached);
            }

            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Walk(property.PropertyType, visited, offenders, reached);
        }
    }
}
