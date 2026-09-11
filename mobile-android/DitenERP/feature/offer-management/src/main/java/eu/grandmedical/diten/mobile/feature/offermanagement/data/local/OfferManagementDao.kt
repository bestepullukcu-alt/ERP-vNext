package eu.grandmedical.diten.mobile.feature.offermanagement.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the offer-management cache. Every scoped read filters on `tenant_id`
 * then `legal_entity_id` so a row outside the caller's scope can never leak
 * (the `:core:database` isolation contract).
 */
@Dao
interface OfferManagementDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM offer_management
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<OfferManagementEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM offer_management
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<OfferManagementEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM offer_management WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<OfferManagementEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM offer_management WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): OfferManagementEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM offer_management WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<OfferManagementEntity>

    @Upsert
    suspend fun upsert(row: OfferManagementEntity)

    @Upsert
    suspend fun upsert(rows: List<OfferManagementEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE offer_management SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM offer_management WHERE id = :id")
    suspend fun delete(id: String)
}
