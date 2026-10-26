namespace Diten.SafetyStockService.Domain;

public sealed record SeriesKey(Guid SkuId, Guid WarehouseId);

public sealed record PolicyReadModel(
    Guid Id,
    Guid TenantId,
    Guid LegalEntityId,
    SeriesKey Series,
    string State,
    long Version);

public sealed record PolicyRevisionReadModel(
    Guid Id,
    Guid PolicyId,
    Guid TenantId,
    Guid LegalEntityId,
    SeriesKey Series,
    string State,
    long Version);

public sealed record PolicyEvaluationReadModel(
    Guid Id,
    Guid PolicyId,
    Guid TenantId,
    Guid LegalEntityId,
    SeriesKey Series,
    Guid RevisionId,
    string Method,
    decimal CandidateQuantity,
    string InputVersion);
