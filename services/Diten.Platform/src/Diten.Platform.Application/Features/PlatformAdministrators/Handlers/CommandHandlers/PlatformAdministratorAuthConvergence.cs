using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

/// <summary>
/// BL-521 FIX4 item 2 / FIX5 items 5 and 10 — THE RECORD IS READ AGAIN AND AUTH FOLLOWS IT AS IT IS NOW. Every path that
/// brings Auth to the Platform record BEFORE the record is written (update: sync; resend invite: provision) can end with the
/// record write refused, failed, cancelled or of unknown outcome. FIX3 put back the values the record had when THAT request
/// loaded it — values another request may already have replaced (the reported race: R1 a name edit and R2 taking SuperAdmin
/// away load the same version; R1 restored its own picture and left every new token SuperAdmin). So the record is read
/// again and Auth is synced to what it holds now; the before-image is never restored.
///
/// <para>Never masks the failure being answered: if Auth cannot be reached either, the failure is logged at Error
/// (identifiers only — never an address, never Auth's reply) and the record is marked
/// <see cref="PlatformAdministrator.AuthSyncPendingSinceUtc"/>; the next successful update of the record, or a successful
/// convergence, clears the mark, and every platform sign-in and refresh takes the roles from the record anyway.</para>
///
/// <para>FIX5 item 5 — shared by the update and the resend-invite handlers (resend used to restore nothing and converge
/// nothing: a refused write left Auth with the roles the resend loaded).</para>
/// </summary>
internal sealed class PlatformAdministratorAuthConvergence(
    IPlatformAdministratorRepository repository,
    IPlatformAdministratorProvisioningService provisioningService,
    ILogger logger)
{
    /// <summary>What Auth must hold for this record: its address (the lookup key, never changed), names, type, roles and status.</summary>
    public static PlatformAdministratorProvisioningSyncRequest SyncRequestOf(PlatformAdministrator administrator) => new(
        PlatformAdministratorParsing.NormalizeEmail(administrator.Email),
        administrator.UserName,
        administrator.DisplayName,
        administrator.ActorType.ToString(),
        administrator.Roles.Select(x => x.ToString()).ToList(),
        administrator.Status.ToString())
    {
        AdministratorId = administrator.Id
    };

    public async Task ConvergeToRecordAsync(Guid administratorId)
    {
        PlatformAdministrator? current;
        try
        {
            current = await repository.GetByIdAsync(administratorId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Platform administrator record could not be read again to converge Auth to it. AdministratorId={AdministratorId}",
                administratorId);
            await MarkPendingAsync(administratorId);
            return;
        }

        if (current is null)
        {
            // Deleted in the meantime: Platform answers its own 404 to the account's every sign-in and refresh, which
            // Auth treats as a definite no. There is no record state left to converge to.
            logger.LogWarning(
                "Platform administrator record is gone; Auth is not converged to it. AdministratorId={AdministratorId}",
                administratorId);
            return;
        }

        try
        {
            await provisioningService.SyncAsync(SyncRequestOf(current), CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Auth could not be converged to the platform administrator record (roles, status); the record is marked AuthSyncPending. The next successful update of this record clears the mark, and every platform sign-in and refresh takes the roles from the record. AdministratorId={AdministratorId}",
                administratorId);
            await MarkPendingAsync(administratorId);
            return;
        }

        if (current.AuthSyncPendingSinceUtc is not null)
        {
            try
            {
                await repository.MarkAuthSyncPendingAsync(administratorId, pending: false, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Auth was converged to the platform administrator record but its pending mark could not be cleared. AdministratorId={AdministratorId}",
                    administratorId);
            }
        }
    }

    /// <summary>
    /// FIX5 item 10 — Auth did not answer the FIRST call (timeout, connection failure, 5xx): a second call to the same Auth
    /// would wait for the same timeout again and still have nothing to converge to. The record is marked directly, the error
    /// is logged (identifiers only) and the caller rethrows the original failure.
    /// </summary>
    public async Task MarkPendingBecauseAuthDidNotAnswerAsync(Guid administratorId)
    {
        logger.LogError(
            "Auth did not answer the platform administrator sync; no second call is made and the record is marked AuthSyncPending. AdministratorId={AdministratorId}",
            administratorId);
        await MarkPendingAsync(administratorId);
    }

    public async Task MarkPendingAsync(Guid administratorId)
    {
        try
        {
            await repository.MarkAuthSyncPendingAsync(administratorId, pending: true, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Platform administrator record could not be marked AuthSyncPending either. AdministratorId={AdministratorId}",
                administratorId);
        }
    }
}
