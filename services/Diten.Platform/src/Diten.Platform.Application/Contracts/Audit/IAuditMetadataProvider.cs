namespace Diten.Platform.Application.Contracts.Audit;

public interface IAuditMetadataProvider
{
    AuditRequestMetadata GetAuditMetadata();
}
