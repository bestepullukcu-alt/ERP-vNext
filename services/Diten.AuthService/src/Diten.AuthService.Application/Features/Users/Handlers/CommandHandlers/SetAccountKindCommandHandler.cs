using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the ONE write path for an account's kind.
///
/// <list type="bullet">
/// <item>Tenant-scoped: the target is read with <c>GetByIdAndTenantAsync</c>; a missing user and another tenant's
/// user produce the SAME 404 body (no cross-tenant existence disclosure).</item>
/// <item>Audited: every actual change is recorded through the shared RBAC audit recorder
/// (<c>authAuditLogs</c>) with the previous and the new kind, the actor (stamped by the recorder), the target, the
/// tenant and the request correlation id. Setting the kind the account already has is a no-op and writes no row.</item>
/// <item>No other handler classifies an account: the enum's Human/Service literals may appear ONLY here and in tests
/// (AccountKindCreationPathsGuardTests). This handler itself parses the NAME the validator already vetted.</item>
/// </list>
/// </summary>
public sealed class SetAccountKindCommandHandler : IRequestHandler<SetAccountKindCommand, Response<AccountAssertionDto>>
{
    public const string AuditEventName = "user_account_kind_changed";

    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IRbacAuditRecorder _audit;
    private readonly ILogger<SetAccountKindCommandHandler> _logger;

    public SetAccountKindCommandHandler(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        IRbacAuditRecorder audit,
        ILogger<SetAccountKindCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Response<AccountAssertionDto>> Handle(SetAccountKindCommand request, CancellationToken ct)
    {
        // Defense in depth behind the validator: a value that is not a defined name never reaches the entity.
        if (!Enum.TryParse<AccountKind>(request.Kind?.Trim(), ignoreCase: true, out var newKind) || !Enum.IsDefined(newKind))
        {
            return Response<AccountAssertionDto>.Fail("Kind must be one of: Unknown, Human, Service.", 400);
        }

        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null)
        {
            return Response<AccountAssertionDto>.Fail("User not found.", 404);
        }

        var previousKind = user.AccountKind;
        if (previousKind == newKind)
        {
            // Idempotent: nothing changed, nothing to audit.
            return Response<AccountAssertionDto>.Success(Assertion(user));
        }

        user.SetAccountKind(newKind);
        await _userRepository.UpdateForTenantAsync(user, _tenantContext.TenantId, ct);

        await _audit.RecordAsync(AuditEventName, _tenantContext.TenantId, new
        {
            targetUserId = user.Id,
            tenantId = _tenantContext.TenantId,
            previousKind = previousKind.ToString(),
            newKind = newKind.ToString(),
            correlationId = request.CorrelationId
        }, ct);

        _logger.LogInformation(
            "Account kind changed. UserId={UserId} TenantId={TenantId} {PreviousKind}->{NewKind} CorrelationId={CorrelationId}",
            user.Id, _tenantContext.TenantId, previousKind, newKind, request.CorrelationId);

        return Response<AccountAssertionDto>.Success(Assertion(user));
    }

    private static AccountAssertionDto Assertion(Domain.Entities.User user) => new(
        user.Id,
        user.IsActive,
        user.AccountKind.ToString(),
        DateTimeOffset.UtcNow,
        user.UpdatedAt);
}
