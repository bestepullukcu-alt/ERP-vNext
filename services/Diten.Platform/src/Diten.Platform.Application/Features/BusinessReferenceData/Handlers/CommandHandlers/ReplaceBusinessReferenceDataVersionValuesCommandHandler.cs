using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.CommandHandlers;

public sealed class ReplaceBusinessReferenceDataVersionValuesCommandHandler : IRequestHandler<ReplaceBusinessReferenceDataVersionValuesCommand, Response<BusinessReferenceDataVersionDetailModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public ReplaceBusinessReferenceDataVersionValuesCommandHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionDetailModel>> Handle(ReplaceBusinessReferenceDataVersionValuesCommand request, CancellationToken ct)
    {
        var version = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            throw new KeyNotFoundException("reference_data_version_not_found");
        }

        if (version.Status != BusinessReferenceDataVersionStatus.Draft || version.IsImmutable || !version.IsEditable)
        {
            throw new InvalidOperationException("draft_required_for_value_edit");
        }

        var normalizedValues = NormalizeValues(request.Values);
        version.Values = normalizedValues;
        version.DeprecatedValuesEffectiveCount = normalizedValues.Count(x => !x.IsActive);
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

    private static List<BusinessReferenceDataValue> NormalizeValues(IReadOnlyList<BusinessReferenceDataVersionValueInputModel> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("values_required");
        }

        var duplicateCodes = values
            .Select(x => NormalizeRequired(x.Code, "value_code_required"))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateCodes.Count > 0)
        {
            throw new InvalidOperationException("duplicate_value_code");
        }

        var result = new List<BusinessReferenceDataValue>(values.Count);
        foreach (var value in values)
        {
            var code = NormalizeRequired(value.Code, "value_code_required");
            var label = NormalizeRequired(value.Label, "value_label_required");
            if (value.SortOrder < 0)
            {
                throw new InvalidOperationException("invalid_sort_order");
            }

            var parentValueCode = NormalizeOptional(value.ParentValueCode);
            if (!string.IsNullOrWhiteSpace(parentValueCode)
                && string.Equals(parentValueCode, code, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("invalid_parent_reference");
            }

            var attributes = value.Attributes?
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(
                    x => x.Key.Trim(),
                    x => x.Value?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            result.Add(new BusinessReferenceDataValue
            {
                ValueCode = code,
                DisplayName = label,
                Description = NormalizeOptional(value.Description),
                IsActive = value.IsActive,
                SortOrder = value.SortOrder,
                ParentValueCode = parentValueCode,
                Attributes = attributes is { Count: > 0 } ? attributes : null
            });
        }

        var knownCodes = result
            .Select(x => x.ValueCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in result)
        {
            if (!string.IsNullOrWhiteSpace(item.ParentValueCode)
                && !knownCodes.Contains(item.ParentValueCode))
            {
                throw new InvalidOperationException("invalid_parent_reference");
            }
        }

        EnsureHierarchyAcyclic(result);

        return result
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ValueCode, StringComparer.OrdinalIgnoreCase)
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

    private static void EnsureHierarchyAcyclic(IReadOnlyList<BusinessReferenceDataValue> values)
    {
        var nodes = values.ToDictionary(x => x.ValueCode, x => x, StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string code)
        {
            if (visited.Contains(code))
            {
                return false;
            }

            if (!visiting.Add(code))
            {
                return true;
            }

            var parent = nodes[code].ParentValueCode;
            if (!string.IsNullOrWhiteSpace(parent) && nodes.ContainsKey(parent))
            {
                if (Visit(parent))
                {
                    return true;
                }
            }

            visiting.Remove(code);
            visited.Add(code);
            return false;
        }

        foreach (var code in nodes.Keys)
        {
            if (Visit(code))
            {
                throw new InvalidOperationException("hierarchy_cycle_detected");
            }
        }
    }
}
