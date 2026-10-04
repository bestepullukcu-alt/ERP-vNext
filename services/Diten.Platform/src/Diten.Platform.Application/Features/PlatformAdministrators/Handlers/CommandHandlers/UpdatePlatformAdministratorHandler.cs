using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class UpdatePlatformAdministratorHandler : IRequestHandler<UpdatePlatformAdministratorCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly IPlatformAdministratorProvisioningService _provisioningService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorSafetyGuard _safetyGuard;
    private readonly ILogger<UpdatePlatformAdministratorHandler> _logger;

    public UpdatePlatformAdministratorHandler(
        IPlatformAdministratorRepository repository,
        IPlatformAdministratorProvisioningService provisioningService,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard,
        ILogger<UpdatePlatformAdministratorHandler> logger)
    {
        _repository = repository;
        _provisioningService = provisioningService;
        _currentUser = currentUser;
        _safetyGuard = safetyGuard;
        _logger = logger;
    }

    /// <summary>The message of a refused self change: the existing self-action sentence, which the administrators
    /// screen already shows in the user's language.</summary>
    public const string SelfChangeMessage = "You cannot perform this action on your own account.";

    public async Task<Response<NoContent>> Handle(UpdatePlatformAdministratorCommand request, CancellationToken ct)
    {
        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        // BL-521 FIX4 item 3 — every decision below is made on the record as loaded, so it must be the version the client
        // edited: a different version is refused before anything is decided, and the write is conditional on the version
        // that was read (the two are then the same).
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        var loadedVersion = administrator.Version;
        var normalizedEmail = PlatformAdministratorParsing.NormalizeEmail(request.Request.Email);
        var currentEmail = PlatformAdministratorParsing.NormalizeEmail(administrator.Email);
        var newActorType = PlatformAdministratorParsing.ParseActorType(request.Request.ActorType);
        var newStatus = PlatformAdministratorParsing.ParseStatus(request.Request.Status);
        var newRoles = PlatformAdministratorParsing.ParseRoles(request.Request.Roles).ToList();
        var emailChanges = !string.Equals(normalizedEmail, currentEmail, StringComparison.Ordinal);
        var rolesChange = !newRoles.ToHashSet().SetEquals(administrator.Roles);

        // BL-521 FIX2 item 5(a) — FIX1 made the record's actor type and status take effect on the very next request, so
        // an administrator may not change its OWN actor type, status or e-mail (the address its record and its sign-in
        // are found by). FIX3 item 1 — nor its own ROLES: they are synced to Auth and travel in the next token to every
        // service that trusts it, so a ReadOnly administrator could make itself SuperAdmin here. Other own fields (name,
        // user name) stay editable.
        if (await _safetyGuard.IsSelfAsync(administrator.Id, ct)
            && (newActorType != administrator.ActorType
                || newStatus != administrator.Status
                || emailChanges
                || rolesChange))
        {
            return Response<NoContent>.Fail(SelfChangeMessage, 403, AdminSafetyReasonCodes.SelfChangeForbidden);
        }

        // FIX3 item 2(c) — the system seed administrator is protected here as Suspend and Delete protect it: its e-mail
        // does not change (the seed finds it by that address on every start, and a renamed seed stops the next start) and
        // it is not suspended or disabled through the update path either. The seed's own behaviour is unchanged (BL-534).
        if (PlatformAdministratorMutationSupport.IsSystemSeed(administrator))
        {
            if (emailChanges)
            {
                return Response<NoContent>.Fail(AdminSafetyMessages.SeedEmailCannotChange, 409, AdminSafetyReasonCodes.SystemSeedProtected);
            }

            if (newStatus != administrator.Status && newStatus != AdministratorStatus.Active)
            {
                return Response<NoContent>.Fail(AdminSafetyMessages.SeedCannotBeSuspended, 409, AdminSafetyReasonCodes.SystemSeedProtected);
            }
        }

        // BL-521 FIX4 item 1 — AN ADMINISTRATOR'S E-MAIL NEVER CHANGES. It is the identity key: Platform and Auth find the
        // same administrator by it (status, sign-in, refresh, the sync below), and every way of moving it (FIX3's saga
        // included) left a window in which the two pointed at different addresses. A new address is invited as a new
        // administrator and the old record removed — as SAP never renames a user id. The comparison is on the normalized
        // address, so a change of letter case or surrounding spaces is not a change.
        if (emailChanges)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.EmailImmutable, 409, AdminSafetyReasonCodes.EmailImmutable);
        }

        // FIX2 item 5(b) — demoting (to partner, or out of SuperAdmin) or suspending / disabling the last active platform
        // administrator holding SuperAdmin is refused 409, as suspend / delete / role removal already are.
        var staysPlatformSuperAdmin = newStatus == AdministratorStatus.Active
                                      && newActorType == ActorType.PlatformAdmin
                                      && newRoles.Contains(AdministratorRole.SuperAdmin);
        var leavesSuperAdminSet = PlatformAdministratorMutationSupport.IsActivePlatformSuperAdmin(administrator) && !staysPlatformSuperAdmin;
        if (leavesSuperAdminSet)
        {
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(administrator.Id, AdminSafetyAction.Disable, ct);
            if (lastAdminGuard is not null) return lastAdminGuard;
        }

        var normalizedUserName = PlatformAdministratorParsing.NormalizeUserName(request.Request.UserName);
        if (await _repository.ExistsByUserNameAsync(normalizedUserName, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator username already exists.", 409);
        }

        // FIX3 item 2(a) — AUTH FIRST, THEN THE RECORD: a sync Auth refuses throws before anything is written here. The
        // address only FINDS the account (FIX4 item 1). FIX4 item 5 — the record's status travels with it, so Auth
        // activates the account only when the record is Active and otherwise leaves the account's own state alone.
        var syncAuth = new PlatformAdministratorProvisioningSyncRequest(
            currentEmail,
            request.Request.UserName.Trim(),
            request.Request.DisplayName.Trim(),
            newActorType.ToString(),
            newRoles.Select(x => x.ToString()).ToList(),
            newStatus.ToString());
        try
        {
            await _provisioningService.SyncAsync(syncAuth, ct);
        }
        catch
        {
            await ConvergeAuthToRecordAsync(administrator.Id);
            throw;
        }

        administrator.UserName = request.Request.UserName.Trim();
        administrator.NormalizedUserName = normalizedUserName;
        administrator.DisplayName = request.Request.DisplayName.Trim();
        administrator.Status = newStatus;
        administrator.Roles = newRoles;
        PlatformAdministratorMutationSupport.ApplyScope(
            administrator,
            newActorType,
            request.Request.PartnerId,
            request.Request.AllowedTenantIds);
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        // FIX4 item 2 — Auth was just given exactly what is written here, so a successful write leaves the two converged
        // and clears a convergence still pending from an earlier failure.
        administrator.AuthSyncPendingSinceUtc = null;

        // FIX3 item 3 — a change that takes an active platform super administrator out of the set is written under the
        // guard: the "is another one left?" count and the write in one transaction no concurrent such write can slip past.
        SuperAdminGuardedWriteResult outcome;
        try
        {
            outcome = leavesSuperAdminSet
                ? await _repository.UpdateGuardedAsync(administrator, loadedVersion, ct)
                : await _repository.UpdateAsync(administrator, loadedVersion, ct)
                    ? SuperAdminGuardedWriteResult.Written
                    : SuperAdminGuardedWriteResult.VersionConflict;
        }
        catch
        {
            // An exception, a cancellation, a commit whose result never came back: whatever the record now says, Auth
            // follows THAT — never this request's values, never the values it had before.
            await ConvergeAuthToRecordAsync(administrator.Id);
            throw;
        }

        if (outcome != SuperAdminGuardedWriteResult.Written)
        {
            await ConvergeAuthToRecordAsync(administrator.Id);
            return PlatformAdministratorMutationSupport.RefusedWrite(outcome, AdminSafetyMessages.ChangedByAnotherUser);
        }

        return Response<NoContent>.Success(204);
    }

    /// <summary>
    /// What Auth must hold for this record: its address (the lookup key, never changed), names, type, roles and status.
    /// </summary>
    private static PlatformAdministratorProvisioningSyncRequest SyncRequestOf(PlatformAdministrator administrator) => new(
        PlatformAdministratorParsing.NormalizeEmail(administrator.Email),
        administrator.UserName,
        administrator.DisplayName,
        administrator.ActorType.ToString(),
        administrator.Roles.Select(x => x.ToString()).ToList(),
        administrator.Status.ToString());

    /// <summary>
    /// BL-521 FIX4 item 2 — THE RECORD IS READ AGAIN AND AUTH FOLLOWS IT AS IT IS NOW. FIX3 put back the values the record
    /// had when THIS request loaded it, which another request may already have replaced: R1 (a name edit) and R2 (taking
    /// SuperAdmin away) load the same version; R2 syncs Auth, R1 syncs Auth (SuperAdmin again), R2 writes, R1's write is
    /// refused — and R1 then restored ITS picture (SuperAdmin): the record said SupportAdmin, every new token SuperAdmin.
    /// Now R1 re-reads (SupportAdmin) and syncs that. Never masks the refusal being answered: if Auth cannot be reached
    /// either, the failure is logged at Error (identifiers only) and the record is marked
    /// <see cref="PlatformAdministrator.AuthSyncPendingSinceUtc"/>; the next successful update of the record clears the
    /// mark, and every platform sign-in and refresh takes the roles from the record anyway.
    /// </summary>
    private async Task ConvergeAuthToRecordAsync(Guid administratorId)
    {
        PlatformAdministrator? current;
        try
        {
            current = await _repository.GetByIdAsync(administratorId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Platform administrator record could not be read again to converge Auth to it. AdministratorId={AdministratorId}",
                administratorId);
            await MarkConvergencePendingAsync(administratorId);
            return;
        }

        if (current is null)
        {
            // Deleted in the meantime: Platform answers its own 404 to the account's every sign-in and refresh, which
            // Auth treats as a definite no. There is no record state left to converge to.
            _logger.LogWarning(
                "Platform administrator record is gone; Auth is not converged to it. AdministratorId={AdministratorId}",
                administratorId);
            return;
        }

        try
        {
            await _provisioningService.SyncAsync(SyncRequestOf(current), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Auth could not be converged to the platform administrator record (roles, status); the record is marked AuthSyncPending. The next successful update of this record clears the mark, and every platform sign-in and refresh takes the roles from the record. AdministratorId={AdministratorId}",
                administratorId);
            await MarkConvergencePendingAsync(administratorId);
            return;
        }

        if (current.AuthSyncPendingSinceUtc is not null)
        {
            try
            {
                await _repository.MarkAuthSyncPendingAsync(administratorId, pending: false, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Auth was converged to the platform administrator record but its pending mark could not be cleared. AdministratorId={AdministratorId}",
                    administratorId);
            }
        }
    }

    private async Task MarkConvergencePendingAsync(Guid administratorId)
    {
        try
        {
            await _repository.MarkAuthSyncPendingAsync(administratorId, pending: true, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Platform administrator record could not be marked AuthSyncPending either. AdministratorId={AdministratorId}",
                administratorId);
        }
    }
}
