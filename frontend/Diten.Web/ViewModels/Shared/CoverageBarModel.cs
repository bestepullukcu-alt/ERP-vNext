namespace Diten.Web.ViewModels.Shared;

// FEAT-ROLEPERMS-REDESIGN — reusable labelled percentage + progress bar. The percent text and bar width are
// live slots (PercentValueId / BarId) that page JS drives; reusable by any screen showing a coverage ratio.
public sealed record CoverageBarModel(string Label, string PercentValueId, string BarId, string Tone = "primary");
