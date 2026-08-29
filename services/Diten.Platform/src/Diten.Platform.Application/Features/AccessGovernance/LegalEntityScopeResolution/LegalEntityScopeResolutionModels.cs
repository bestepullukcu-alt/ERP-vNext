namespace Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;

public sealed record TrustedLegalEntityScopeResolution(
    Guid TenantId, Guid SubjectId, string ModuleCode, string PermissionKey,
    DateTimeOffset EvaluatedAtUtc, IReadOnlyList<Guid> LegalEntityIds);
