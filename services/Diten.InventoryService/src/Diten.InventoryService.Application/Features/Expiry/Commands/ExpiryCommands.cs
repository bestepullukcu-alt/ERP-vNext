using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Expiry.Enums;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Commands;

// FEFO-BUNDLE v1 §4 writes served by F1. Every command carries the request's idempotency key (header = body, 8–100) and, on
// a mutable record, the If-Match version. The controller parses the body; the handler holds every business rule.

/// <summary>§4.18 updatePolicy (PUT, full replacement; If-Match "0" creates the company's first row).</summary>
public sealed record UpdateExpiryPolicyCommand(ExpiryPolicyInput Policy, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<ExpiryPolicyDto>>;

/// <summary>§4.12 openException — manual "İstisna aç" (origin Manual).</summary>
public sealed record OpenExpiryExceptionCommand(
    ExpiryExceptionType Type,
    Guid LotId,
    Guid? WarehouseId,
    decimal? Quantity,
    string? Description,
    string IdempotencyKey) : IRequest<Response<ExpiryExceptionDetailDto>>
{
    /// <summary>FU-0176-F1-UOM (FEFO 1.1.0): the unit of <see cref="Quantity"/>; omitted → read from the lot's stock; unreadable → none.</summary>
    public string? BaseUomId { get; init; }
}


/// <summary>§4.13 takeException — "İncelemeye al".</summary>
public sealed record TakeExpiryExceptionCommand(Guid ExceptionId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<ExpiryExceptionDetailDto>>;

/// <summary>§4.14 commentException (append-only, no If-Match).</summary>
public sealed record CommentExpiryExceptionCommand(Guid ExceptionId, string? Text, string IdempotencyKey)
    : IRequest<Response<ExpiryExceptionEventDto>>;

/// <summary>§4.15 prepareResolution — the decision the user is about to sign (15 minutes).</summary>
public sealed record PrepareExpiryResolutionCommand(Guid ExceptionId, ExpiryResolution Resolution, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<ExpiryResolutionPreparedDto>>;

/// <summary>§4.16 resolveException — accept / fix / cancel, signature by MOD-0022 policy (KF-E), SoD (CT-F10).</summary>
public sealed record ResolveExpiryExceptionCommand(
    Guid ExceptionId,
    ExpiryResolution Resolution,
    string? Reason,
    string? SignatureId,
    string IdempotencyKey,
    int ExpectedVersion) : IRequest<Response<ExpiryExceptionDetailDto>>;

/// <summary>§4.17 closeException — Accepted / Fixed → Closed (no signature).</summary>
public sealed record CloseExpiryExceptionCommand(Guid ExceptionId, string? Note, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<ExpiryExceptionDetailDto>>;

/// <summary>§4.19 createDateOverride (F2, KF-D) — prepare an override of the suggested lot dates (24 h, consumed by one createLot / Draft updateLot).</summary>
public sealed record CreateDateOverrideCommand(
    Guid ItemId,
    Guid SkuId,
    Domain.Enums.SkuLevel SkuLevel,
    string? LotNumber,
    DateOnly Manufacturing,
    DateOnly? Expiry,
    DateOnly? Retest,
    string? Reason,
    string IdempotencyKey) : IRequest<Response<ExpiryDateOverrideDto>>;
