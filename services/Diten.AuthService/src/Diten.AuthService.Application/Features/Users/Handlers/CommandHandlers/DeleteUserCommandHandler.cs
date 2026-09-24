using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

/// <summary>
/// ⚠ A TENANT MUST NOT BE ABLE TO LOCK ITSELF OUT (2026-09-23, found in live testing).
///
/// <para>This handler had no guard of any kind: one line, soft-delete, 204. During a from-scratch test the owner
/// deleted every account in the tenant — including their own and the administrator's — and then could not log in
/// at all. Nothing in the product refused, warned, or even noticed; the screen reported success each time.</para>
///
/// <para>Two refusals close that door, and they are deliberately different questions:</para>
/// <list type="bullet">
///   <item><b>Yourself.</b> Deleting the account you are signed in with is never what someone means to do, and
///   the damage is immediate. This one needs no counting.</item>
///   <item><b>The last person who can create users.</b> Recovery from any other deletion runs through someone who
///   can add an account back. When that capability reaches zero the tenant is finished — there is no screen left
///   that can undo it. So the LAST holder of <c>auth.users.create</c> cannot be removed.</item>
/// </list>
///
/// <para>SAP and Oracle both refuse to remove the final administrator for the same reason; it is the one delete
/// whose blast radius is the whole tenant. The check counts the CAPABILITY rather than a role NAME, because a
/// tenant may call its administrator role anything at all.</para>
///
/// <para>⚠ AND IT COUNTS LIVE HOLDERS, not assignments. A role row pointing at a soft-deleted user is not a
/// person who can log in; counting those would let the tenant delete its last real administrator while the
/// guard congratulated itself.</para>
/// </summary>
public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Response<NoContent>>
{
    /// <summary>The capability that makes recovery possible: whoever holds it can put an account back.</summary>
    public const string RecoveryPermission = "auth.users.create";

    public const string SelfDeleteCode = "USER_DELETE_SELF";
    public const string LastStewardCode = "USER_DELETE_LAST_STEWARD";

    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantContext tenantContext,
        ICurrentUserAccessor currentUser,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;

        // Deleting something that is not there used to answer 204 — a success for work nobody did.
        var target = await _userRepository.GetByIdAndTenantAsync(request.Id, tenantId, ct);
        if (target is null)
        {
            return Response<NoContent>.Fail("User not found.", 404);
        }

        if (_currentUser.UserId == request.Id)
        {
            return Response<NoContent>.Fail(
                "You cannot delete the account you are signed in with.",
                [new ResponseError(SelfDeleteCode)],
                409);
        }

        if (await IsLastRecoveryHolderAsync(request.Id, tenantId, ct))
        {
            return Response<NoContent>.Fail(
                "This is the last account that can create users; deleting it would leave the tenant with no way back in.",
                [new ResponseError(LastStewardCode)],
                409);
        }

        await _userRepository.SoftDeleteAsync(request.Id, tenantId, ct);
        _logger.LogInformation("User soft-deleted. Id={Id} TenantId={TenantId}", request.Id, tenantId);
        return Response<NoContent>.Success(204);
    }

    /// <summary>
    /// True when removing <paramref name="candidateId"/> would leave nobody holding <see cref="RecoveryPermission"/>.
    /// Reads roles → permissions → holders, and keeps only holders who are still real users.
    /// </summary>
    private async Task<bool> IsLastRecoveryHolderAsync(Guid candidateId, Guid tenantId, CancellationToken ct)
    {
        var roles = await _roleRepository.GetAllByTenantAsync(tenantId, ct);

        foreach (var role in roles)
        {
            var permissions = await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, tenantId, ct);
            if (!permissions.Any(p => string.Equals(p, RecoveryPermission, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var holderId in await _userRoleRepository.GetUserIdsByRoleAsync(role.Id, tenantId, ct))
            {
                if (holderId == candidateId)
                {
                    continue;
                }

                // A live user, not merely an assignment row: the repository hides soft-deleted accounts.
                if (await _userRepository.GetByIdAndTenantAsync(holderId, tenantId, ct) is not null)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
