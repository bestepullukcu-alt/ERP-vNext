using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.AccountContact.Commands;

public sealed record LinkContactToAccountCommand(
    Guid AccountId,
    Guid ContactId,
    string RoleCode,
    bool IsPrimary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    // MOD-0150 hardening: required only when Contact and Account are in different known countries (controlled link).
    string? CrossCountryReason = null,
    // MOD-0150 in-account hierarchy: the contact this contact reports to within THIS account (optional).
    Guid? ReportsToContactId = null) : IRequest<Response<Guid>>;

public sealed record UpdateAccountContactLinkCommand(
    Guid AccountId,
    Guid LinkId,
    string RoleCode,
    bool IsPrimary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes,
    string? Status = null,
    string? CrossCountryReason = null,
    Guid? ReportsToContactId = null) : IRequest<Response<bool>>;

public sealed record DeleteAccountContactLinkCommand(Guid AccountId, Guid LinkId) : IRequest<Response<bool>>;
