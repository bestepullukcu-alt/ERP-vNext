package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the candidate-pipeline cache. Every scoped read filters on `tenant_id`
 * then `legal_entity_id` so a row outside the caller's scope can never leak
 * (the `:core:database` isolation contract).
 */
@Dao
interface CandidatePipelineDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM candidate_pipeline
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<CandidatePipelineEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM candidate_pipeline
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<CandidatePipelineEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM candidate_pipeline WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<CandidatePipelineEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM candidate_pipeline WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): CandidatePipelineEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM candidate_pipeline WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<CandidatePipelineEntity>

    @Upsert
    suspend fun upsert(row: CandidatePipelineEntity)

    @Upsert
    suspend fun upsert(rows: List<CandidatePipelineEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE candidate_pipeline SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM candidate_pipeline WHERE id = :id")
    suspend fun delete(id: String)
}
