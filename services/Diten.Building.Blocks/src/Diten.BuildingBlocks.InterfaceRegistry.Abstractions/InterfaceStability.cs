using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterfaceStability
{
    Experimental = 0,
    Stable = 1,
    Deprecated = 2
}
