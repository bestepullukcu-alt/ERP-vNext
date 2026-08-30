using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public sealed record ExternalContextReferenceResponse(
    [property: JsonPropertyName("contractName")] string ContractName,
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("contextKind")] string ContextKind,
    [property: JsonPropertyName("contextId")] string ContextId);
