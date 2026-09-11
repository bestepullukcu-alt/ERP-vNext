package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the compensation-benefits cache. Every scoped read filters on
 * `tenant_id` then `legal_entity_id` so a row outside the caller's scope can
 * never leak (the `:core:database` isolation contract).
 */
@Dao
interface CompensationBenefitsDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM compensation_benefits
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<CompensationBenefitsEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM compensation_benefits
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<CompensationBenefitsEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM compensation_benefits WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<CompensationBenefitsEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM compensation_benefits WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): CompensationBenefitsEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM compensation_benefits WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<CompensationBenefitsEntity>

    /**
     * All rows not yet server-synced — PENDING **or** FAILED — across scopes.
     * The sync handler retries this set so a row left FAILED by a transient
     * error (offline / expired-token window) is picked up on a later pass
     * instead of being stuck forever.
     */
    @Query("SELECT * FROM compensation_benefits WHERE sync_status != :synced ORDER BY code ASC")
    suspend fun getUnsynced(synced: SyncStatus = SyncStatus.SYNCED): List<CompensationBenefitsEntity>

    @Upsert
    suspend fun upsert(row: CompensationBenefitsEntity)

    @Upsert
    suspend fun upsert(rows: List<CompensationBenefitsEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE compensation_benefits SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM compensation_benefits WHERE id = :id")
    suspend fun delete(id: String)
}
