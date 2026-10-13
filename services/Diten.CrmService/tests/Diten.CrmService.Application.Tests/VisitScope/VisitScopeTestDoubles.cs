using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;

namespace Diten.CrmService.Application.Tests.VisitScope;

/// <summary>WP-VP-2 — a caller with a resource id and a fixed permission set. <see cref="Unrestricted"/> holds both
/// read-all keys, which keeps the pre-VP-2 tests' tenant-wide reads unchanged.</summary>
internal sealed class TestCallerScope : ICallerScope
{
    private readonly HashSet<string> _permissions;

    public TestCallerScope(string? resourceId, params string[] permissions)
    {
        CallerResourceId = resourceId;
        _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
    }

    public static TestCallerScope Unrestricted(string? resourceId = "rep-1")
        => new(resourceId, "crm.planned-visit.read-all", "crm.visit-plan.read-all");

    public string? CallerResourceId { get; }

    public bool HasPermission(string permissionKey) => _permissions.Contains(permissionKey);
}

/// <summary>WP-VP-2 — a deriver that answers a fixed play / campaign (default: none).</summary>
internal sealed class FixedProvenanceDeriver : IVisitProvenanceDeriver
{
    public DerivedPlay Play { get; set; } = DerivedPlay.None(Array.Empty<Guid>());
    public Guid? Campaign { get; set; }

    public Task<DerivedPlay> DerivePlayAsync(Guid? contactId, DateTimeOffset at, CancellationToken cancellationToken)
        => Task.FromResult(Play);

    public Task<Guid?> DeriveCampaignAsync(Guid? contactId, Guid? accountId, DateOnly date, CancellationToken cancellationToken)
        => Task.FromResult(Campaign);
}
