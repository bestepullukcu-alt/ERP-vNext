using System.Reflection;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// WC-D1 Y2 — A MANIFEST MAY NOT CARRY AN ADDRESS.
///
/// <para><b>What this is defending.</b> A self-registration manifest is CLIENT-SUPPLIED: the module being called
/// pushes it to Platform at startup. The remote work-item bridge sends the CALLER'S OWN BEARER TOKEN to whatever
/// address it is configured with. Put those two facts together and an address inside a manifest is the callee
/// telling Platform where to forward a caller's credential — a redirect written by the party being redirected to.
/// The address therefore lives in <c>RemoteWorkItemProviderOptions.BaseUrl</c>, written by an OPERATOR, in the
/// same shape as <c>MdmService:BaseUrl</c> and the repository's other inter-service addresses.</para>
///
/// <para>Until this file that rule was a SENTENCE, in two class comments and in
/// <c>.antigravity/rules/workcenter-bridge-standard.md</c>. It was true on 2026-08-28 only because nobody had
/// added such a field yet; adding one would have broken nothing.</para>
/// </summary>
public sealed class ModuleManifestCarriesNoAddressTests
{
    private static readonly Assembly ManifestAssembly = typeof(ModuleManifestDocument).Assembly;

    /// <summary>
    /// Word fragments that appear in the name of a field capable of carrying a network location. Deliberately
    /// broad: a false positive costs one line in <see cref="NamesClearedAsNotAddresses"/> plus the thought that
    /// goes with it, and a false negative costs a credential.
    /// </summary>
    private static readonly string[] AddressShapedFragments =
    [
        "address", "url", "uri", "host", "endpoint", "origin", "callback", "webhook",
        "server", "domainname", "port", "path", "link", "location", "target", "destination"
    ];

    /// <summary>
    /// Names that MATCH a fragment above and have been looked at and cleared, each with the reason. A name only
    /// belongs here after someone has decided it cannot carry a scheme/host/port.
    /// </summary>
    private static readonly Dictionary<string, string> NamesClearedAsNotAddresses = new(StringComparer.Ordinal)
    {
        // A relative in-app route on the tenant's own frontend ("/Tasks/Index"). It names a page in a UI Platform
        // already serves — it does not name a service Platform would call.
        ["RoutePath"] = "relative tenant-frontend route, not a service Platform calls",

        // Identifiers, resolved against the Module Catalog / ModulePages at sync time. They name a row, not a host.
        ["TargetPageCode"] = "page code, validated against ModulePages",
        ["TargetRouteDescriptorId"] = "descriptor id, validated against the catalog",

        // An enum-like policy string ("Internal"/"None"), not a destination.
        ["LinkPolicy"] = "policy name, not a destination"
    };

