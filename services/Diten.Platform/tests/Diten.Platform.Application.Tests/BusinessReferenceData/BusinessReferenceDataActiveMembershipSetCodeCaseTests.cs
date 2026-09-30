using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-CL-BE-2b — the REAL <see cref="BusinessReferenceDataActiveMembershipService"/> over a consumer query that matches the
/// set code EXACTLY, as the Mongo repository does (Filter.Eq on the trimmed code). The service used to upper-case the set
/// code, so every lower-case set (e.g. <c>evidence-type</c>) was "not found" while it was published.
/// </summary>
public sealed class BusinessReferenceDataActiveMembershipSetCodeCaseTests
{
    private static BusinessReferenceDataActiveMembershipService Service() => new(new ExactConsumerQuery());

    [Fact]
    public async Task A_lower_case_set_is_found_and_an_active_value_is_accepted()
    {
        var result = await Service().ValidateActiveValueAsync("evidence-type", "smpc-pil");

        Assert.True(result.IsActive, result.Message);
        Assert.Equal("evidence-type", result.SetCode);
        Assert.Null(result.ReasonCode);
    }

    [Fact]
    public async Task Value_codes_still_match_case_insensitively()
    {
        Assert.True((await Service().ValidateActiveValueAsync(" evidence-type ", "SMPC-PIL")).IsActive);
    }

    [Fact]
    public async Task An_upper_case_set_is_still_found()
    {
        var result = await Service().ValidateActiveValuesAsync("COUNTRY_CODES", ["tr", "UZ"]);

        Assert.True(result.IsActive, result.Message);
        Assert.Equal("COUNTRY_CODES", result.SetCode);
    }

    [Fact]
    public async Task A_passive_value_is_not_active()
    {
        var result = await Service().ValidateActiveValueAsync("evidence-type", "retired-type");

        Assert.False(result.IsActive);
        Assert.Equal("reference_value_not_active", result.ReasonCode);
    }

    [Fact]
    public async Task A_missing_set_reports_set_not_found_with_the_original_code()
    {
        var result = await Service().ValidateActiveValueAsync("no-such-set", "x");

        Assert.False(result.IsActive);
        Assert.Equal("reference_data_set_not_found", result.ReasonCode);
        Assert.Equal("no-such-set", result.SetCode);
        Assert.Contains("set_code=no-such-set", result.Message);
    }

    [Fact]
    public async Task Ensure_set_has_active_values_finds_a_lower_case_set()
    {
        Assert.True((await Service().EnsureSetHasActiveValuesAsync("evidence-type")).IsActive);
    }

    /// <summary>Matches the set code ordinally after a trim — exactly like the repository's Filter.Eq.</summary>
    internal sealed class ExactConsumerQuery : IBusinessReferenceDataConsumerQueryService
    {
        private static readonly Dictionary<string, BusinessReferenceDataPublishedValuesModel> Sets = new(StringComparer.Ordinal)
        {
            ["evidence-type"] = Published("evidence-type", ("smpc-pil", true), ("clinical-study", true), ("retired-type", false)),
            ["COUNTRY_CODES"] = Published("COUNTRY_CODES", ("TR", true), ("UZ", true))
        };

        public Task<BusinessReferenceDataPublishedValuesModel> GetPublishedValuesAsync(string setCode, string? scopeKey,
            CancellationToken ct = default)
            => Sets.TryGetValue(setCode.Trim(), out var set)
                ? Task.FromResult(set)
                : throw new KeyNotFoundException("reference_data_set_not_found");

        public Task<BusinessReferenceDataValuesLookupModel> GetValuesAsync(string setCode, string? scopeKey, int? versionNumber,
            DateTimeOffset? asOfDate, bool includeDeprecated, bool includeAttributes, bool includeMappings,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<BusinessReferenceDataHierarchyLookupModel> GetHierarchyAsync(string setCode, string? scopeKey,
            int? versionNumber, DateTimeOffset? asOfDate, bool includeDeprecated, bool includeAttributes,
            bool includeMappings, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<BusinessReferenceDataUsageRegistrationResultModel> RegisterUsageAsync(string setCode,
            string consumerModule, string consumerName, string? consumerEndpoint, string? scopeType, string? scopeKey,
            int? versionPin, DateTimeOffset? asOfDate, string? resolutionMode, string? criticality, string? notes,
            string actorId, string correlationId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<BusinessReferenceDataUsageRegistrationListModel> GetUsageRegistrationsAsync(string setCode,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> DeactivateUsageRegistrationAsync(Guid usageRegistrationId, string actorId, string correlationId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<int> DeactivateUsageRegistrationsBulkAsync(IReadOnlyCollection<Guid> usageRegistrationIds,
            string actorId, string correlationId, CancellationToken ct = default) => throw new NotSupportedException();

        private static BusinessReferenceDataPublishedValuesModel Published(string code, params (string Value, bool Active)[] values)
            => new(code, 1, DateTimeOffset.UtcNow, values
                .Select((v, i) => new BusinessReferenceDataPublishedValueItemModel(v.Value, v.Value, null, v.Active, i, null))
                .ToList());
    }
}
