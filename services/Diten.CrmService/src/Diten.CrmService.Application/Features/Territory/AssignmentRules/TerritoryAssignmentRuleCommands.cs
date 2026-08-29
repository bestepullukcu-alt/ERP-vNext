using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules;

/// <summary>FU03 criteria input. Mirrors the typed whitelist on the entity — there is no free-form expression field,
/// so an unknown property is rejected by model binding/validation instead of being stored unvalidated.</summary>
public sealed record TerritoryRuleCriteriaInput(
    IReadOnlyList<string>? CountryRefs = null,
    IReadOnlyList<string>? CityRefs = null,
    IReadOnlyList<string>? DistrictRefs = null,
    IReadOnlyList<string>? AccountTypes = null,
    IReadOnlyList<string>? AccountCategories = null,
    IReadOnlyList<string>? AccountStatuses = null,
    IReadOnlyList<Guid>? IncludeAccountIds = null,
    IReadOnlyList<Guid>? ExcludeAccountIds = null);

/// <summary>Creates an assignment rule on a DRAFT model. TenantId is server-resolved and is NOT a field here.</summary>
public sealed record CreateTerritoryAssignmentRuleCommand(
    Guid ModelId,
    string RuleCode,
    string Name,
    Guid TerritoryId,
    string RuleType,
    string ConflictPolicy,
    int Priority,
    bool IsEnabled,
    TerritoryRuleCriteriaInput? Criteria,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? CorrelationId) : IRequest<Response<Guid>>;

/// <summary>Updates an assignment rule on a DRAFT model. RuleCode is immutable after creation.</summary>
public sealed record UpdateTerritoryAssignmentRuleCommand(
    Guid ModelId,
    Guid RuleId,
    string Name,
    Guid TerritoryId,
    string RuleType,
    string ConflictPolicy,
    int Priority,
    bool IsEnabled,
    TerritoryRuleCriteriaInput? Criteria,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? CorrelationId) : IRequest<Response<bool>>;

/// <summary>Soft-deletes a rule on a DRAFT model (no hard delete anywhere in MOD-0151).</summary>
public sealed record SoftDeleteTerritoryAssignmentRuleCommand(
    Guid ModelId,
    Guid RuleId,
    string? Reason,
    string? CorrelationId) : IRequest<Response<bool>>;

/// <summary>
/// Runs the assignment rules of a model against the tenant's accounts and returns candidates + conflicts.
/// <b>Side-effect free by construction:</b> the handler has no assignment repository and no account writer, so it
/// cannot persist an AccountTerritoryAssignment or mutate an Account even by mistake. Apply is FU05.
/// </summary>
public sealed record PreviewTerritoryAssignmentsCommand(
    Guid ModelId,
    Guid? RuleId,
    int? MaxAccounts,
    string? CorrelationId) : IRequest<Response<TerritoryAssignmentPreviewDto>>;
