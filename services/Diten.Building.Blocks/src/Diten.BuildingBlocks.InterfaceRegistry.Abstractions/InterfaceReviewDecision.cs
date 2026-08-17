using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterfaceReviewDecision
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}
