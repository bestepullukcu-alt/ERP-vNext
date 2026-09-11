package eu.grandmedical.diten.mobile.feature.performancereviews.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the performance-reviews cache. Every scoped read filters on `tenant_id`
 * then `legal_entity_id` so a row outside the caller's scope can never leak
 * (the `:core:database` isolation contract).
 */
@Dao
interface PerformanceReviewsDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM performance_reviews
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<PerformanceReviewsEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM performance_reviews
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<PerformanceReviewsEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM performance_reviews WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<PerformanceReviewsEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM performance_reviews WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): PerformanceReviewsEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM performance_reviews WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<PerformanceReviewsEntity>

    @Upsert
    suspend fun upsert(row: PerformanceReviewsEntity)

    @Upsert
    suspend fun upsert(rows: List<PerformanceReviewsEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE performance_reviews SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM performance_reviews WHERE id = :id")
    suspend fun delete(id: String)
}
