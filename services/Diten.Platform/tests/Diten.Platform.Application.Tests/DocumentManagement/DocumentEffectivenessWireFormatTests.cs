using System.Text.Json;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// DCP-005 — wire-format guard for the effectiveness contract. DocumentEffectivenessState crosses the HTTP boundary
/// (the effectiveness:batch response `state` field), so per the service convention (Enums/Tasks/TaskEnums.cs:7-11) it
/// must serialize as its NAME, not the numeric value. These tests fail if the per-enum
/// [JsonConverter(typeof(JsonStringEnumConverter))] is ever dropped (System.Text.Json would then write 0/1/2).
/// </summary>
public sealed class DocumentEffectivenessWireFormatTests
{
    [Theory]
    [InlineData(DocumentEffectivenessState.Effective, "\"Effective\"")]
    [InlineData(DocumentEffectivenessState.Blocked, "\"Blocked\"")]
    [InlineData(DocumentEffectivenessState.Unresolved, "\"Unresolved\"")]
    public void State_enum_serializes_as_its_name(DocumentEffectivenessState state, string expected) =>
        Assert.Equal(expected, JsonSerializer.Serialize(state));

    [Fact]
    public void Result_state_field_is_a_string_not_a_number_on_the_wire()
    {
        // Serialized the way the API emits it (camelCase), so this asserts the real response shape.
        var result = new DocumentEffectivenessResult(new[]
        {
            new DocumentEffectivenessItem("UID-EFF", DocumentEffectivenessState.Effective, "C-EFF", "UID-EFF", "Effective", null),
            new DocumentEffectivenessItem("C-BLK", DocumentEffectivenessState.Blocked, "C-BLK", "UID-BLK", "Retired", "Retired"),
            new DocumentEffectivenessItem("UID-X", DocumentEffectivenessState.Unresolved, null, null, null, null)
        });

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"state\":\"Effective\"", json);
        Assert.Contains("\"state\":\"Blocked\"", json);
        Assert.Contains("\"state\":\"Unresolved\"", json);
        // The defect being fixed: a numeric enum on the wire (measured live: state:2). Without the [JsonConverter]
        // these would be state:0 / state:1 / state:2 and the assertions above would fail.
        Assert.DoesNotContain("\"state\":0", json);
        Assert.DoesNotContain("\"state\":1", json);
        Assert.DoesNotContain("\"state\":2", json);
    }
}
