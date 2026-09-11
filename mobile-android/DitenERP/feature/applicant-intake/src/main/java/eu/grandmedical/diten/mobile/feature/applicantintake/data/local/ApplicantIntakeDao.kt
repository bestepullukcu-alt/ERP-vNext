package eu.grandmedical.diten.mobile.feature.applicantintake.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the applicant-intake cache. Every scoped read filters on `tenant_id`
 * then `legal_entity_id` so a row outside the caller's scope can never leak
 * (the `:core:database` isolation contract).
 */
@Dao
interface ApplicantIntakeDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM applicant_intake
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<ApplicantIntakeEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM applicant_intake
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<ApplicantIntakeEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM applicant_intake WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<ApplicantIntakeEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM applicant_intake WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): ApplicantIntakeEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM applicant_intake WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<ApplicantIntakeEntity>

    @Upsert
    suspend fun upsert(row: ApplicantIntakeEntity)

    @Upsert
    suspend fun upsert(rows: List<ApplicantIntakeEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE applicant_intake SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM applicant_intake WHERE id = :id")
    suspend fun delete(id: String)
}
