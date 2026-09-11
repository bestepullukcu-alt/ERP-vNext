package eu.grandmedical.diten.mobile.feature.learningtraining.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the learning-training cache. Every scoped read filters on `tenant_id`
 * then `legal_entity_id` so a row outside the caller's scope can never leak
 * (the `:core:database` isolation contract).
 */
@Dao
interface LearningTrainingDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM learning_training
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<LearningTrainingEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM learning_training
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<LearningTrainingEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM learning_training WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<LearningTrainingEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM learning_training WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): LearningTrainingEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM learning_training WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<LearningTrainingEntity>

    /**
     * All rows not yet server-synced — PENDING **or** FAILED — across scopes.
     * The sync handler retries this set so a row left FAILED by a transient
     * error (offline / expired-token window) is picked up on a later pass
     * instead of being stuck forever.
     */
    @Query("SELECT * FROM learning_training WHERE sync_status != :synced ORDER BY code ASC")
    suspend fun getUnsynced(synced: SyncStatus = SyncStatus.SYNCED): List<LearningTrainingEntity>

    @Upsert
    suspend fun upsert(row: LearningTrainingEntity)

    @Upsert
    suspend fun upsert(rows: List<LearningTrainingEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE learning_training SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM learning_training WHERE id = :id")
    suspend fun delete(id: String)
}
