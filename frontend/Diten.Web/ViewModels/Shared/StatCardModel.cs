namespace Diten.Web.ViewModels.Shared;

// FEAT-ROLEPERMS-REDESIGN — reusable compact stat card (icon + value + label). The value is a live slot
// (ValueId) that page JS updates by id; other screens can reuse the partial with a static Value and no ValueId.
public sealed record StatCardModel(string Icon, string Label, string Tone, string Value = "0", string? ValueId = null);
