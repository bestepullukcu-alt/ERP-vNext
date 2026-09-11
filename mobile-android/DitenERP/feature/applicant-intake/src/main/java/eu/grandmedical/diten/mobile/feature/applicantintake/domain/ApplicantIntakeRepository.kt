package eu.grandmedical.diten.mobile.feature.applicantintake.domain

import eu.grandmedical.diten.mobile.core.common.UiResult
import kotlinx.coroutines.flow.Flow

/**
 * The feature's data contract. Offline-first: [observeList] and [observe] read
 * the local Room cache (the Single Source Of Truth) scoped to the current tenant
 * + selected legal entity; mutating calls reconcile with the backend.
 *
 * - [create] is optimistic — it writes a `PENDING` row locally and returns
 *   immediately (works with NO connectivity), then schedules a background push.
 * - [refresh], [evaluate] and [delete] are server-authoritative.
 */
interface ApplicantIntakeRepository {

    /** Observes the scoped list from the local cache, offline-first. */
    fun observeList(): Flow<List<ApplicantIntakeListItem>>

    /** Observes a single record by [id] from the local cache (null if absent). */
    fun observe(id: String): Flow<ApplicantIntakeReadiness?>

    /**
     * Optimistically creates [new]: writes a `PENDING` row to the cache and
     * schedules a sync. Returns the new local id on success (no server round-trip).
     */
    suspend fun create(new: NewApplicantIntake): UiResult<String>

    /** Asks the backend to (re)evaluate [id], then reconciles the result locally. */
    suspend fun evaluate(id: String): UiResult<ApplicantIntakeReadiness>

    /** Deletes [id] on the backend, then removes it from the cache. */
    suspend fun delete(id: String): UiResult<Unit>

    /** Pulls the server list for the current scope and upserts it as `SYNCED`. */
    suspend fun refresh(): UiResult<Unit>
}
