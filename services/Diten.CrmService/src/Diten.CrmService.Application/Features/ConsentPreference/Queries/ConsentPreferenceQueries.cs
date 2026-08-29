using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using MediatR;

namespace Diten.CrmService.Application.Features.ConsentPreference.Queries;

/// <summary>Lists consent records for the tenant, optionally narrowed. Archived rows are included by default so
/// consent history (including withdrawals) stays visible; pass <paramref name="includeArchived"/>=false to exclude.</summary>
public sealed record ListConsentRecordsQuery(
    string? SubjectType = null,
    Guid? SubjectId = null,
    string? Channel = null,
    string? Purpose = null,
    string? ConsentStatus = null,
    bool IncludeArchived = true) : IRequest<Response<ConsentRecordListDto>>;

public sealed record GetConsentRecordQuery(Guid ConsentId) : IRequest<Response<ConsentRecordDto>>;

/// <summary>Read-only evaluation query. Never writes. Maps 1:1 onto the GET evaluate endpoint.</summary>
public sealed record EvaluateConsentQuery(
    string SubjectType,
    Guid SubjectId,
    string Channel,
    string Purpose,
    DateTimeOffset? EffectiveAt = null,
    string? ScopeType = null,
    Guid? ScopeId = null,
    bool IncludeDiagnostics = true) : IRequest<Response<ConsentEvaluationResult>>;

public sealed record ListPreferenceRecordsQuery(
    string? SubjectType = null,
    Guid? SubjectId = null,
    string? Channel = null,
    string? PreferenceType = null,
    bool IncludeArchived = true) : IRequest<Response<PreferenceRecordListDto>>;

public sealed record GetPreferenceRecordQuery(Guid PreferenceId) : IRequest<Response<PreferenceRecordDto>>;
