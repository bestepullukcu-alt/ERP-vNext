namespace Diten.MdmService.Application.Contracts.Audit;

/// <summary>
/// Self-declared audit metadata for an <see cref="IAuditableCommand"/>. EntityId and outcome are NOT here — the
/// behavior derives EntityId from the command id (update/lifecycle) or the handler response (create), and the outcome
/// from the response.
/// </summary>
public interface IAuditMetadataProvider
{
    AuditMetadata GetAuditMetadata();
}

public sealed record AuditMetadata(
    AuditCategory Category,
    AuditOperation Operation,
    string EntityType,
    string SourceModule);
