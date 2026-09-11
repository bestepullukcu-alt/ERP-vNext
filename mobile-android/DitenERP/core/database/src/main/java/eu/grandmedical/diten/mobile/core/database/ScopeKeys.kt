package eu.grandmedical.diten.mobile.core.database

import androidx.room.ColumnInfo

/**
 * The partition key every cached row carries.
 *
 * The Diten backend scopes all readable data by **tenant** and then by
 * **legal entity**, so the offline cache must reproduce that boundary exactly:
 * a row cached for one legal entity must never be observable while a different
 * legal entity is active, even inside the same tenant. Embedding [ScopeKeys] in
 * every entity (via `@Embedded`) guarantees the two columns exist on every table
 * and that DAO queries can filter on them uniformly.
 *
 * This mirrors the server design's two scoping modes:
 *  - **write scope** — a single `tenantId` + `legalEntityId` pair (strict isolation).
 *  - **roll-up scope** — one `tenantId` with a set of `legalEntityId`s, so a parent
 *    entity can read its own rows plus those of its descendants.
 *
 * Later feature modules reuse this type; do not add nullable columns here — a row
 * with an unknown scope is a data leak, not a valid cache entry.
 */
data class ScopeKeys(
    @ColumnInfo(name = "tenant_id")
    val tenantId: String,
    @ColumnInfo(name = "legal_entity_id")
    val legalEntityId: String,
)
