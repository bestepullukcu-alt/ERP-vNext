using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Domain.Entities;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

internal static class EmployeeDraftHandlerHelpers
{
    public static bool TryGetTenantId(ITenantContext tenantContext, out Guid tenantId)
    {
        tenantId = tenantContext.TenantId ?? Guid.Empty;
        return tenantContext.HasTenant && tenantId != Guid.Empty;
    }

    public static Response<T> MissingTenant<T>()
        => Response<T>.Fail("Tenant context is required.", 400);

    public static bool IsStale(string? ifMatch, EmployeeDraftSession draftSession)
        => string.IsNullOrWhiteSpace(ifMatch) || !string.Equals(ifMatch, draftSession.ETag, StringComparison.Ordinal);

    public static ReferenceValidationResponse BuildReferenceValidationResponse(IReadOnlyList<ReferenceValidationItem> results)
        => new(results, results.All(item => item.IsReferenceable));

    public static IReadOnlyList<string> BuildReviewBlockingReasons(EmployeeDraftSession draftSession)
    {
        var blockingReasons = new List<string>();

        if (!draftSession.ReferenceValidationSummary.CanReview)
        {
            blockingReasons.Add("references_not_validated");
        }

        var requiredPayloadKeys = new[]
        {
            "person_id",
            "legal_name",
            "worker_type",
            "employment_type",
            "hire_date",
            "organization_unit_id",
            "position_id",
            "legal_entity_id",
            "sensitivity_level"
        };

        var allPayload = draftSession.Steps.Values
            .SelectMany(step => step.Payload)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);

        foreach (var key in requiredPayloadKeys)
        {
            if (!allPayload.TryGetValue(key, out var value) || value is null || string.IsNullOrWhiteSpace(Convert.ToString(value)))
            {
                blockingReasons.Add($"missing_{key}");
            }
        }

        return blockingReasons;
    }
}
