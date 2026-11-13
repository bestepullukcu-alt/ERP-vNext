namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.7: who changed what, when, old → new (lists, items, rules, rates). Append only; the
/// "change history" tab of screen D reads it.
/// </summary>
public sealed class PricingChangeLog : EntityBase
{
    /// <summary><see cref="PricingChangeEntityTypes"/>.</summary>
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int EntityVersion { get; set; }

    /// <summary>The list / rule the change belongs to (an item's list), for the history filter.</summary>
    public Guid? ParentId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? ByUserId { get; set; }
    public DateTimeOffset At { get; set; }
    public List<PricingFieldChange> Changes { get; set; } = new();
}

public sealed class PricingFieldChange
{
    public string Field { get; set; } = string.Empty;
    public string? Old { get; set; }
    public string? New { get; set; }
}

public static class PricingChangeEntityTypes
{
    public const string PriceList = "price-list";
    public const string PriceListItem = "price-list-item";
    public const string PricingRule = "pricing-rule";
    public const string FxRate = "fx-rate";

    public static readonly IReadOnlyList<string> All = new[] { PriceList, PriceListItem, PricingRule, FxRate };
}
