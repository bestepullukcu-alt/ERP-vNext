using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InterfaceLifecycleStatus
{
    Discovered = 0,
    PendingReview = 1,
    Confirmed = 2,
    Active = 3,
    Changed = 4,
    MissingInSource = 5,
    Deprecated = 6,
    Retired = 7,
    Rejected = 8
}
