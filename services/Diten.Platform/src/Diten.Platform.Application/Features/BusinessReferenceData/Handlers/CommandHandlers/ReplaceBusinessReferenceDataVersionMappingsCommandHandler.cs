using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class ReplaceBusinessReferenceDataVersionMappingsCommandHandler : IRequestHandler<ReplaceBusinessReferenceDataVersionMappingsCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public ReplaceBusinessReferenceDataVersionMappingsCommandHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(ReplaceBusinessReferenceDataVersionMappingsCommand request, CancellationToken ct)
    {
        var version = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            throw new KeyNotFoundException("reference_data_version_not_found");
        }

        if (version.Status != BusinessReferenceDataVersionStatus.Draft || version.IsImmutable || !version.IsEditable)
        {
            throw new InvalidOperationException("draft_required_for_mapping_edit");
        }

        var mappings = NormalizeMappings(request.Mappings);
        version.Mappings = mappings;
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

    private static List<BusinessReferenceDataMapping> NormalizeMappings(IReadOnlyList<BusinessReferenceDataMappingInputModel> mappings)
    {
        var result = new List<BusinessReferenceDataMapping>(mappings.Count);
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in mappings)
        {
            var mappingKey = NormalizeRequired(input.MappingKey, "mapping_key_required");
            var source = NormalizeRequired(input.SourceValueCode, "mapping_source_required");
            var target = NormalizeRequired(input.TargetCode, "mapping_target_required");
            var dedupKey = $"{mappingKey}|{source}";
            if (!dedup.Add(dedupKey))
            {
                throw new InvalidOperationException("duplicate_mapping_key_source");
            }

            result.Add(new BusinessReferenceDataMapping
            {
                MappingKey = mappingKey,
                SourceValueCode = source,
                TargetCode = target,
                TargetLabel = NormalizeOptional(input.TargetLabel)
            });
        }

        return result
            .OrderBy(x => x.MappingKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceValueCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static string? NormalizeOptional(string? raw)
    {
        var value = raw?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
