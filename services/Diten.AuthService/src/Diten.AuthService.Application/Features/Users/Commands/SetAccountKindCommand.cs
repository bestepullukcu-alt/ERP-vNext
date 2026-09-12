using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — classifies a tenant user account. Guarded at the endpoint by the explicit-grant-only
/// key <c>auth.users.account-kind.manage</c>; tenant-scoped (a user of another tenant answers 404, indistinguishable
/// from "does not exist"). Every actual change is written to <c>authAuditLogs</c> with the previous and the new kind.
/// </summary>
/// <param name="UserId">The target user (must belong to the caller's tenant).</param>
/// <param name="Kind">The enum NAME, case-insensitive: Unknown | Human | Service. Anything else fails validation (400).</param>
/// <param name="CorrelationId">The request's correlation id, carried into the audit row (set by the controller).</param>
public sealed record SetAccountKindCommand(
    Guid UserId,
    string Kind,
    string? CorrelationId = null
) : IRequest<Response<AccountAssertionDto>>;
