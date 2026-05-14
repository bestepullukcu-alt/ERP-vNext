using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterfaceChangeType
{
    New = 0,
    Changed = 1,
    Missing = 2,
    Deprecated = 3,
    Unchanged = 4
}
