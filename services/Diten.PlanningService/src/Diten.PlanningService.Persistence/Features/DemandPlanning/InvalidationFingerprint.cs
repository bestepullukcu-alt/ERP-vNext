using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

internal static class InvalidationFingerprint
{
    public static string Calculate(Guid tenantId, Guid legalEntityId,
        Guid revisionId, Guid actorId, string requestKey, string reason,
        InvalidationImpactCode impactCode, string evidenceReference,
        int expectedContentVersion, int expectedStateVersion)
    {
        var serialized = JsonSerializer.Serialize(new
        {
            tenantId, legalEntityId, revisionId, actorId,
            key = requestKey, reason, impactCode = impactCode.ToString(),
            evidenceReference, expectedContentVersion, expectedStateVersion
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}
