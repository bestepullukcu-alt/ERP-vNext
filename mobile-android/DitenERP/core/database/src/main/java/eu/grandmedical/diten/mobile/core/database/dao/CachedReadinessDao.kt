package eu.grandmedical.diten.mobile.core.database.dao

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.CachedReadinessEntity
import kotlinx.coroutines.flow.Flow

/**
 * Reference DAO for the tenant + legal-entity partitioned cache.
 *
 * Every read is filtered by `tenant_id` first, then by legal entity, so a query
 * can never return a row outside the caller's scope. Two scoping modes mirror
 * the backend:
 *  - **write scope** ([observeInScope] / [getInScope]) — exactly one legal entity.
 *  - **roll-up scope** ([observeInRollup] / [getInRollup]) — a set of legal
 *    entities, so a parent reads its own rows plus its descendants'.
 *
 * Feature modules should copy this shape rather than writing unscoped queries.
 */
@Dao
interface CachedReadinessDao {

    // --- Write-scope reads: strict single-legal-entity isolation. --------------

    /** Observes rows for exactly one (tenant, legal entity) pair, offline-first. */
    @Query(
        """
        SELECT * FROM cached_readiness
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<CachedReadinessEntity>>

    /** One-shot variant of [observeInScope] for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM cached_readiness
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<CachedReadinessEntity>

    // --- Roll-up reads: a parent sees a set of descendant legal entities. ------

    /** Observes rows for one tenant across a set of legal entities (roll-up). */
    @Query(
        """
        SELECT * FROM cached_readiness
        WHERE tenant_id = :tenantId AND legal_entity_id IN (:legalEntityIds)
        ORDER BY legal_entity_id ASC, code ASC
        """,
    )
    fun observeInRollup(
        tenantId: String,
        legalEntityIds: List<String>,
    ): Flow<List<CachedReadinessEntity>>

    /** One-shot variant of [observeInRollup]. */
    @Query(
        """
        SELECT * FROM cached_readiness
        WHERE tenant_id = :tenantId AND legal_entity_id IN (:legalEntityIds)
        ORDER BY legal_entity_id ASC, code ASC
        """,
    )
    suspend fun getInRollup(
        tenantId: String,
        legalEntityIds: List<String>,
    ): List<CachedReadinessEntity>

    // --- Writes. ---------------------------------------------------------------

    /** Inserts new rows or replaces existing ones by primary key. */
    @Upsert
    suspend fun upsert(rows: List<CachedReadinessEntity>)

    /** Convenience single-row upsert. */
    @Upsert
    suspend fun upsert(row: CachedReadinessEntity)

    /**
     * Deletes only the rows inside one (tenant, legal entity) scope. Used when a
     * scope is refreshed or the user leaves a legal entity; never touches other
     * scopes' cached data.
     */
    @Query(
        """
        DELETE FROM cached_readiness
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        """,
    )
    suspend fun deleteScope(tenantId: String, legalEntityId: String)
}
