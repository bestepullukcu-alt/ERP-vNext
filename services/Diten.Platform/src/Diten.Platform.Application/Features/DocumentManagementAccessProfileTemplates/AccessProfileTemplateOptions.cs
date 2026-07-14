namespace Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;

/// <summary>
/// MOD-0029-FU05 — maps the logical template roles to concrete role principal ids (the string stored in
/// <c>DocumentAccessPolicyEntry.PrincipalId</c> with <c>PrincipalType = Role</c>). Bound from configuration section
/// <see cref="SectionName"/>; the defaults are stable role KEYS (not real RBAC role ids) so the engine is usable
/// out of the box. A blank mapping means "no principal for this role" — the engine then emits a missing-principal
/// finding and skips that rule rather than seeding an unknown role.
/// </summary>
public sealed class AccessProfileTemplateOptions
{
    public const string SectionName = "DocumentManagement:AccessProfileTemplates";

    public string? QaDocumentation { get; set; } = "qa-documentation";
    public string? Gqd { get; set; } = "gqd";
    public string? LocalQa { get; set; } = "local-qa";
    public string? RecordsOwner { get; set; } = "records-owner";
    public string? Hr { get; set; } = "hr";
    public string? Legal { get; set; } = "legal";
    public string? CountryManager { get; set; } = "country-manager";
    public string? DepartmentOwner { get; set; } = "department-owner";
    public string? SiteQa { get; set; } = "site-qa";
    public string? SiteHead { get; set; } = "site-head";
    public string? OwnerFunction { get; set; } = "owner-function";

    /// <summary>Broad "all tenant users" role. Empty by default — a broad grant is only issued if explicitly configured.</summary>
    public string? AllUsers { get; set; }

    /// <summary>Resolves a logical role to its configured principal id, or null when unmapped/blank.</summary>
    public string? Resolve(LogicalTemplateRole role)
    {
        var value = role switch
        {
            LogicalTemplateRole.QaDocumentation => QaDocumentation,
            LogicalTemplateRole.Gqd => Gqd,
            LogicalTemplateRole.LocalQa => LocalQa,
            LogicalTemplateRole.RecordsOwner => RecordsOwner,
            LogicalTemplateRole.Hr => Hr,
            LogicalTemplateRole.Legal => Legal,
            LogicalTemplateRole.CountryManager => CountryManager,
            LogicalTemplateRole.DepartmentOwner => DepartmentOwner,
            LogicalTemplateRole.SiteQa => SiteQa,
            LogicalTemplateRole.SiteHead => SiteHead,
            LogicalTemplateRole.OwnerFunction => OwnerFunction,
            LogicalTemplateRole.AllUsers => AllUsers,
            _ => null
        };

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
