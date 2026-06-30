using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

/// <summary>
/// A1 — collects every distinct permission key declared via <see cref="HasPermissionAttribute"/> across all
/// controllers in an assembly (class-level and action-level). This is the source for startup auto-registration,
/// so a new module's <c>[HasPermission]</c> keys reach AuthService with no hand-edited seed.
/// </summary>
public static class HasPermissionReflector
{
    public static IReadOnlyList<string> CollectPermissionKeys(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var controllerType in GetControllerTypes(assembly))
        {
            // Class-level [HasPermission] (a whole controller gated by one key).
            foreach (var attr in controllerType.GetCustomAttributes<HasPermissionAttribute>(inherit: true))
            {
                Add(keys, attr.Permission);
            }

            // Action-level [HasPermission].
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                foreach (var attr in method.GetCustomAttributes<HasPermissionAttribute>(inherit: true))
                {
                    Add(keys, attr.Permission);
                }
            }
        }

        return keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<Type> GetControllerTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Be resilient to a single bad type — collect whatever loaded.
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types.Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(t));
    }

    private static void Add(HashSet<string> keys, string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            keys.Add(key.Trim());
        }
    }
}
