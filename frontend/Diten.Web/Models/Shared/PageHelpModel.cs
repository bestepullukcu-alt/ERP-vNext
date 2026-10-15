namespace Diten.Web.Models.Shared;

/// <summary>
/// WP-UX-COMMON-01 (BL-495) — what the shell hands its help partials: the page's stable code (null when the shell
/// cannot tell which page this is). A record, not a bare string, because a partial given a null model would silently
/// inherit the page's own model instead.
/// </summary>
public sealed record PageHelpModel(string? PageCode);
