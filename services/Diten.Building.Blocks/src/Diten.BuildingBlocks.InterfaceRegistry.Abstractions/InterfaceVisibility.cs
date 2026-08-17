using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterfaceVisibility
{
    Internal = 0,
    Platform = 1,
    Tenant = 2,
    Public = 3
}