    [Fact]
    public void No_manifest_type_declares_a_field_whose_name_could_carry_a_network_address()
    {
        var offenders = new List<string>();

        foreach (var (type, property) in ManifestProperties())
        {
            var lowered = property.Name.ToLowerInvariant();

            if (!AddressShapedFragments.Any(f => lowered.Contains(f, StringComparison.Ordinal)))
            {
                continue;
            }

            if (NamesClearedAsNotAddresses.ContainsKey(property.Name))
            {
                continue;
            }

            offenders.Add($"{type.Name}.{property.Name} ({property.PropertyType.Name})");
        }

        Assert.True(
            offenders.Count == 0,
            "A self-registration manifest is client-supplied and MUST NOT carry an address: the module being "
            + "called would be telling Platform where to forward a caller's bearer token. Put the address in "
            + "'WorkAggregation:RemoteProviders[].BaseUrl', written by an operator. If the field below genuinely "
            + "cannot carry a scheme/host/port, add it to NamesClearedAsNotAddresses WITH THE REASON."
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// THE LIMIT OF THE TEST ABOVE, MEASURED AND WRITTEN DOWN RATHER THAN HIDDEN.
    ///
    /// <para>A name check cannot catch a name it was not told about. <c>CallbackTarget</c> and <c>WebhookUri</c>
    /// are caught; <c>Where</c>, <c>Reachable</c>, <c>Upstream</c> or a plain <c>string Value</c> are NOT — and a
    /// field added deliberately is exactly the case that would pick such a name. So the name check is the
    /// readable first line, and this is the one that actually holds: the manifest's property set is PINNED. Any
    /// new field on any manifest record fails here, whatever it is called.</para>
    ///
    /// <para><b>This will fail on legitimate additive fields too, and that is the price.</b> The manifest is
    /// designed to grow (see the NotificationEvents note on <see cref="ModuleManifestDocument"/>), so this test
    /// is not saying "never add a field" — it is saying a human looks at every field added to a client-supplied
    /// contract and confirms it is not an address before the build goes green. That costs one line here. The
    /// alternative costs a token.</para>
    /// </summary>
    [Fact]
    public void The_manifest_property_set_is_pinned_so_a_field_the_name_check_would_miss_still_fails()
    {
        var actual = ManifestProperties()
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        string[] expected =
        [
            "ModuleManifestAction.ActionCode",
            "ModuleManifestAction.ActionType",
            "ModuleManifestAction.DisplayName",
            "ModuleManifestAction.IsDangerous",
            "ModuleManifestAction.IsRowAction",
            "ModuleManifestAction.IsToolbarAction",
            "ModuleManifestAction.PermissionKey",
            "ModuleManifestAction.SortOrder",
            "ModuleManifestDocument.DisplayName",
            "ModuleManifestDocument.Domain",
            "ModuleManifestDocument.Icon",
            "ModuleManifestDocument.IsBaseline",
            "ModuleManifestDocument.IsTenantAssignable",
            "ModuleManifestDocument.ModuleCode",
            "ModuleManifestDocument.ModuleName",
            "ModuleManifestDocument.ModuleVersion",
            "ModuleManifestDocument.NotificationEvents",
            "ModuleManifestDocument.Pages",
            "ModuleManifestDocument.Service",
            "ModuleManifestDocument.SortOrder",
            "ModuleManifestNotificationEvent.CanTenantOverride",
            "ModuleManifestNotificationEvent.Channel",
            "ModuleManifestNotificationEvent.DefaultTemplateKey",
            "ModuleManifestNotificationEvent.Description",
            "ModuleManifestNotificationEvent.DisplayNameKey",
            "ModuleManifestNotificationEvent.EventCode",
            "ModuleManifestNotificationEvent.FallbackDisplayName",
            "ModuleManifestNotificationEvent.LinkPolicy",
            "ModuleManifestNotificationEvent.OptionalVariables",
            "ModuleManifestNotificationEvent.RequiredPermissionKey",
            "ModuleManifestNotificationEvent.RequiredVariables",
            "ModuleManifestNotificationEvent.SeverityDefault",
            "ModuleManifestNotificationEvent.Status",
            "ModuleManifestNotificationEvent.TargetPageCode",
            "ModuleManifestNotificationEvent.TargetRouteDescriptorId",
            "ModuleManifestNotificationEvent.UsageType",
            "ModuleManifestNotificationVariable.IsRequired",
            "ModuleManifestNotificationVariable.Name",
            "ModuleManifestNotificationVariable.Type",
            "ModuleManifestPage.Actions",
            "ModuleManifestPage.DisplayName",
            "ModuleManifestPage.IsNavigationVisible",
            "ModuleManifestPage.PageCode",
            "ModuleManifestPage.PageType",
            "ModuleManifestPage.ParentPageCode",
            "ModuleManifestPage.RequiredPermission",
            "ModuleManifestPage.RoutePath",
            "ModuleManifestPage.SortOrder"
        ];

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Non-vacuity. If the manifest types were renamed or moved out of this assembly both tests above would pass
    /// by inspecting nothing.
    /// </summary>
    [Fact]
    public void The_guard_is_looking_at_an_assembly_that_actually_holds_the_manifest_types()
    {
        var properties = ManifestProperties().ToList();

        Assert.NotEmpty(properties);
        Assert.Contains(properties, x => x.Type == typeof(ModuleManifestDocument));
        Assert.Contains(properties, x => x.Type == typeof(ModuleManifestPage));

        // The fragment list must be able to fire at all — RoutePath matches "path" and is only silent because it
        // was cleared by name. If this stops being true the first test has gone vacuous.
        Assert.Contains("RoutePath", NamesClearedAsNotAddresses.Keys);
        Assert.Contains(AddressShapedFragments, f => "routepath".Contains(f, StringComparison.Ordinal));
    }

    private static IEnumerable<(Type Type, PropertyInfo Property)> ManifestProperties()
        => ManifestAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Name.StartsWith("ModuleManifest", StringComparison.Ordinal))
            .SelectMany(t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => (Type: t, Property: p)));
}
