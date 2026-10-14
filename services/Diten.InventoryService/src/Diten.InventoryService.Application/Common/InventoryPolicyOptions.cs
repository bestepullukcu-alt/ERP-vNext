namespace Diten.InventoryService.Application.Common;

/// <summary>Configuration section <c>Inventory:Policy</c>.</summary>
public sealed class InventoryPolicyOptions
{
    public const string SectionName = "Inventory:Policy";

    /// <summary>The only environment in which <see cref="EnforceLocationLegalEntityMatch"/> may be false.</summary>
    public const string MockEnvironmentName = "Mock";

    /// <summary>
    /// ASSUMPTION-0173-07: a location's <c>legalEntityId</c> must equal the caller's legal entity. ON by default.
    /// Prism static mocks return a fixed <c>le-tr</c>, so the check may be switched off only in the
    /// <see cref="MockEnvironmentName"/> environment; the host refuses to start with it off anywhere else.
    /// </summary>
    public bool EnforceLocationLegalEntityMatch { get; set; } = true;
}
