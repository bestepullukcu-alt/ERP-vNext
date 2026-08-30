namespace Diten.MdmService.Application.Contracts.Authorization;

public interface ITrustedLegalEntityScopeProvider
{
    Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
        Guid expectedTenantId,
        Guid expectedSubjectId,
        string moduleCode,
        string permissionKey,
        CancellationToken cancellationToken = default);
}

public sealed record TrustedLegalEntityScopeProviderResult(
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
    public static TrustedLegalEntityScopeProviderResult Success(
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

    public static TrustedLegalEntityScopeProviderResult Fail(int statusCode, string failureCode) => new(
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
