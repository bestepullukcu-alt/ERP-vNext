using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.Models;

/// <summary>FU02A business scope selection input. <c>ScopeType</c> must be <c>business-unit</c> (brand/product rejected);
/// <c>ScopeCode</c> is a MOD-0048 published business-unit value code.</summary>
public sealed record TerritoryBusinessScopeInput(string ScopeType, string ScopeCode);

/// <summary>Creates a DRAFT TerritoryModel. TenantId is server-resolved and is NOT a field here (never from payload).
/// Status is always <c>draft</c> (FU01 does no lifecycle transition); it is still validated against
/// <c>territory-model-status</c> so an unpublished set fails closed.</summary>
public sealed record CreateTerritoryModelCommand(
    string ModelCode,
    string Name,
    string? CountryScope,
    string? DivisionScope,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid? BasedOnModelId,
    string? ChangeReason,
    string? CorrelationId,
    IReadOnlyList<TerritoryBusinessScopeInput>? BusinessScopes = null) : IRequest<Response<Guid>>;

/// <summary>Updates a DRAFT TerritoryModel. Non-draft models are immutable in FU01 (409). Status is not changed here.</summary>
public sealed record UpdateTerritoryModelCommand(
    Guid Id,
    string Name,
    string? CountryScope,
    string? DivisionScope,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ChangeReason,
    string? CorrelationId,
    IReadOnlyList<TerritoryBusinessScopeInput>? BusinessScopes = null) : IRequest<Response<bool>>;

public sealed record ActivateTerritoryModelCommand(Guid Id, string? Reason, string? CorrelationId)
    : IRequest<Response<bool>>;

public sealed record DeactivateTerritoryModelCommand(Guid Id, string? Reason, string? CorrelationId)
    : IRequest<Response<bool>>;

public sealed record ArchiveTerritoryModelCommand(Guid Id, string? Reason, string? CorrelationId)
    : IRequest<Response<bool>>;

public sealed record SoftDeleteDraftTerritoryModelCommand(Guid Id, string? Reason, string? CorrelationId)
    : IRequest<Response<bool>>;
