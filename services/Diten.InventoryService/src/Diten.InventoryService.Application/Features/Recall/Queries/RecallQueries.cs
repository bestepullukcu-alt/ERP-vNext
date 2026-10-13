using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Queries;

// RECALL-BUNDLE v1 §4 reads served by R1. Raw query strings: the handler validates them (400 INVALID_REQUEST).

/// <summary>§4.20 getSettings — the company's settings, or the KR-G defaults at version 0 (<c>isDefault: true</c>).</summary>
public sealed record GetRecallSettingsQuery : IRequest<Response<RecallSettingsDto>>;

/// <summary>§4.21 getSettingsHistory — newest first.</summary>
public sealed record GetRecallSettingsHistoryQuery(string? Page, string? PageSize) : IRequest<Response<RecallSettingsHistoryPageDto>>;

/// <summary>§4.4 listCases (R1: real cases; drill rows join in R5).</summary>
public sealed record ListRecallCasesQuery(
    string? Type,
    IReadOnlyList<string> Status,
    string? Class,
    string? SkuId,
    string? OpenedFrom,
    string? Q,
    string? Sort,
    string? Page,
    string? PageSize) : IRequest<Response<RecallCasePageDto>>;

/// <summary>§4.5 getCase.</summary>
public sealed record GetRecallCaseQuery(string CaseId) : IRequest<Response<RecallCaseDetailDto>>;

/// <summary>§4.6 getCaseScope (R1: the hand-entered items; R2 adds the trace).</summary>
public sealed record GetRecallCaseScopeQuery(string CaseId) : IRequest<Response<RecallScopeViewDto>>;

/// <summary>§4.12 getCaseTimeline.</summary>
public sealed record GetRecallCaseTimelineQuery(string CaseId, string? Page, string? PageSize) : IRequest<Response<RecallTimelinePageDto>>;

/// <summary>§4.13 getLotRecallStatus — tenant-wide (CT-R7).</summary>
public sealed record GetLotRecallStatusQuery(string LotId) : IRequest<Response<RecallLotStatusDto>>;
