using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommandHandler : IRequestHandler<ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommandHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommand request, CancellationToken ct)
    {
        var version = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            throw new KeyNotFoundException("reference_data_version_not_found");
        }

        if (version.Status != BusinessReferenceDataVersionStatus.Draft || version.IsImmutable || !version.IsEditable)
        {
            throw new InvalidOperationException("draft_required_for_attribute_edit");
        }

        var definitions = NormalizeDefinitions(request.Definitions);
        version.AttributeDefinitions = definitions;
        version.UpdatedBy = request.ActorId;
        version.LastCorrelationId = request.CorrelationId;

        var expectedToken = string.IsNullOrWhiteSpace(request.ExpectedConcurrencyToken)
            ? version.ConcurrencyToken
            : request.ExpectedConcurrencyToken.Trim();

        var updated = await _repository.UpdateVersionAsync(version, expectedToken, ct);
        if (!updated)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        return Response<BusinessReferenceDataVersionDetailModel>.Success(BusinessReferenceDataModelMapper.ToVersionDetail(version));
    }

    private static List<BusinessReferenceDataAttributeDefinition> NormalizeDefinitions(IReadOnlyList<BusinessReferenceDataAttributeDefinitionInputModel> definitions)
    {
        var result = new List<BusinessReferenceDataAttributeDefinition>(definitions.Count);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            var code = NormalizeRequired(definition.AttributeCode, "attribute_code_required");
            var displayName = NormalizeRequired(definition.DisplayName, "attribute_display_name_required");
            if (!seenCodes.Add(code))
            {
                throw new InvalidOperationException("duplicate_attribute_code");
            }

            var dataType = NormalizeDataType(definition.DataType);
            result.Add(new BusinessReferenceDataAttributeDefinition
            {
                AttributeCode = code,
                DisplayName = displayName,
                DataType = dataType,
                IsRequired = definition.IsRequired
            });
        }

        return result
            .OrderBy(x => x.AttributeCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeDataType(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? "string" : raw.Trim().ToLowerInvariant();
        return value switch
        {
            "string" or "number" or "decimal" or "boolean" or "date" or "datetime" => value,
            _ => throw new InvalidOperationException("invalid_attribute_data_type")
        };
    }

    private static string NormalizeRequired(string? raw, string errorCode)
    {
        var value = raw?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorCode);
        }

        return value;
    }
}
