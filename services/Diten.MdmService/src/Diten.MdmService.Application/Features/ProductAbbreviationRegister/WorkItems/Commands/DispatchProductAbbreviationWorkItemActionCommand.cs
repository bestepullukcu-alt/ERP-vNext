using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;

public sealed record DispatchProductAbbreviationWorkItemActionCommand(
    Guid ItemId,
    string ActionCode,
    string? ProviderCode,
    int? ExpectedVersion,
    string? Reason,
    string? Note,
    bool HasUnmappedFields,
    /// <summary>
    /// FIX1 C1 — the caller's idempotency key (the Task Center sends one with every versioned item). Bound into the
    /// operation key, so a retried click is the same command and a fresh attempt is a new one.
    /// </summary>
    string? IdempotencyKey = null)
    : IRequest<ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemActionResponse>>;
