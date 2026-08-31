using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public sealed class ValidateExternalContextReferenceRequest
{
    [JsonPropertyName("contractName")]
    public string? ContractName { get; init; }

    [JsonPropertyName("contractVersion")]
    public string? ContractVersion { get; init; }

    [JsonPropertyName("contextKind")]
    public string? ContextKind { get; init; }

    [JsonPropertyName("contextId")]
    public string? ContextId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
