using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Queries;

// FEFO-BUNDLE v1 §4 reads served by F1. Raw query strings: the handler validates them (400 INVALID_REQUEST).

/// <summary>§4.7 getPolicy — the company's policy, or the defaults at version 0 (<c>isDefault: true</c>).</summary>
public sealed record GetExpiryPolicyQuery : IRequest<Response<ExpiryPolicyDto>>;

/// <summary>§4.8 getPolicyHistory — newest first.</summary>
public sealed record GetExpiryPolicyHistoryQuery(string? Page, string? PageSize) : IRequest<Response<ExpiryPolicyHistoryPageDto>>;

/// <summary>§4.5 listExceptions.</summary>
public sealed record ListExpiryExceptionsQuery(
    string? Type,
    IReadOnlyList<string> Status,
    string? Late,
    string? WarehouseId,
    string? AssignedToMe,
    string? Q,
    string? Sort,
    string? Page,
    string? PageSize) : IRequest<Response<ExpiryExceptionPageDto>>;

/// <summary>§4.6 getException.</summary>
public sealed record GetExpiryExceptionQuery(string ExceptionId) : IRequest<Response<ExpiryExceptionDetailDto>>;
