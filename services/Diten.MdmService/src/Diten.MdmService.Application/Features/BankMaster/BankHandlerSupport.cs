using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>What the bank and branch handlers share: the coded failures and the answer to a refused write.</summary>
internal static class BankHandlerSupport
{
    public static Response<T> Fail<T>(string code, int status) => Response<T>.Fail(code, status);

    /// <summary>
    /// A write that did not land and needs no re-read: unique-index conflicts, a missing / unwritable audit intent, indexes
    /// not ready. <see cref="BankStoreOutcome.Stale"/> is the caller's (it re-reads to say 404, version or state) and
    /// <see cref="BankStoreOutcome.Written"/> is not a failure.
    /// </summary>
    public static Response<T> FromOutcome<T>(BankStoreOutcome outcome) => outcome switch
    {
        BankStoreOutcome.BicConflict => Fail<T>(BankErrorCodes.BicConflict, 409),
        BankStoreOutcome.NationalCodeConflict => Fail<T>(BankErrorCodes.NationalCodeConflict, 409),
        BankStoreOutcome.BranchCodeConflict => Fail<T>(BankErrorCodes.BranchCodeConflict, 409),
        // The code service gave a code the tenant already holds: a code is never reused (RCS-001 §6) and none is invented.
        BankStoreOutcome.CodeConflict => Fail<T>(BankErrorCodes.RecordCodeUnavailable, 503),
        BankStoreOutcome.IndexesNotReady => Fail<T>(BankErrorCodes.StoreNotReady, 503),
        BankStoreOutcome.AuditIntentUnavailable => Fail<T>(BankErrorCodes.AuditUnavailable, 503),
        _ => throw new InvalidOperationException("BANK_STORE_OUTCOME_UNHANDLED")
    };

    /// <summary>A stale conditional write, told apart by a tenant-scoped re-read: gone, another version, or another state.</summary>
    public static Response<T> FromStale<T>(int? storedVersion, int expectedVersion, string notFoundCode) =>
        storedVersion is null
            ? Fail<T>(notFoundCode, 404)
            : storedVersion != expectedVersion
                ? Fail<T>(BankErrorCodes.VersionConflict, 409)
                : Fail<T>(BankErrorCodes.StateInvalid, 409);
}
