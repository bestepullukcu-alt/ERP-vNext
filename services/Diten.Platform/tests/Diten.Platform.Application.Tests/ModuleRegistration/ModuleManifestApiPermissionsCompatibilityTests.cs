using System.Text.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleRegistration;

// FU-RSV-01 (WP-0173-RSV1) — ApiPermissions is ADDITIVE + OPTIONAL: a payload pushed by a service built before the field
// existed must still bind (web JSON defaults, as the internal register endpoint binds it), and a declared entry
// round-trips with its scope (default "Tenant" when omitted).
public sealed class ModuleManifestApiPermissionsCompatibilityTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string OldPayload = """
        {
          "moduleCode": "GOLDENSLIM", "moduleName": "Golden Reference Slim", "displayName": "Golden Slim",
          "domain": "DevEnablement", "service": "DevEnablement", "moduleVersion": "1.0.0",
          "isTenantAssignable": true, "sortOrder": 100,
          "pages": [ { "pageCode": "RECORDS", "displayName": "Records", "routePath": "/GoldenReferenceSlim",
                       "requiredPermission": "goldenslim.records.read", "parentPageCode": null,
                       "isNavigationVisible": true, "pageType": "List", "sortOrder": 10, "actions": [] } ],
          "icon": "bx-box", "isBaseline": false
        }
        """;

    [Fact]
    public void An_old_payload_without_the_field_deserializes_with_null_api_permissions()
    {
        var manifest = JsonSerializer.Deserialize<ModuleManifestDocument>(OldPayload, Web)!;

        Assert.Equal("GOLDENSLIM", manifest.ModuleCode);
        Assert.Single(manifest.Pages);
        Assert.Null(manifest.NotificationEvents);
        Assert.Null(manifest.ApiPermissions);
    }

    [Fact]
    public void A_declared_api_permission_round_trips_and_its_scope_defaults_to_Tenant()
    {
        var json = OldPayload.TrimEnd().TrimEnd('}') + """
            , "apiPermissions": [
                { "permissionKey": "goldenslim.records.reserve", "displayName": "Reserve records" },
                { "permissionKey": "goldenslim.ops.purge", "displayName": "Purge", "scope": "PlatformAdmin" } ]
            }
            """;

        var manifest = JsonSerializer.Deserialize<ModuleManifestDocument>(json, Web)!;
        var again = JsonSerializer.Deserialize<ModuleManifestDocument>(JsonSerializer.Serialize(manifest, Web), Web)!;

        Assert.Equal(
            [new ModuleManifestPermission("goldenslim.records.reserve", "Reserve records", "Tenant"),
             new ModuleManifestPermission("goldenslim.ops.purge", "Purge", "PlatformAdmin")],
            again.ApiPermissions!);
    }
}
