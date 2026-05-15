using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

public sealed record AuditRequestMetadata(
    AuditCategory Category,
    AuditOperation Operation,
    string EntityType,
    Guid? EntityId = null,
    string? SourceModule = null,
    bool IsPlatformGlobal = false,
    int Sequence = 0,
    Guid? CorrelationId = null,
    Guid? TargetTenantId = null,
    IReadOnlyDictionary<string, object?>? BeforeState = null,
    IReadOnlyDictionary<string, object?>? AfterState = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    bool IsMetaAudit = false);
