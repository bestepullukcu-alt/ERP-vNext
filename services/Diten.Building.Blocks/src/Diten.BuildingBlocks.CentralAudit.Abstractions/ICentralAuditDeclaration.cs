namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// BL-501 / AUD-001 K4 — the marker a write command carries to be recorded in Platform's CENTRAL audit log
/// (<c>audit_events</c>, path b). <c>CentralAuditBehavior</c> (Diten.BuildingBlocks.CentralAudit) reads it: when the command
/// SUCCEEDS one event is written to the service's own outbox (in the command's Mongo transaction when the command enlists
/// it — <c>CentralAuditTransaction.EnlistAsync</c> — otherwise right after the command), and
/// <c>CentralAuditDispatcher</c> delivers it to <c>POST /api/internal/audit/append</c>. A failed command writes nothing.
///
/// <para>Same name and members as MVP-3's pre-wiring (commit 82ebedeb4, <c>Diten.ManufacturingService.Application.Common</c>),
/// so a service that prepared its declarations only changes the <c>using</c>.</para>
/// </summary>
public interface ICentralAuditDeclaration
{
    CentralAuditMetadata CentralAudit { get; }

    /// <summary>
    /// The audited entity's id. Explicit, not inferred: a create command carries no id and returns it inside its
    /// response, which a "property ending in Id" lookup cannot see. Null when the response names none.
    /// </summary>
    Guid? CentralAuditEntityId(object? response);
}

/// <summary>
/// What the central record says about the command. <see cref="Category"/> and <see cref="Operation"/> are Platform's own
/// enum NAMES (<c>Diten.Platform.Domain.Enums.AuditCategory</c> / <c>AuditOperation</c>); <see cref="SourceModule"/> is the
/// module code the service's manifest registers with Platform. The request body is never copied into the record — the
/// declaration names the entity, not its data (AUD-001 §3).
/// </summary>
public sealed record CentralAuditMetadata(string Category, string Operation, string EntityType, string SourceModule);
