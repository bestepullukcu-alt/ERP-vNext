using System.Text.Json.Serialization;

namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>RECORD-CODE-ALLOCATE 1.0.0 §3 — the 200 <c>data</c>: the code and the counter number it carries, nothing else.</summary>
public sealed record RecordCodeAllocationResult(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("sequence")] long Sequence);
