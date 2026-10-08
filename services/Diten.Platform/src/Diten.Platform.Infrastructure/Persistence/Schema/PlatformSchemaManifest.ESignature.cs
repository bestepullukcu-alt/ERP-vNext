using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0022 e-Signature Service (E2) — pack §3 / §4 indexes. The UNIQUE indexes carry the rules the code relies on:
    /// one signature number per tenant, one signature per (signer, idempotency key), one invalidation per signature, one
    /// meaning code per tenant, one action code per tenant, one tenant-default policy and one override per legal entity.
    /// Records are insert-only, so none of them filters on IsDeleted.
    /// </summary>
    // A property, not a field: the union (AllCollections) reads it inside its Lazy, and a static FIELD declared in a file the
    // compiler orders after that initializer is flagged "maybe null" (CS8604). The getter runs once, from that Lazy.
    private static SchemaCollection[] ESignatureCollections => new[]
    {
        Collection<ESignatureRecord>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureRecords,
            () => new CreateIndexModel<ESignatureRecord>[]
            {
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureNumber),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_records_tenant_number" }),
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignerUserId).Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_records_tenant_signer_idempotency" }),
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Module).Ascending(x => x.SubjectType)
                        .Ascending(x => x.SubjectId).Descending(x => x.SignedAtUtc),
                    new CreateIndexOptions { Name = "ix_esign_records_tenant_subject" }),
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Descending(x => x.SignedAtUtc),
                    new CreateIndexOptions { Name = "ix_esign_records_tenant_signed_at" }),
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignerUserId).Descending(x => x.SignedAtUtc),
                    new CreateIndexOptions { Name = "ix_esign_records_tenant_signer" }),
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequestId),
                    new CreateIndexOptions { Name = "ix_esign_records_tenant_request" }),
                // One signature binds one workflow task (E4). Partial on the key's existence: signatures without a task
                // context stay outside the unique space.
                new(Builders<ESignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending("Context.workflowTaskId"),
                    new CreateIndexOptions<ESignatureRecord>
                    {
                        Unique = true,
                        Name = "ux_esign_records_tenant_workflow_task",
                        PartialFilterExpression = Builders<ESignatureRecord>.Filter.Exists("Context.workflowTaskId")
                    })
            }),
        Collection<ESignatureInvalidation>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureInvalidations,
            () => new CreateIndexModel<ESignatureInvalidation>[]
            {
                new(Builders<ESignatureInvalidation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureId),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_invalidations_tenant_signature" })
            }),
        Collection<ESignatureRequest>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureRequests,
            () => new CreateIndexModel<ESignatureRequest>[]
            {
                new(Builders<ESignatureRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequestNumber),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_requests_tenant_number" }),
                new(Builders<ESignatureRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequestedBy).Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_requests_tenant_requester_idempotency" }),
                new(Builders<ESignatureRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignerUserId).Ascending(x => x.Status)
                        .Descending(x => x.RequestedAtUtc),
                    new CreateIndexOptions { Name = "ix_esign_requests_tenant_signer_status" }),
                new(Builders<ESignatureRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.DueAtUtc),
                    new CreateIndexOptions { Name = "ix_esign_requests_tenant_status_due" })
            }),
        Collection<ESignaturePolicy>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignaturePolicies,
            () => new CreateIndexModel<ESignaturePolicy>[]
            {
                // LegalEntityId is null on the tenant default, so (TenantId, Scope, LegalEntityId) is unique for both.
                new(Builders<ESignaturePolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Scope).Ascending(x => x.LegalEntityId),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_policies_tenant_scope_legal_entity" })
            }),
        Collection<ESignaturePolicyChange>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignaturePolicyChanges,
            () => new CreateIndexModel<ESignaturePolicyChange>[]
            {
                new(Builders<ESignaturePolicyChange>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Descending(x => x.ChangedAtUtc),
                    new CreateIndexOptions { Name = "ix_esign_policy_changes_tenant_scope_time" })
            }),
        Collection<ESignatureMeaning>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureMeanings,
            () => new CreateIndexModel<ESignatureMeaning>[]
            {
                new(Builders<ESignatureMeaning>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Code),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_meanings_tenant_code" })
            }),
        Collection<ESignableAction>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureSignableActions,
            () => new CreateIndexModel<ESignableAction>[]
            {
                // Global catalog (ESignableAction doc): one row per action code.
                new(Builders<ESignableAction>.IndexKeys.Ascending(x => x.ActionCode),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_actions_code" })
            }),
        Collection<ESignatureExport>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureExports,
            () => new CreateIndexModel<ESignatureExport>[]
            {
                new(Builders<ESignatureExport>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExportNumber),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_exports_tenant_number" }),
                new(Builders<ESignatureExport>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ByUserId).Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions { Unique = true, Name = "ux_esign_exports_tenant_by_idempotency" }),
                new(Builders<ESignatureExport>.IndexKeys.Ascending(x => x.TenantId).Descending(x => x.AtUtc),
                    new CreateIndexOptions { Name = "ix_esign_exports_tenant_time" })
            }),
        Collection<ESignatureCounter>(
            SchemaProfile.ESignature,
            PlatformCollections.ESignatureCounters,
            () => new CreateIndexModel<ESignatureCounter>[]
            {
                // _id is the (tenant, series, year) key; this index serves per-tenant inspection only.
                new(Builders<ESignatureCounter>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Series).Ascending(x => x.Year),
                    new CreateIndexOptions { Name = "ix_esign_counters_tenant_series_year" })
            })
    };
}
