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
            .Select(c => c.ToInput())
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

    /// <summary>WP-VP-4E — one draft week's day pins (absent = keep; an empty list clears that week's).</summary>
    public DayPinsRequest? DayPins { get; set; }

    /// <summary>WP-VP-4L (2) — one draft week's extra visits (absent = keep; an empty list clears that week's).</summary>
    public WeekExtrasRequest? WeekExtras { get; set; }

    public WeekExtrasInput? ToWeekExtras()
        => WeekExtras is null
            ? null
            : new WeekExtrasInput(WeekExtras.WeekStart, (WeekExtras.Targets ?? new List<WeekExtraRequest>())
                .Select(t => new WeekExtraInput(t.TargetType, t.TargetId, t.ContactId, t.AccountId))
                .ToList());

    public DayPinsInput? ToDayPins()
        => DayPins is null
            ? null
            : new DayPinsInput(DayPins.WeekStart, (DayPins.Pins ?? new List<DayPinRequest>())
                .Select(p => new DayPinInput(p.TargetType, p.TargetId, p.ContactId, p.Date, p.Scope, p.StartTime))
                .ToList());

    /// <summary>WP-VP-FIX-2 (D9) — an ABSENT doctor list stays null ("leave the doctors as they are"); an empty one is an
    /// explicit clear. (The create request keeps its own empty-list default.)</summary>
    public IReadOnlyList<SelectedContactInput>? ToContacts()
        => SelectedContacts?
            .Select(c => c.ToInput())
            .ToList();
}

public sealed class WeekExtrasRequest
{
    public string? WeekStart { get; set; }
    public List<WeekExtraRequest>? Targets { get; set; }
}

public sealed class WeekExtraRequest
{
    public string? TargetType { get; set; }
    public Guid TargetId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? AccountId { get; set; }
}

public sealed class DayPinsRequest
{
    public string? WeekStart { get; set; }
    public List<DayPinRequest>? Pins { get; set; }
}

public sealed class DayPinRequest
{
    public string? TargetType { get; set; }
    public Guid TargetId { get; set; }
    public Guid? ContactId { get; set; }
    public string? Date { get; set; }
    public string? Scope { get; set; }

    /// <summary>WP-VW-W2 (BE-b) — optional "HH:mm" start time of a visit pin (15-minute grid, working hours).</summary>
    public string? StartTime { get; set; }
}

public sealed class SelectedContactRequest
{
    public Guid ContactId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AccountContactLinkId { get; set; }

    /// <summary>WP-VP-3C (K-7, S-4) — null (absent) keeps the doctor's stored pick; [] clears it; a list sets it.</summary>
    public List<SelectedProductRequest>? Products { get; set; }

    public SelectedContactInput ToInput()
        => new(ContactId, AccountId, AccountContactLinkId,
            Products?.Select(p => new SelectedProductInput(p.ProductId, p.ProductCode, p.Role)).ToList());
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

    /// <summary>WP-VP-3A — the Monday (yyyy-MM-dd) to approve; absent ⇒ the whole-period apply (deprecated, Faz 4).</summary>
    public string? WeekStart { get; set; }
}

/// <summary>WP-VP-3A — reopen an approved week (reason ≥ 10 characters).</summary>
public sealed class ReopenPlanningWeekRequest
{
    public string? Reason { get; set; }
    public int? ExpectedVersion { get; set; }
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

/// <summary>WP-VP-3C — one product of a doctor's pick (MDM Global Product id; role promo / non-promo, null = promo).</summary>
public sealed class SelectedProductRequest
{
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? Role { get; set; }
}
