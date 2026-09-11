package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import kotlinx.coroutines.flow.Flow

/**
 * DAO for the employee-onboarding cache. Every scoped read filters on `tenant_id`
 * then `legal_entity_id` so a row outside the caller's scope can never leak
 * (the `:core:database` isolation contract).
 */
@Dao
interface EmployeeOnboardingDao {

    /** Observes the scoped list, offline-first, ordered by code. */
    @Query(
        """
        SELECT * FROM employee_onboarding
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    fun observeInScope(tenantId: String, legalEntityId: String): Flow<List<EmployeeOnboardingEntity>>

    /** One-shot scoped read for non-reactive callers/tests. */
    @Query(
        """
        SELECT * FROM employee_onboarding
        WHERE tenant_id = :tenantId AND legal_entity_id = :legalEntityId
        ORDER BY code ASC
        """,
    )
    suspend fun getInScope(tenantId: String, legalEntityId: String): List<EmployeeOnboardingEntity>

    /** Observes a single row by id (null once deleted / not present). */
    @Query("SELECT * FROM employee_onboarding WHERE id = :id LIMIT 1")
    fun observe(id: String): Flow<EmployeeOnboardingEntity?>

    /** One-shot single-row read. */
    @Query("SELECT * FROM employee_onboarding WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): EmployeeOnboardingEntity?

    /** All rows awaiting a server push, across scopes (used by the sync handler). */
    @Query("SELECT * FROM employee_onboarding WHERE sync_status = :status ORDER BY code ASC")
    suspend fun getByStatus(status: SyncStatus = SyncStatus.PENDING): List<EmployeeOnboardingEntity>

    @Upsert
    suspend fun upsert(row: EmployeeOnboardingEntity)

    @Upsert
    suspend fun upsert(rows: List<EmployeeOnboardingEntity>)

    /** Marks a row's sync status (e.g. PENDING -> SYNCED after a successful push). */
    @Query("UPDATE employee_onboarding SET sync_status = :status WHERE id = :id")
    suspend fun markStatus(id: String, status: SyncStatus)

    /** Removes a single row by id. */
    @Query("DELETE FROM employee_onboarding WHERE id = :id")
    suspend fun delete(id: String)
}
