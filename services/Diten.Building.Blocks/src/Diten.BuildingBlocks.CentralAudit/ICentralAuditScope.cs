namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// Who and where, for the central record — implemented by the SERVICE over its own scoped tenant context (each service
/// resolves tenant / company / user its own way; this package does not guess from claims). Read once, when the marked
/// command starts.
///
/// <para>⚠ The tenant here is the tenant the record is filed under, and Platform files it there without re-checking
/// (<c>InternalAuditController</c> pins <c>TargetTenantId</c>). It must come from the caller's signed token / resolved
/// scope — never from a request body.</para>
/// </summary>
public interface ICentralAuditScope
{
    /// <summary>False when the command runs without a resolved caller scope (no tenant): no record can be filed.</summary>
    bool IsResolved { get; }

    Guid TenantId { get; }

    /// <summary>The legal entity / company the command acted in, when the service has one.</summary>
    Guid? CompanyId { get; }

    /// <summary>The acting user (<c>sub</c>); null for a system actor.</summary>
    Guid? ActorId { get; }

    /// <summary>The actor's name at that moment, when the token carries one. Platform masks it before storing.</summary>
    string? ActorDisplayName { get; }

    /// <summary>A Platform <c>AuditActorType</c> NAME — <see cref="CentralAuditActorTypes"/>.</summary>
    string ActorType { get; }

    Guid CorrelationId { get; }
}

/// <summary>Platform <c>AuditActorType</c> names this package sends.</summary>
public static class CentralAuditActorTypes
{
    public const string PlatformAdministrator = "PlatformAdministrator";
    public const string PartnerAdministrator = "PartnerAdministrator";
    public const string TenantUser = "TenantUser";
    public const string System = "System";
    public const string Service = "Service";
}
