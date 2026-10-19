using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

public sealed class ProductLegalEntityScopeCandidateFacade
{
    private const int MaximumCandidates = ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot;
    private const string ProductItemSkuMasterModuleCode = "product-item-sku-master";
    private const int MaximumPermissionKeyLength = 200;

    private readonly ITrustedLegalEntityScopeProvider _provider;
    private readonly ILegalEntityRepository _legalEntityRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IProductIdentityActorContext _actorContext;
    private readonly Dictionary<CandidateMemoKey, Task<ProductLegalEntityScopeCandidateResult>> _memo = [];
    private readonly object _memoLock = new();

    public ProductLegalEntityScopeCandidateFacade(
        ITrustedLegalEntityScopeProvider provider,
        ILegalEntityRepository legalEntityRepository,
        ITenantContext tenantContext,
        IProductIdentityActorContext actorContext)
    {
        _provider = provider;
        _legalEntityRepository = legalEntityRepository;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
    }

    public Task<ProductLegalEntityScopeCandidateResult> ResolveAsync(
        string moduleCode,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return Task.FromResult(ProductLegalEntityScopeCandidateResult.Fail(
                400,
                "TENANT_CONTEXT_REQUIRED"));
        }
        string actorId;
        try
        {
            actorId = _actorContext.ActorId;
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(ProductLegalEntityScopeCandidateResult.Fail(
                401,
                "TRUSTED_SUBJECT_REQUIRED"));
        }
        if (!Guid.TryParse(actorId, out var subjectId) || subjectId == Guid.Empty)
        {
            return Task.FromResult(ProductLegalEntityScopeCandidateResult.Fail(
                401,
                "TRUSTED_SUBJECT_REQUIRED"));
        }
        if (!string.Equals(moduleCode, ProductItemSkuMasterModuleCode, StringComparison.Ordinal)
            || !IsValidPermissionKey(permissionKey))
        {
            return Task.FromResult(ProductLegalEntityScopeCandidateResult.Fail(
                400,
                "LEGAL_ENTITY_SCOPE_REQUEST_INVALID"));
        }

        var key = new CandidateMemoKey(
            _tenantContext.TenantId,
            subjectId,
            moduleCode,
            permissionKey);

        lock (_memoLock)
        {
            if (!_memo.TryGetValue(key, out var result))
            {
                result = ResolveCoreAsync(key, cancellationToken);
                _memo.Add(key, result);
            }

            return result;
        }
    }

    private async Task<ProductLegalEntityScopeCandidateResult> ResolveCoreAsync(
        CandidateMemoKey key,
        CancellationToken cancellationToken)
    {
        var providerResult = await _provider.ResolveAsync(
            key.TenantId,
            key.SubjectId,
            key.ModuleCode,
            key.PermissionKey,
            cancellationToken);

        if (!providerResult.IsSuccessful)
        {
            return MapProviderFailure(providerResult);
        }
        if (!IsValidProviderSuccess(providerResult, key))
        {
            return ProductLegalEntityScopeCandidateResult.Fail(
                503,
                "LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID");
        }

        var localEntities = await _legalEntityRepository.GetReferenceableByIdsAsync(
            providerResult.LegalEntityIds,
            cancellationToken);
        var localIds = localEntities.Select(entity => entity.Id).ToHashSet();
        var candidates = providerResult.LegalEntityIds
            .Where(localIds.Contains)
            .ToArray();

        return ProductLegalEntityScopeCandidateResult.Success(
            key.TenantId,
            key.SubjectId,
            key.ModuleCode,
            key.PermissionKey,
            providerResult.EvaluatedAtUtc!.Value,
            candidates);
    }

    private static bool IsValidProviderSuccess(
        TrustedLegalEntityScopeProviderResult result,
        CandidateMemoKey expected)
    {
        if (result.StatusCode != 200
            || result.FailureCode is not null
            || result.TenantId != expected.TenantId
            || result.SubjectId != expected.SubjectId
            || !string.Equals(result.ModuleCode, expected.ModuleCode, StringComparison.Ordinal)
            || !string.Equals(result.PermissionKey, expected.PermissionKey, StringComparison.Ordinal)
            || result.EvaluatedAtUtc is null
            || result.EvaluatedAtUtc.Value.Offset != TimeSpan.Zero
            || result.LegalEntityIds is null
            || result.LegalEntityIds.Count > MaximumCandidates
            || result.LegalEntityIds.Any(id => id == Guid.Empty)
            || result.LegalEntityIds.Distinct().Count() != result.LegalEntityIds.Count)
        {
            return false;
        }

        var canonical = result.LegalEntityIds
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal);
        return result.LegalEntityIds.SequenceEqual(canonical);
    }

    private static ProductLegalEntityScopeCandidateResult MapProviderFailure(
        TrustedLegalEntityScopeProviderResult result)
    {
        return result.StatusCode switch
        {
            400 => ProductLegalEntityScopeCandidateResult.Fail(
                400,
                "LEGAL_ENTITY_SCOPE_REQUEST_INVALID"),
            401 => ProductLegalEntityScopeCandidateResult.Fail(
                401,
                "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED"),
            403 => ProductLegalEntityScopeCandidateResult.Fail(
                403,
                "LEGAL_ENTITY_SCOPE_FORBIDDEN"),
            504 => ProductLegalEntityScopeCandidateResult.Fail(
                504,
                "LEGAL_ENTITY_SCOPE_PROVIDER_TIMEOUT"),
            _ => ProductLegalEntityScopeCandidateResult.Fail(
                503,
                "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE")
        };
    }

    private static bool IsValidPermissionKey(string? value)
    {
        if (value is null || value.Length is < 1 or > MaximumPermissionKeyLength)
        {
            return false;
        }

        var segments = value.Split('.');
        return segments.Length >= 3 && segments.All(IsValidKebabSegment);
    }

    private static bool IsValidKebabSegment(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var character in value)
        {
            var isLowerAlphaNumeric = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (!isLowerAlphaNumeric && character != '-')
            {
                return false;
            }
            if (character == '-' && previousHyphen)
            {
                return false;
            }

            previousHyphen = character == '-';
        }

        return true;
    }

    private sealed record CandidateMemoKey(
        Guid TenantId,
        Guid SubjectId,
        string ModuleCode,
        string PermissionKey);
}

public sealed record ProductLegalEntityScopeCandidateResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    Guid TenantId,
    Guid SubjectId,
    string? ModuleCode,
    string? PermissionKey,
    DateTimeOffset? EvaluatedAtUtc,
    IReadOnlyList<Guid> LegalEntityIds)
{
    public static ProductLegalEntityScopeCandidateResult Success(
        Guid tenantId,
        Guid subjectId,
        string moduleCode,
        string permissionKey,
        DateTimeOffset evaluatedAtUtc,
        IReadOnlyList<Guid> legalEntityIds) => new(
        true,
        200,
        null,
        tenantId,
        subjectId,
        moduleCode,
        permissionKey,
        evaluatedAtUtc,
        legalEntityIds);

    public static ProductLegalEntityScopeCandidateResult Fail(int statusCode, string failureCode) => new(
        false,
        statusCode,
        failureCode,
        Guid.Empty,
        Guid.Empty,
        null,
        null,
        null,
        []);
}
