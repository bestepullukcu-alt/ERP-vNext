using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountRelationship.Commands;

public sealed record CreateAccountRelationshipCommand(
    Guid SourceAccountId,
    Guid TargetAccountId,
    string RelationshipType,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    // MOD-0150 hardening: required only when the two accounts are in different known countries (controlled relationship).
    string? CrossCountryReason = null) : IRequest<Response<Guid>>;

public sealed record UpdateAccountRelationshipCommand(
    Guid SourceAccountId,
    Guid RelationshipId,
    string RelationshipType,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    string? CrossCountryReason = null) : IRequest<Response<bool>>;

public sealed record DeleteAccountRelationshipCommand(Guid SourceAccountId, Guid RelationshipId) : IRequest<Response<bool>>;
