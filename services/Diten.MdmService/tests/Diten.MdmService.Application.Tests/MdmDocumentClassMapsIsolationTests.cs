using System.Reflection;
using System.Runtime.Loader;
using Diten.MdmService.Persistence.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-BL576-01 FIX1 — the PRODUCTION registration path proven in a fresh driver, apart from this process's own maps
/// (the test module initializer registers them at assembly load, so here AddPersistence's own call would hit the early
/// return and prove nothing). Each test loads MDM's assemblies and the Mongo driver into a collectible context.
/// </summary>
public sealed class MdmDocumentClassMapsIsolationTests
{
    private static readonly string Bin = Path.GetDirectoryName(typeof(MdmDocumentClassMapsIsolationTests).Assembly.Location)!;

    [Fact]
    public void R1a_AddPersistence_in_a_fresh_driver_registers_every_covered_map_and_production_guid_storage()
    {
        var context = new IsolatedMdm(Bin);
        try
        {
            var (bson, persistence) = (Load(context, "MongoDB.Bson"), Load(context, "Diten.MdmService.Persistence"));
            var addPersistence = persistence.GetType("Diten.MdmService.Persistence.DependencyInjection", true)!
                .GetMethod("AddPersistence")!;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100",
                ["Mongo:DatabaseName"] = "diten_mdm_bl576_isolated_never_opened"
            }).Build();

            addPersistence.Invoke(null, [new ServiceCollection(), configuration, false]);

