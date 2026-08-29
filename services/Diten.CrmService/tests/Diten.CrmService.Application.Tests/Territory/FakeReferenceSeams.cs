using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>Configurable in-memory MOD-0048 seams for MOD-0151 tests. No network, no hardcoded reference fallback —
/// tests wire exactly the published values / attributes / catalog they want (including the "unpublished" case).</summary>
internal sealed class FakeReferenceValidator : IReferenceDataValidator
{
    // setCode -> (value -> status). A missing set yields SetMissing; a missing value yields InvalidValue.
    private readonly Dictionary<string, HashSet<string>> _sets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingSets = new(StringComparer.OrdinalIgnoreCase);

    public FakeReferenceValidator Publish(string setCode, params string[] values)
    {
        _sets[setCode] = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        return this;
    }

    public FakeReferenceValidator MarkMissing(string setCode)
    {
        _missingSets.Add(setCode);
        _sets.Remove(setCode);
        return this;
    }

    public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken cancellationToken)
    {
        if (_missingSets.Contains(setCode) || !_sets.TryGetValue(setCode, out var values))
        {
            return Task.FromResult(new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value));
        }

        var status = values.Contains(value) ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue;
        return Task.FromResult(new ReferenceValidationResult(status, setCode, value));
    }
}

internal sealed class FakeMetadataReader : IReferenceMetadataReader
{
    // setCode -> (value -> attributes). null attributes => value absent / set unpublished (returns null).
    private readonly Dictionary<(string, string), IReadOnlyDictionary<string, string>?> _attrs = new();

    public FakeMetadataReader Set(string setCode, string value, IReadOnlyDictionary<string, string>? attributes)
    {
        _attrs[(setCode.ToLowerInvariant(), value.ToLowerInvariant())] = attributes;
        return this;
    }

    public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken cancellationToken)
        => Task.FromResult(_attrs.TryGetValue((setCode.ToLowerInvariant(), value.ToLowerInvariant()), out var a) ? a : null);
}

internal sealed class FakeCatalogReader : IReferenceDataCatalogReader
{
    private readonly Dictionary<string, ReferenceSetSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public FakeCatalogReader Set(string setCode, ReferenceSetSnapshot snapshot)
    {
        _snapshots[setCode] = snapshot;
        return this;
    }

    public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
        => Task.FromResult(_snapshots.TryGetValue(setCode, out var s) ? s : ReferenceSetSnapshot.NotPublished(setCode));
}
