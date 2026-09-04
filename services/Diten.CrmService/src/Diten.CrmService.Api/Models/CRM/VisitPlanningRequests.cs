using Diten.CrmService.Application.Features.VisitPlanning.Commands;

namespace Diten.CrmService.Api.Models.CRM;

/// <summary>
/// MOD-0155 FU05 request bodies for the Visit Planning setup endpoints. TenantId appears in none of them — it is
/// server-resolved from the claim. Coordinates are optional day-1 seeds; dates/times never appear here (they are
/// DERIVED by the engine from the CyclePeriod + FU03 route).
/// </summary>
public sealed class CreatePlanningSessionRequest
{
    public Guid CyclePeriodId { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceDisplayName { get; set; }
    public List<Guid>? SelectedAccountIds { get; set; }
    public List<Guid>? SelectedPharmacyIds { get; set; }
    public List<SelectedContactRequest>? SelectedContacts { get; set; }
    public Guid? SegmentId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? StrategyTemplateId { get; set; }
    public string? TargetWeekStart { get; set; }

    public IReadOnlyList<SelectedContactInput> ToContacts()
        => (SelectedContacts ?? new List<SelectedContactRequest>())
            .Select(c => new SelectedContactInput(c.ContactId, c.AccountId, c.AccountContactLinkId))
            .ToList();
}

public sealed class UpdatePlanningSessionRequest
{
    public List<Guid>? SelectedAccountIds { get; set; }
    public List<Guid>? SelectedPharmacyIds { get; set; }
    public List<SelectedContactRequest>? SelectedContacts { get; set; }
    public Guid? SegmentId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? StrategyTemplateId { get; set; }
    public string? RequestedStatus { get; set; }
    public int? ExpectedVersion { get; set; }
    public string? TargetWeekStart { get; set; }

    public IReadOnlyList<SelectedContactInput> ToContacts()
        => (SelectedContacts ?? new List<SelectedContactRequest>())
            .Select(c => new SelectedContactInput(c.ContactId, c.AccountId, c.AccountContactLinkId))
            .ToList();
}

public sealed class SelectedContactRequest
{
    public Guid ContactId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AccountContactLinkId { get; set; }
}

public sealed class GeneratePlanPreviewRequest
{
    public Guid PlanningSessionId { get; set; }
    public string? VisitPurpose { get; set; }
    public string? VisitType { get; set; }
    public double? StartLat { get; set; }
    public double? StartLong { get; set; }

    /// <summary>Optional manual visiting order (target ids, first→last). Present ⇒ the preview honors this sequence.</summary>
    public List<Guid>? ManualVisitOrder { get; set; }
}

public sealed class ApplyPlanRequest
{
    public Guid PlanningSessionId { get; set; }
    public string? VisitPurpose { get; set; }
    public string? VisitType { get; set; }
    public double? StartLat { get; set; }
    public double? StartLong { get; set; }
    public int? ExpectedVersion { get; set; }

    /// <summary>Optional manual visiting order (target ids) — persisted on the session as "this week's plan".</summary>
    public List<Guid>? ManualVisitOrder { get; set; }
}

public sealed class ReplanPlanRequest
{
    public Guid PlanningSessionId { get; set; }
    public List<Guid> AffectedContactIds { get; set; } = new();
    public string? VisitPurpose { get; set; }
    public string? VisitType { get; set; }
    public double? StartLat { get; set; }
    public double? StartLong { get; set; }

    /// <summary>Optional manual visiting order (target ids) for the re-planned subset.</summary>
    public List<Guid>? ManualVisitOrder { get; set; }
}
