using Microsoft.Extensions.DependencyInjection;

namespace Diten.MdmService.Application.Common;

/// <summary>
/// WP-MDM-BL598-01 — <c>Lazy&lt;T&gt;</c> for any registered service (open-generic registration in AddApplication): the
/// container builds the service when <c>.Value</c> is first read, inside the caller's own failure handling — not while the
/// caller itself is being resolved. Used where a dependency's constructor may reach the store (repositories that build
/// their indexes in the constructor, BL-598 second round), so that a store outage is answered by the caller's coded 503
/// instead of an uncoded 500 from DI. The resolution itself stays the container's (no service locator in a handler).
/// </summary>
public sealed class DeferredService<T>(IServiceProvider services) : Lazy<T>(services.GetRequiredService<T>)
    where T : notnull;

/// <summary>
/// WP-MDM-BL598-01 — the ONLY failures of building a store-backed service that count as "the store cannot be reached":
/// a timeout (server selection) and the driver's connection / not-primary failures, recognised by type name (the
/// Application layer does not reference the driver). Anything else — a programming error, an unresolved tenant — is not
/// masked: it stays the 500 it is.
/// </summary>
public static class StoreConnectionFailure
{
    public static bool Is(Exception exception)
    {
        if (exception is TimeoutException) return true;
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
            if (type.FullName is "MongoDB.Driver.MongoConnectionException" or "MongoDB.Driver.MongoNotPrimaryException")
                return true;
        return false;
    }
}
