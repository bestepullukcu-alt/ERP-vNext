using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.ExternalContextReferences;


public sealed record ValidateExternalContextReferenceQuery(
    string? ContractName,
    string? ContractVersion,
    string? ContextKind,
    string? ContextId,
    bool HasAdditionalProperties)
    : IRequest<Response<ExternalContextReferenceResponse>>;
