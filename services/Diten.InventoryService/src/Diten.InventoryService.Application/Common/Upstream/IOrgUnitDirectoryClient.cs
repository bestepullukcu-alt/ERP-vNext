namespace Diten.InventoryService.Application.Common.Upstream;

/// <summary>
/// MOD-0173-FU02 §7 / ASSUMPTION-LOC-11: confirms, on a location write only, that an <c>orgUnitRef</c> names an
/// Active, non-archived Platform OrganizationUnit of the caller's legal entity (owner MOD-0288; read through the
/// Gateway with the caller's token). A soft link — nothing is copied, nothing cascades. Unreachable / refused /
/// out-of-contract → <see cref="OrgUnitDirectoryUnavailableException"/> (fail-closed, nothing written).
/// </summary>
public interface IOrgUnitDirectoryClient
{
    Task<OrgUnitCheck> CheckAsync(Guid organizationUnitId, Guid legalEntityId, CancellationToken cancellationToken = default);
}

public enum OrgUnitCheck
{
    /// <summary>Active, not archived, same legal entity.</summary>
    Valid,

    /// <summary>Unknown to the caller's tenant, inactive, archived or of another legal entity.</summary>
    Invalid
}

public sealed class OrgUnitDirectoryUnavailableException : Exception
{
    public OrgUnitDirectoryUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
