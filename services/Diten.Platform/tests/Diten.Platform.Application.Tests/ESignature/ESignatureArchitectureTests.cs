using System.Reflection;
using Diten.Platform.API.Controllers.ESignature;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.ESignature.SelfRegistration;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — the structural guards (pack §16 E2 / §17 "Architecture"): insert-only repositories, the manifest's tenant
/// scope and namespace, and the controllers' keys.
/// </summary>
public sealed class ESignatureArchitectureTests
{
    // Any member whose name starts like this changes or removes stored evidence.
    private static readonly string[] MutatingPrefixes = ["Update", "Replace", "Delete", "Remove", "Upsert", "Save", "Set", "Mark", "Patch", "Modify", "Invalidate", "Purge", "Drop"];

    private static IReadOnlyList<Type> InsertOnlyInterfaces() =>
        typeof(IESignatureRecordRepository).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.StartsWith("IESignature", StringComparison.Ordinal) && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .OrderBy(t => t.Name)
            .ToList();

    /// <summary>E2's four evidence stores + E3's sample and sample-event stores.</summary>
    [Fact]
    public void The_insert_only_repository_interfaces_are_the_six_evidence_stores()
    {
        Assert.Equal(
            new[]
            {
                nameof(IESignatureExportRepository), nameof(IESignatureInvalidationRepository), nameof(IESignaturePolicyChangeRepository),
                nameof(IESignatureRecordRepository), nameof(IESignatureSampleEventRepository), nameof(IESignatureSampleRepository)
            },
            InsertOnlyInterfaces().Select(t => t.Name).ToArray());
    }

    [Fact]
    public void Insert_only_repository_interfaces_have_no_update_replace_or_delete_member()
    {
        var offenders = InsertOnlyInterfaces()
            .SelectMany(i => i.GetMethods().Select(m => $"{i.Name}.{m.Name}"))
            .Where(name => MutatingPrefixes.Any(prefix => name.Split('.')[1].StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();
        Assert.True(offenders.Count == 0, "insert-only repository exposes a mutator: " + string.Join(", ", offenders));

        // …and everything else is an insert or a read.
        var unexpected = InsertOnlyInterfaces()
            .SelectMany(i => i.GetMethods().Select(m => (i, m)))
            .Where(x => x.m.Name != "InsertAsync" && !x.m.Name.StartsWith("Get", StringComparison.Ordinal)
                        && !x.m.Name.StartsWith("List", StringComparison.Ordinal) && !x.m.Name.StartsWith("Search", StringComparison.Ordinal))
            .Select(x => $"{x.i.Name}.{x.m.Name}")
            .ToList();
        Assert.True(unexpected.Count == 0, "insert-only repository has a member that is neither an insert nor a read: " + string.Join(", ", unexpected));
    }

    [Fact]
    public void Insert_only_implementations_expose_nothing_beyond_their_interface_and_inherit_no_delete()
    {
        var implementations = typeof(ESignatureRecordRepository).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => InsertOnlyInterfaces().Contains(i)))
            .ToList();
        Assert.Equal(6, implementations.Count);

        foreach (var implementation in implementations)
        {
            for (var baseType = implementation.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                Assert.False(baseType.IsGenericType && (baseType.GetGenericTypeDefinition() == typeof(Diten.Platform.Common.Persistence.RepositoryBase<>)
                        || baseType.GetGenericTypeDefinition() == typeof(Diten.Platform.Infrastructure.Persistence.Repositories.RepositoryBase<>)),
                    $"{implementation.Name} derives from RepositoryBase<T> and inherits its public soft delete");
            }

            var publicMethods = implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name);
            var interfaceMethods = implementation.GetInterfaces().Where(i => InsertOnlyInterfaces().Contains(i)).SelectMany(i => i.GetMethods()).Select(m => m.Name);
            var extra = publicMethods.Except(interfaceMethods).ToList();
            Assert.True(extra.Count == 0, $"{implementation.Name} exposes members outside its insert-only interface: {string.Join(", ", extra)}");
        }
    }

    // ── manifest (KS-0, CT-D8, CT-D10) ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Manifest_routes_are_tenant_routes_and_never_under_Platform()
    {
        var manifest = new ESignManifestProvider().GetManifest();
        Assert.All(manifest.Pages, page =>
        {
            Assert.False(page.RoutePath.StartsWith("/Platform", StringComparison.OrdinalIgnoreCase), $"{page.PageCode} is under /Platform");
            Assert.Equal("Tenant", ModulePageDescriptorNormalizer.ScopeFromRoute(page.RoutePath));
            Assert.StartsWith("/ESignature/", page.RoutePath, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Manifest_declares_only_esign_keys_every_esign_key_and_the_api_only_ones_as_tenant()
    {
        var manifest = new ESignManifestProvider().GetManifest();
        Assert.Equal("esign", manifest.ModuleCode);
        Assert.True(manifest.IsBaseline);

        var pageKeys = manifest.Pages.Select(p => p.RequiredPermission);
        var actionKeys = manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey);
        var apiKeys = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToList();
        var all = pageKeys.Concat(actionKeys).Concat(apiKeys).Distinct().OrderBy(k => k).ToList();

        Assert.All(all, key => Assert.StartsWith("esign.", key, StringComparison.Ordinal));
        Assert.Equal(ESignPermissions.All.OrderBy(k => k).ToList(), all);
        Assert.Equal(new[] { ESignPermissions.RequestsCreate, ESignPermissions.SignaturesSign }, apiKeys.OrderBy(k => k).ToArray());
        Assert.All(manifest.ApiPermissions!, p => Assert.Equal("Tenant", p.Scope));
    }

    [Fact]
    public void Every_esign_endpoint_is_gated_by_an_esign_key_the_manifest_declares()
    {
        var manifest = new ESignManifestProvider().GetManifest();
        var declared = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(p => p.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);
        var controllers = typeof(ESignatureSignaturesController).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ESignatureSignaturesController).Namespace && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();
        Assert.Equal(5, controllers.Count);

        foreach (var action in controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        {
            var route = action.DeclaringType!.GetCustomAttribute<RouteAttribute>()!.Template;
            Assert.StartsWith("api/v1/esign", route, StringComparison.Ordinal);
            var gate = action.GetCustomAttribute<HasPermissionAttribute>();
            if (gate is null)
            {
                // The two multi-key endpoints check their keys themselves: §4.20 (esign.meanings.read OR esign.signatures.sign)
                // and the §4.17 sample image (owner key, approver key or signature-read key).
                Assert.Contains(action.Name, new[] { nameof(ESignatureSettingsController.ListMeanings), nameof(ESignatureSamplesController.Image) });
                continue;
            }

            Assert.Contains(gate.Permission, declared);
        }
    }
}
