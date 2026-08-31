using Diten.Platform.Domain.Enums;
using Diten.PpmService.Contracts.Events;

namespace Diten.Platform.Infrastructure.Eventing;

/// <summary>
/// Maps the PPM-owned V1 payload after its transport identity and signature have been verified.
/// This type neither verifies transport metadata nor performs a MongoDB write; those concerns stay
/// fail-closed until the shared PPM publisher-identity/key-provider binding is approved.
/// </summary>
internal static class PpmAuditIntentV1AuditMapping
{
    public const string SourceService = "Diten.PpmService";

    public static PpmAuditIntentV1AuditProjection Map(PpmAuditIntentSubmittedV1 auditIntent)
    {
        ArgumentNullException.ThrowIfNull(auditIntent);

        return new PpmAuditIntentV1AuditProjection(
            auditIntent.AuditIntentId,
            auditIntent.ActorId,
            auditIntent.EntityType,
            auditIntent.EntityId,
            MapOperation(auditIntent.Mutation),
            auditIntent.OccurredAtUtc);
    }

    private static AuditOperation MapOperation(string mutation) => mutation switch
    {
        "created" => AuditOperation.Create,
        "updated" => AuditOperation.Update,
        "lifecycle-changed" => AuditOperation.LifecycleTransition,
        "soft-deleted" => AuditOperation.Delete,
        _ => throw new InvalidOperationException("PPM V1 audit mutation is not recognized.")
    };
}

internal sealed record PpmAuditIntentV1AuditProjection(
    Guid AuditIntentId,
    Guid ActorId,
    string EntityType,
    Guid EntityId,
    AuditOperation Operation,
    DateTime OccurredAtUtc)
{
    public AuditCategory Category => AuditCategory.PortfolioDelivery;
}
