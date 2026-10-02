namespace Diten.AuthService.Application.Common.Interfaces;

public interface IAuthAuditService
{
    Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default);
    Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default);

    /// <summary>
    /// Writes the row under a CALLER-SUPPLIED id, only if no row with that id exists (create-if-absent). For a
    /// write-ahead audit whose id is derived from what is being recorded: any number of writers — or the same writer
    /// again after a restart — leave exactly ONE row. An existing row is never modified.
    /// </summary>
    /// <returns>True when this call wrote the row; false when a row with that id was already there.</returns>
    /// <remarks>
    /// Additive, like the registry members of <c>IServiceClientIdentityRepository</c>: an implementation that does not
    /// provide it refuses, so a caller that writes ahead (audit first, then the insert) inserts nothing.
    /// </remarks>
    Task<bool> WriteOnceAsync(Guid auditId, string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) =>
        throw new NotSupportedException("Idempotent audit write is unavailable.");
}
