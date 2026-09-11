package eu.grandmedical.diten.mobile.core.database.entity

import androidx.room.ColumnInfo
import androidx.room.Embedded
import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import eu.grandmedical.diten.mobile.core.database.ScopeKeys

/**
 * A generic "readiness" cache row and the reference template every future
 * feature cache entity follows.
 *
 * The important part is the embedded [ScopeKeys]: `tenant_id` and
 * `legal_entity_id` become real columns on this table, and the composite index
 * on them keeps both the write-scope (`tenant + entity`) and roll-up
 * (`tenant + entity IN (...)`) queries fast while making the partition boundary
 * explicit in the schema.
 *
 * [tags] shows a `List<String>` column round-tripped through the type converters,
 * and ([updatedAt], [syncStatus]) are the offline-first sync metadata.
 */
@Entity(
    tableName = "cached_readiness",
    indices = [
        Index(value = ["tenant_id", "legal_entity_id"]),
    ],
)
data class CachedReadinessEntity(
    @PrimaryKey
    @ColumnInfo(name = "id")
    val id: String,
    @Embedded
    val scope: ScopeKeys,
    @ColumnInfo(name = "code")
    val code: String,
    @ColumnInfo(name = "display_name")
    val displayName: String,
    @ColumnInfo(name = "state")
    val state: String,
    @ColumnInfo(name = "tags")
    val tags: List<String> = emptyList(),
    @ColumnInfo(name = "updated_at")
    val updatedAt: Long,
    @ColumnInfo(name = "sync_status")
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)
