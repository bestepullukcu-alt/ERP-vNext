using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class ModuleCatalogSeed
{
    // SELF-REG Faz 1: GOLDENSLIM is no longer hard-seeded here — DevEnablement self-registers it via its module
    // manifest (POST /api/internal/module-catalog/register-manifest at startup), which is the single source of truth.
    // Consequence (correct self-reg semantics): GOLDENSLIM appears in the catalog only when DevEnablement is running.
    // The collection is intentionally not seeded with any module here; kept for the startup call site + future seeds.
    public static Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default) => Task.CompletedTask;
}
