using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

// MOD-0027-FU03A (Bridge) — startup seed loader for PlatformSeed/SystemSeed notification events. Runs AFTER
// NotificationTemplateSeed (so referenced templates already exist). Idempotent upsert delegating the decision to the
// source-agnostic NotificationEventSeedPlanner (clobber guard: never overwrites a Manifest-owned record; reconciles
// HARD, preserves SOFT). It is kept OUTSIDE INotificationEventManifestSyncService and uses NO IModuleManifestProvider.
//
// LAYER RULE (§5.1): this seed layer does NOT reference Platform.API and performs NO RBAC reflection. Permission-gated
// seeds (RequiredPermissionKey set) are written Draft; an API-side pass validates the literal and promotes to Active.
//
// The bridge ships NotificationEventSeedCatalog.PlatformSeedDefinitions EMPTY — this loop is a no-op until FU04A adds
// the 3 tenant events. No tenant event content lives here.
public static class NotificationEventSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var definitions = NotificationEventSeedCatalog.PlatformSeedDefinitions;
        if (definitions.Count == 0)
        {
            return; // bridge: no seed content yet (FU04A scope).
        }

        var events = database.GetCollection<NotificationEventDefinition>(PlatformCollections.NotificationEventDefinitions);
        var templates = database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in definitions)
        {
            var eventCode = (def.EventCode ?? string.Empty).Trim().ToLowerInvariant();
            if (!seen.Add(eventCode))
            {
                continue;
            }

            var templateKey = NotificationParsing.NormalizeTemplateKey(def.DefaultTemplateKey);
            var templateExists = await templates
                .Find(t => !t.IsDeleted && t.Status == NotificationTemplateStatus.Active && t.TemplateKey == templateKey)
                .AnyAsync(ct);

            var validation = NotificationEventSeedPlanner.Validate(def, templateExists);
            var existing = await events
                .Find(e => !e.IsDeleted && e.EventCode == eventCode)
                .FirstOrDefaultAsync(ct);

            var plan = NotificationEventSeedPlanner.Plan(existing, def, validation.EffectiveStatus);
            switch (plan.Action)
            {
                case NotificationEventSeedAction.Create:
                    await events.InsertOneAsync(plan.Entity!, cancellationToken: ct);
                    break;
                case NotificationEventSeedAction.Update:
                    plan.Entity!.UpdatedAt = DateTimeOffset.UtcNow;
                    await events.ReplaceOneAsync(
                        Builders<NotificationEventDefinition>.Filter.Eq(x => x.Id, plan.Entity!.Id),
                        plan.Entity!,
                        cancellationToken: ct);
                    break;
                case NotificationEventSeedAction.Skip:
                    // Clobber guard: Manifest-owned record; leave untouched.
                    break;
            }
        }
    }
}