            var maps = persistence.GetType("Diten.MdmService.Persistence.Configurations.MdmDocumentClassMaps", true)!;
            var covered = (IEnumerable<Type>)maps.GetProperty("CoveredTypes")!.GetValue(null)!;
            var strict = (IEnumerable<Type>)maps.GetProperty("StrictByDesign")!.GetValue(null)!;
            var classMap = bson.GetType("MongoDB.Bson.Serialization.BsonClassMap", true)!;
            var lookup = classMap.GetMethod("LookupClassMap")!;
            var ignores = classMap.GetProperty("IgnoreExtraElements")!;
            Assert.All(covered, type => Assert.True((bool)ignores.GetValue(lookup.Invoke(null, [type]))!, type.Name));
            Assert.All(strict, type => Assert.False((bool)ignores.GetValue(lookup.Invoke(null, [type]))!, type.Name));
            Assert.Equal("Standard", GuidRepresentationOf(bson));
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void R1b_A_document_type_read_before_registration_stops_the_production_register_by_name()
    {
        var context = new IsolatedMdm(Bin);
        try
        {
            var (bson, domain, persistence) =
                (Load(context, "MongoDB.Bson"), Load(context, "Diten.MdmService.Domain"), Load(context, "Diten.MdmService.Persistence"));
            RegisterStandardGuid(bson);
            var lsku = domain.GetType("Diten.MdmService.Domain.Entities.Lsku", true)!;
            // The implicit auto-map a first read makes — frozen without the flag.
            bson.GetType("MongoDB.Bson.Serialization.BsonClassMap", true)!.GetMethod("LookupClassMap")!.Invoke(null, [lsku]);
            var register = persistence.GetType("Diten.MdmService.Persistence.Configurations.MdmDocumentClassMaps", true)!
                .GetMethod("Register")!;

            var refused = Assert.Throws<TargetInvocationException>(() => register.Invoke(null, null));

            Assert.IsType<InvalidOperationException>(refused.InnerException);
            Assert.Contains("Lsku", refused.InnerException!.Message, StringComparison.Ordinal);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void C2_The_one_shot_selected_delivery_path_registers_every_covered_map_without_the_host()
    {
        // WP-MDM-BL576-01 FIX2 — CreateSelected runs before (without) AddPersistence; in a fresh driver its own Register() is
        // the only thing that can flag the maps. Its constructor already takes typed collections (Gsku, Lsku, …), so without
        // that call those types freeze as plain auto-maps here.
        var context = new IsolatedMdm(Bin);
        try
        {
            var (bson, domain, persistence) =
                (Load(context, "MongoDB.Bson"), Load(context, "Diten.MdmService.Domain"), Load(context, "Diten.MdmService.Persistence"));
            var tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var aggregateType = domain.GetType("Diten.MdmService.Domain.Enums.AuditAggregateType", true)!;
            var locator = Activator.CreateInstance(domain.GetType("Diten.MdmService.Domain.Repositories.AuditIntentLocator", true)!,
                tenantId, Enum.Parse(aggregateType, "Gsku"), Guid.Parse("22222222-2222-3333-4444-555555555555"),
                Guid.Parse("33333333-2222-3333-4444-555555555555"))!;
            var itemType = domain.GetType("Diten.MdmService.Domain.Repositories.SelectedAuditIntentDeliveryItem", true)!;
            var items = Array.CreateInstance(itemType, 1);
            items.SetValue(Activator.CreateInstance(itemType, locator, 0L, new string('a', 64)), 0);
            var request = Activator.CreateInstance(
                domain.GetType("Diten.MdmService.Domain.Repositories.SelectedAuditIntentDeliveryRequest", true)!,
                Guid.Parse("44444444-2222-3333-4444-555555555555"), tenantId, items)!;
            var createSelected = persistence.GetType("Diten.MdmService.Persistence.Repositories.AuditIntentDeliveryRepository", true)!
                .GetMethods().Single(method => method.Name == "CreateSelected" && method.GetParameters()[0].ParameterType == typeof(string));

            // The client connects lazily — nothing here opens a socket.
            createSelected.Invoke(null, ["mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100",
                "diten_mdm_bl576_isolated_never_opened", request, TimeProvider.System]);

            var maps = persistence.GetType("Diten.MdmService.Persistence.Configurations.MdmDocumentClassMaps", true)!;
            var covered = ((IEnumerable<Type>)maps.GetProperty("CoveredTypes")!.GetValue(null)!).ToList();
            var classMap = bson.GetType("MongoDB.Bson.Serialization.BsonClassMap", true)!;
            var lookup = classMap.GetMethod("LookupClassMap")!;
            var ignores = classMap.GetProperty("IgnoreExtraElements")!;
            Assert.Contains(covered, type => type.Name == "Gsku");
            Assert.All(covered, type => Assert.True((bool)ignores.GetValue(lookup.Invoke(null, [type]))!, type.Name));
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void M2_Every_map_this_registry_makes_writes_the_same_bytes_as_the_drivers_own_automap()
    {
        // "The existing mapping does not change", measured on the bytes: context A = production's Guid serializer only (the
        // driver auto-maps); context B = the same plus Register(). Every covered type this registry maps itself is
        // serialized from the same deterministic sample in both; Guids stay subtype 4 (UuidStandard).
        var withoutMaps = SerializeAll(registerMaps: false);
        var withMaps = SerializeAll(registerMaps: true);

        Assert.Equal(withoutMaps.Keys.Order(StringComparer.Ordinal), withMaps.Keys.Order(StringComparer.Ordinal));
        Assert.True(withoutMaps.Count >= 30, $"only {withoutMaps.Count} types serialized — the comparison is blind");
        foreach (var (name, bytes) in withoutMaps)
        {
            Assert.True(bytes.AsSpan().SequenceEqual(withMaps[name]), $"{name}: the registered map writes other bytes");
            var document = new RawBsonDocument(bytes);
            if (document.Contains("_id") && document["_id"] is BsonBinaryData id)
                Assert.Equal(BsonBinarySubType.UuidStandard, id.SubType);
        }
    }

    private static Dictionary<string, byte[]> SerializeAll(bool registerMaps)
    {
        var context = new IsolatedMdm(Bin);
        try
        {
            var (bson, persistence) = (Load(context, "MongoDB.Bson"), Load(context, "Diten.MdmService.Persistence"));
            RegisterStandardGuid(bson);
            var maps = persistence.GetType("Diten.MdmService.Persistence.Configurations.MdmDocumentClassMaps", true)!;
            var covered = ((IEnumerable<Type>)maps.GetProperty("CoveredTypes")!.GetValue(null)!).ToList();
            if (registerMaps) maps.GetMethod("Register")!.Invoke(null, null);
            // ToBson(this object, Type nominalType, …optional) — the overload with the nominal type; its optional
            // parameters get their defaults.
            var toBson = bson.GetType("MongoDB.Bson.BsonExtensionMethods", true)!.GetMethods()
                .Where(method => method.Name == "ToBson" && !method.IsGenericMethodDefinition
                                 && method.GetParameters() is { Length: >= 2 } p && p[1].ParameterType == typeof(Type))
                .OrderByDescending(method => method.GetParameters().Length).First();
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var type in covered.Where(type => !ExplicitlyMappedNames.Contains(type.Name)))
            {
                var sample = Sample(type);
                var arguments = toBson.GetParameters().Select((parameter, index) => index switch
                {
                    0 => sample,
                    1 => type,
                    _ => parameter.HasDefaultValue && parameter.DefaultValue is not null and not DBNull ? parameter.DefaultValue
                        : parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null
                }).ToArray();
                result[type.FullName!] = (byte[])toBson.Invoke(null, arguments)!;
            }
            return result;
        }
        finally
        {
            context.Unload();
        }
    }

    // Brand / Product / the GP lifecycle requests keep their OWN explicit maps (not this registry's): named in the report.
    private static readonly HashSet<string> ExplicitlyMappedNames =
    [
        "Brand", "Product", "BrandProductExternalReference", "BrandHistoryEntry", "BrandAuditSnapshot",
        "ExternalReferenceReservation", "GlobalProductCorrectionOperation", "GlobalProductRetirementRequestOperation",
        "GlobalProductIdentityWorkflowOperation"
    ];

    private static object Sample(Type type)
    {
        var instance = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes) is { } parameterless
            ? parameterless.Invoke([])
            : type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First() is var constructor
                ? constructor.Invoke(constructor.GetParameters()
                    .Select(p => p.ParameterType == typeof(string) ? string.Empty
                        : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray())
                : throw new InvalidOperationException(type.Name);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            if (property.PropertyType == typeof(Guid)) property.SetValue(instance, Guid.Parse("11111111-2222-3333-4444-555555555555"));
            else if (property.PropertyType == typeof(DateTimeOffset)) property.SetValue(instance, new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
            else if (property.PropertyType == typeof(DateTimeOffset?)) property.SetValue(instance, null);
            else if (property.PropertyType == typeof(DateTime)) property.SetValue(instance, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc));
        }
        return instance;
    }

    private static void RegisterStandardGuid(Assembly bson)
    {
        var representation = bson.GetType("MongoDB.Bson.GuidRepresentation", true)!;
        var guidSerializer = bson.GetType("MongoDB.Bson.Serialization.Serializers.GuidSerializer", true)!;
        var serializer = Activator.CreateInstance(guidSerializer, Enum.Parse(representation, "Standard"))!;
        bson.GetType("MongoDB.Bson.Serialization.BsonSerializer", true)!.GetMethods()
            .Single(method => method.Name == "TryRegisterSerializer" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(Guid)).Invoke(null, [serializer]);
    }

    private static string GuidRepresentationOf(Assembly bson)
    {
        var serializer = bson.GetType("MongoDB.Bson.Serialization.BsonSerializer", true)!.GetMethods()
            .Single(method => method.Name == "LookupSerializer" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
            .MakeGenericMethod(typeof(Guid)).Invoke(null, null)!;
        return serializer.GetType().GetProperty("GuidRepresentation")!.GetValue(serializer)!.ToString()!;
    }

    private static Assembly Load(AssemblyLoadContext context, string name) => context.LoadFromAssemblyName(new AssemblyName(name));

    // MDM's own assemblies and the Mongo driver, loaded afresh (their statics apart from this process's); the rest shared.
    private sealed class IsolatedMdm(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name) =>
            name.Name is { } simple && (simple.StartsWith("Diten.MdmService.", StringComparison.Ordinal)
                                        || simple.StartsWith("MongoDB.", StringComparison.Ordinal))
            && File.Exists(Path.Combine(directory, simple + ".dll"))
                ? LoadFromAssemblyPath(Path.Combine(directory, simple + ".dll"))
                : null;
    }
}
