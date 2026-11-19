using Diten.Platform.Domain.Entities.RecordCodes;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0040-FU02 §4 — the shared record-code service (RCS-001). Every index is tenant-first. The counter is unique per
    /// (tenant, prefix, type); the ledger is unique per (tenant, caller, idempotency key) — the idempotency guarantee — and
    /// per (tenant, code) — the second defence against a code issued twice; the group-prefix setting is one row per tenant.
    /// The two LegalEntity lookups serve the interim short-code usage lock (§8.13).
    /// </summary>
    private static readonly SchemaCollection[] RecordCodesCollections =
    {
        Collection<RecordCodeCounter>(
            SchemaProfile.RecordCodes,
            PlatformCollections.RecordCodeCounters,
            () => new[]
            {
                new CreateIndexModel<RecordCodeCounter>(
                    Builders<RecordCodeCounter>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Prefix).Ascending(x => x.RecordType),
                    new CreateIndexOptions { Name = "ux_record_code_counters_tenant_prefix_type", Unique = true })
            }),
        Collection<RecordCodeAllocation>(
            SchemaProfile.RecordCodes,
            PlatformCollections.RecordCodeAllocations,
            () => new[]
            {
                new CreateIndexModel<RecordCodeAllocation>(
                    Builders<RecordCodeAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CallerService).Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Name = "ux_record_code_allocations_tenant_caller_key", Unique = true }),
                new CreateIndexModel<RecordCodeAllocation>(
                    Builders<RecordCodeAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Code),
                    new CreateIndexOptions { Name = "ux_record_code_allocations_tenant_code", Unique = true }),
                new CreateIndexModel<RecordCodeAllocation>(
                    Builders<RecordCodeAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RecordType).Ascending(x => x.Prefix).Descending(x => x.Sequence),
                    new CreateIndexOptions { Name = "ix_record_code_allocations_tenant_type_prefix_seq" }),
                new CreateIndexModel<RecordCodeAllocation>(
                    Builders<RecordCodeAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Descending(x => x.AllocatedAtUtc),
                    new CreateIndexOptions { Name = "ix_record_code_allocations_tenant_le_time" }),
                new CreateIndexModel<RecordCodeAllocation>(
                    Builders<RecordCodeAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Prefix).Ascending(x => x.LegalEntityId),
                    new CreateIndexOptions { Name = "ix_record_code_allocations_tenant_prefix_le" })
            }),
        Collection<RecordCodeGroupPrefix>(
            SchemaProfile.RecordCodes,
            PlatformCollections.RecordCodeGroupPrefixes,
            () => new[]
            {
                new CreateIndexModel<RecordCodeGroupPrefix>(
                    Builders<RecordCodeGroupPrefix>.IndexKeys.Ascending(x => x.TenantId),
                    new CreateIndexOptions { Name = "ux_record_code_group_prefixes_tenant", Unique = true })
            })
    };
}
