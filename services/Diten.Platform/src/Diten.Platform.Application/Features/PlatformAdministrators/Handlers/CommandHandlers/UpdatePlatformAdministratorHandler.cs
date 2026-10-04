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

        // FIX2 item 5(b) — demoting (to partner, or out of SuperAdmin) or suspending / disabling the last active platform
        // administrator holding SuperAdmin is refused 409, as suspend / delete / role removal already are. FIX3 item 2(b) —
        // so is moving it to another e-mail: until Auth follows, it cannot sign in, while the count still counts it.
        var staysPlatformSuperAdmin = newStatus == AdministratorStatus.Active
                                      && newActorType == ActorType.PlatformAdmin
                                      && newRoles.Contains(AdministratorRole.SuperAdmin)
                                      && !emailChanges;
        var leavesSuperAdminSet = PlatformAdministratorMutationSupport.IsActivePlatformSuperAdmin(administrator) && !staysPlatformSuperAdmin;
        if (leavesSuperAdminSet)
        {
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(administrator.Id, AdminSafetyAction.Disable, ct);
            if (lastAdminGuard is not null) return lastAdminGuard;
        }

        // The write below is conditional on this version anyway; refusing a stale edit HERE spares Auth a sync it would
        // have to undo.
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        if (await _repository.ExistsByEmailAsync(normalizedEmail, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator email already exists.", 409);
        }

        var normalizedUserName = PlatformAdministratorParsing.NormalizeUserName(request.Request.UserName);
        if (await _repository.ExistsByUserNameAsync(normalizedUserName, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator username already exists.", 409);
        }

        // FIX3 item 2(a) — AUTH FIRST, THEN THE RECORD. Auth finds the account by the record's CURRENT address and moves
        // it to the new one. Before, the record was saved first and the sync looked the account up by the NEW address: an
        // e-mail change answered 404 there, the request 400, and the saved record pointed at an address Auth did not have
        // — the next sign-in / refresh was refused and every session revoked. Now a refused sync throws before anything is
        // written here (Auth is first put back to the record's values, in case it applied part of the change); a record
        // write refused after a successful sync (version, the last super administrator, a concurrent change) puts Auth back
        // too — found then under the NEW address it was just moved to.
        var recordValues = SyncRequestOf(administrator);
        var syncAuth = new PlatformAdministratorProvisioningSyncRequest(
            normalizedEmail,
            request.Request.UserName.Trim(),
            request.Request.DisplayName.Trim(),
            newActorType.ToString(),
            newRoles.Select(x => x.ToString()).ToList(),
            PreviousEmail: currentEmail);
        try
        {
            await _provisioningService.SyncAsync(syncAuth, ct);
        }
        catch
        {
            await RestoreAuthAsync(recordValues, administrator.Id);
            throw;
        }

        administrator.Email = normalizedEmail;
        administrator.NormalizedEmail = normalizedEmail;
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

        // FIX3 item 3 — a change that takes an active platform super administrator out of the set is written under the
        // guard: the "is another one left?" count and the write in one transaction no concurrent such write can slip past.
        var outcome = leavesSuperAdminSet
            ? await _repository.UpdateGuardedAsync(administrator, request.Request.Version, ct)
            : await _repository.UpdateAsync(administrator, request.Request.Version, ct)
                ? SuperAdminGuardedWriteResult.Written
                : SuperAdminGuardedWriteResult.VersionConflict;
        if (outcome != SuperAdminGuardedWriteResult.Written)
        {
            await RestoreAuthAsync(recordValues with { PreviousEmail = normalizedEmail }, administrator.Id);
            return PlatformAdministratorMutationSupport.RefusedWrite(outcome, AdminSafetyMessages.ChangedByAnotherUser);
        }

        return Response<NoContent>.Success(204);
    }

    /// <summary>What Auth holds for the administrator BEFORE this change (its current address, names, type, roles).</summary>
    private static PlatformAdministratorProvisioningSyncRequest SyncRequestOf(PlatformAdministrator administrator) => new(
        PlatformAdministratorParsing.NormalizeEmail(administrator.Email),
        administrator.UserName,
        administrator.DisplayName,
        administrator.ActorType.ToString(),
        administrator.Roles.Select(x => x.ToString()).ToList());

    /// <summary>
    /// Puts Auth back to what the record still says, after a sync this request will not keep. Best effort, never masking
    /// the refusal being answered: if it fails too, the two disagree until the next successful update of this record —
    /// logged as an error (identifiers only).
    /// </summary>
    private async Task RestoreAuthAsync(PlatformAdministratorProvisioningSyncRequest restore, Guid administratorId)
    {
        try
        {
            await _provisioningService.SyncAsync(restore, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Platform administrator update was not written, and Auth could not be put back to the record's values. AdministratorId={AdministratorId}",
                administratorId);
        }
    }
}
