using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.PayrollSources;

public sealed record PayrollExternalSystemProfileRequest(
    string Code,
    string DisplayName,
    PayrollProviderFamily ProviderFamily,
    string ExternalPayrollSystemId,
    PayrollSourceLifecycleState LifecycleState,
    string? ConnectionProfileReference,
    string? SupportOwner,
    string? Notes);

public sealed record PayrollContractProfileRequest(
    string ContractVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<PayrollSupportedObjectType> SupportedObjectTypes,
    IReadOnlyList<string> StatusVocabulary,
    IReadOnlyList<string>? ErrorVocabulary,
    string? CorrelationIdPattern);

public sealed record PayrollEmployeeReferenceMapRequest(
    string ExternalEmployeeReference,
    Guid? PersonReferenceId,
    Guid? OrganizationUnitReferenceId,
    Guid? PositionReferenceId,
    PayrollReferenceMappingState MappingState);

public sealed record PayrollCycleReferenceRequest(
    string ExternalPayCycleId,
    string CycleCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PayrollCycleProcessingState ProcessingState,
    string? CorrelationId);

public sealed record PayrollResultReferenceRequest(
    Guid PayrollCycleReferenceId,
    string ExternalPayrollResultId,
    string ResultVersion,
    PayrollResultState ResultState,
    DateTimeOffset? PublishedAt,
    string? CorrelationId);

public sealed record PayrollSourceHealthSnapshotRequest(
    PayrollSourceHealthState HealthState,
    DateTimeOffset CheckedAt,
    string? RedactedMessage,
    string? CorrelationId);

public sealed record PayrollExternalSystemProfileDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string DisplayName,
    PayrollProviderFamily ProviderFamily,
    string ExternalPayrollSystemId,
    PayrollSourceLifecycleState LifecycleState,
    string? ConnectionProfileReference,
    string? SupportOwner,
    string? Notes,
    Guid? ContractProfileId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PayrollContractProfileDto(
    Guid Id,
    Guid PayrollExternalSystemProfileId,
    string ContractVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<PayrollSupportedObjectType> SupportedObjectTypes,
    IReadOnlyList<string> StatusVocabulary,
    IReadOnlyList<string> ErrorVocabulary,
    string? CorrelationIdPattern);

public sealed record PayrollEmployeeReferenceMapDto(
    Guid Id,
    Guid PayrollExternalSystemProfileId,
    string ExternalEmployeeReference,
    Guid? PersonReferenceId,
    Guid? OrganizationUnitReferenceId,
    Guid? PositionReferenceId,
    PayrollReferenceMappingState MappingState,
    DateTimeOffset? LastValidatedAt);

public sealed record PayrollCycleReferenceDto(
    Guid Id,
    Guid PayrollExternalSystemProfileId,
    string ExternalPayCycleId,
    string CycleCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PayrollCycleProcessingState ProcessingState,
    string? CorrelationId);

public sealed record PayrollResultReferenceDto(
    Guid Id,
    Guid PayrollExternalSystemProfileId,
    Guid PayrollCycleReferenceId,
    string ExternalPayrollResultId,
    string ResultVersion,
    PayrollResultState ResultState,
    DateTimeOffset? PublishedAt,
    string? CorrelationId);

public sealed record PayrollSourceHealthSnapshotDto(
    Guid Id,
    Guid PayrollExternalSystemProfileId,
    PayrollSourceHealthState HealthState,
    DateTimeOffset CheckedAt,
    string? RedactedMessage,
    string? CorrelationId);

public static class PayrollSourceMapper
{
    public static PayrollExternalSystemProfileDto ToDto(PayrollExternalSystemProfile entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.DisplayName,
            entity.ProviderFamily,
            entity.ExternalPayrollSystemId,
            entity.LifecycleState,
            entity.ConnectionProfileReference,
            entity.SupportOwner,
            entity.Notes,
            entity.ContractProfileId,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static PayrollContractProfileDto ToDto(PayrollContractProfile entity) =>
        new(
            entity.Id,
            entity.PayrollExternalSystemProfileId,
            entity.ContractVersion,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.SupportedObjectTypes,
            entity.StatusVocabulary,
            entity.ErrorVocabulary,
            entity.CorrelationIdPattern);

    public static PayrollEmployeeReferenceMapDto ToDto(PayrollEmployeeReferenceMap entity) =>
        new(
            entity.Id,
            entity.PayrollExternalSystemProfileId,
            entity.ExternalEmployeeReference,
            entity.PersonReferenceId,
            entity.OrganizationUnitReferenceId,
            entity.PositionReferenceId,
            entity.MappingState,
            entity.LastValidatedAt);

    public static PayrollCycleReferenceDto ToDto(PayrollCycleReference entity) =>
        new(
            entity.Id,
            entity.PayrollExternalSystemProfileId,
            entity.ExternalPayCycleId,
            entity.CycleCode,
            entity.PeriodStart,
            entity.PeriodEnd,
            entity.ProcessingState,
            entity.CorrelationId);

    public static PayrollResultReferenceDto ToDto(PayrollResultReference entity) =>
        new(
            entity.Id,
            entity.PayrollExternalSystemProfileId,
            entity.PayrollCycleReferenceId,
            entity.ExternalPayrollResultId,
            entity.ResultVersion,
            entity.ResultState,
            entity.PublishedAt,
            entity.CorrelationId);

    public static PayrollSourceHealthSnapshotDto ToDto(PayrollSourceHealthSnapshot entity) =>
        new(
            entity.Id,
            entity.PayrollExternalSystemProfileId,
            entity.HealthState,
            entity.CheckedAt,
            entity.RedactedMessage,
            entity.CorrelationId);
}
