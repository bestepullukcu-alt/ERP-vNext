namespace Diten.MdmService.Application.Contracts.Audit;

/// <summary>
/// Marker: a command whose execution should be forwarded to Platform's central audit store (S2S). Pair with
/// <see cref="IAuditMetadataProvider"/> so <c>AuditForwardingBehavior</c> knows what to record.
/// </summary>
public interface IAuditableCommand;
