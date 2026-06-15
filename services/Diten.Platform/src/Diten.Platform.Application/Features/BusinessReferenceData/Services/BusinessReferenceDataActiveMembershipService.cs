using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed record BusinessReferenceDataActiveMembershipResult(
    bool IsActive,
    string SetCode,
    string ValueCode,
    string? ReasonCode,
    string Message);

public interface IBusinessReferenceDataActiveMembershipService
{
    Task<BusinessReferenceDataActiveMembershipResult> ValidateActiveValueAsync(
        string setCode,
        string valueCode,
        CancellationToken ct = default);

    Task<BusinessReferenceDataActiveMembershipResult> ValidateActiveValuesAsync(
        string setCode,
        IEnumerable<string> valueCodes,
        CancellationToken ct = default);

    Task<BusinessReferenceDataActiveMembershipResult> EnsureSetHasActiveValuesAsync(
        string setCode,
        CancellationToken ct = default);
}

public sealed class BusinessReferenceDataActiveMembershipService : IBusinessReferenceDataActiveMembershipService
{
    private readonly IBusinessReferenceDataConsumerQueryService _consumerQueryService;

    public BusinessReferenceDataActiveMembershipService(IBusinessReferenceDataConsumerQueryService consumerQueryService)
    {
        _consumerQueryService = consumerQueryService;
    }

    public async Task<BusinessReferenceDataActiveMembershipResult> ValidateActiveValueAsync(
        string setCode,
        string valueCode,
        CancellationToken ct = default)
    {
        var normalizedValue = Normalize(valueCode);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return Block(setCode, normalizedValue, "reference_value_required");
        }

        return await ValidateActiveValuesAsync(setCode, [normalizedValue], ct);
    }

    public async Task<BusinessReferenceDataActiveMembershipResult> ValidateActiveValuesAsync(
        string setCode,
        IEnumerable<string> valueCodes,
        CancellationToken ct = default)
    {
        var normalizedSet = Normalize(setCode);
        var requested = valueCodes
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(normalizedSet))
        {
            return Block(normalizedSet, string.Join(",", requested), "reference_set_required");
        }

        if (requested.Count == 0)
        {
            return Block(normalizedSet, string.Empty, "reference_value_required");
        }

        BusinessReferenceDataPublishedValuesModel published;
        try
        {
            published = await _consumerQueryService.GetPublishedValuesAsync(normalizedSet, null, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return Block(normalizedSet, string.Join(",", requested), ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Block(normalizedSet, string.Join(",", requested), ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Block(normalizedSet, string.Join(",", requested), "reference_dependency_unavailable");
        }

        var active = published.Items
            .Where(x => x.IsActive)
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (active.Count == 0)
        {
            return Block(normalizedSet, string.Join(",", requested), "reference_set_has_no_active_values");
        }

        var missing = requested.Where(x => !active.Contains(x)).ToList();
        if (missing.Count > 0)
        {
            return Block(normalizedSet, string.Join(",", missing), "reference_value_not_active");
        }

        return new BusinessReferenceDataActiveMembershipResult(
            true,
            normalizedSet,
            string.Join(",", requested),
            null,
            "ok");
    }

    public async Task<BusinessReferenceDataActiveMembershipResult> EnsureSetHasActiveValuesAsync(
        string setCode,
        CancellationToken ct = default)
    {
        var normalizedSet = Normalize(setCode);
        if (string.IsNullOrWhiteSpace(normalizedSet))
        {
            return Block(normalizedSet, string.Empty, "reference_set_required");
        }

        try
        {
            var published = await _consumerQueryService.GetPublishedValuesAsync(normalizedSet, null, ct);
            return published.Items.Any(x => x.IsActive)
                ? new BusinessReferenceDataActiveMembershipResult(true, normalizedSet, string.Empty, null, "ok")
                : Block(normalizedSet, string.Empty, "reference_set_has_no_active_values");
        }
        catch (KeyNotFoundException ex)
        {
            return Block(normalizedSet, string.Empty, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Block(normalizedSet, string.Empty, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Block(normalizedSet, string.Empty, "reference_dependency_unavailable");
        }
    }

    private static BusinessReferenceDataActiveMembershipResult Block(string setCode, string valueCode, string reasonCode)
        => new(
            false,
            Normalize(setCode),
            Normalize(valueCode),
            Normalize(reasonCode).ToLowerInvariant(),
            $"BusinessReferenceData active membership failed: set_code={Normalize(setCode)}; value_code={Normalize(valueCode)}; reason={Normalize(reasonCode).ToLowerInvariant()}");

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
}
