using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;

namespace Diten.MdmService.Api.Contracts.ProductAbbreviationWorkItems;

public sealed record ProductAbbreviationWorkItemActionRequest(
    string? ProviderCode,
    ProductAbbreviationWorkItemActionPayload? Payload)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? UnmappedFields { get; init; }

    public bool HasUnmappedFields => UnmappedFields is { Count: > 0 }
                                     || Payload?.HasUnmappedFields == true;
}

public sealed record ProductAbbreviationWorkItemActionPayload(
    int? ExpectedVersion = null,
    string? Reason = null,
    string? ReasonCode = null,
    string? Note = null,
    DateTimeOffset? PlannedDate = null,
    Guid? AssigneeUserId = null,
    Guid? WaitingOnUserId = null,
    string? Comment = null,
    string? EvidenceRef = null,
    string? TargetPrincipalId = null,
    string? IdempotencyKey = null)
{
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? UnmappedFields { get; init; }

    public bool HasUnmappedFields
        => UnmappedFields is { Count: > 0 }
           || !string.IsNullOrEmpty(ReasonCode)
           || PlannedDate.HasValue
           || AssigneeUserId.HasValue
           || WaitingOnUserId.HasValue
           || !string.IsNullOrEmpty(Comment)
           || !string.IsNullOrEmpty(EvidenceRef)
           || !string.IsNullOrEmpty(TargetPrincipalId)
           || !string.IsNullOrEmpty(IdempotencyKey);
}

public static class ProductAbbreviationWorkItemEnvelopeFactory
{
    public static ProductAbbreviationWorkItemEnvelope<T> From<T>(
        ProductAbbreviationWorkItemOperationResult<T> result)
        => result.IsSuccessful
            ? ProductAbbreviationWorkItemEnvelope<T>.Success(result.Data!, result.StatusCode)
            : ProductAbbreviationWorkItemEnvelope<T>.Fail(result.StatusCode, result.ReasonCode!);
}
