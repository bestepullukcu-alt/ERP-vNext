namespace Diten.PpmService.Domain.Repositories;

public sealed record AuditIntentDispatchMetadata(
    string SignatureScheme,
    string KeyId,
    string Signature);
