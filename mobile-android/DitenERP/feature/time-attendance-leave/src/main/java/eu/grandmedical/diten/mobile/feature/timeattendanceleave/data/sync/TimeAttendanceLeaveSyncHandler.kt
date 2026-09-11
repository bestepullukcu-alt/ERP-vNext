package eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.sync

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.ApiResponse
import eu.grandmedical.diten.mobile.core.network.NetworkError
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncHandler
import eu.grandmedical.diten.mobile.core.sync.SyncResult
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.api.TimeAttendanceLeaveApi
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.local.TimeAttendanceLeaveDao
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.local.TimeAttendanceLeaveEntity
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.mapper.toCreateRequest
import javax.inject.Inject

/**
 * The feature's real [SyncHandler], contributed to the app-wide
 * `Set<SyncHandler>` multibinding (`@IntoSet`, see the feature DI module). This is
 * the concrete, functioning readiness sync handler that M0.6 deferred to M1.
 *
 * The offline-first loop:
 *  1. **pull pending** — read all `PENDING` rows from the DAO;
 *  2. **push** — POST each as a create request via [TimeAttendanceLeaveApi];
 *  3. **reconcile** — on accept, stamp the server id and mark `SYNCED`
 *     (delete the temporary local-id row, insert the server-id row); on a
 *     transient error, leave it `PENDING` and ask to retry; on a permanent
 *     (validation/permission) error, mark `FAILED` and report failure.
 */
class TimeAttendanceLeaveSyncHandler @Inject constructor(
    private val dao: TimeAttendanceLeaveDao,
    private val api: TimeAttendanceLeaveApi,
    private val safeApiCaller: SafeApiCaller,
) : SyncHandler {

    override val key: String = KEY

    override suspend fun sync(): SyncResult {
        // PENDING **or** FAILED: a row left FAILED by an earlier transient error
        // (offline / expired-token window) must be retried, not stuck forever.
        val unsynced = dao.getUnsynced()
        val failures = mutableListOf<String>()

        for (row in unsynced) {
            when (val outcome = push(row)) {
                Outcome.Transient ->
                    return SyncResult.Retry("connectivity/server pushing time-attendance-leave row ${row.id}")

                is Outcome.Permanent -> {
                    dao.markStatus(row.id, SyncStatus.FAILED)
                    failures += "${row.id}: ${outcome.reason}"
                }

                is Outcome.Accepted -> reconcileAccepted(row, outcome.serverId)
            }
        }

        return if (failures.isEmpty()) SyncResult.Success else SyncResult.Failure(failures.joinToString())
    }

    private suspend fun push(row: TimeAttendanceLeaveEntity): Outcome =
        when (val response = safeApiCaller.apiCall { api.create(row.toCreateRequest()) }) {
            is ApiResponse.Success -> Outcome.Accepted(response.data)
            is ApiResponse.Failure -> response.error.toOutcome()
        }

    /** Replaces the temporary local-id row with the server-authoritative id, marked SYNCED. */
    private suspend fun reconcileAccepted(row: TimeAttendanceLeaveEntity, serverId: String) {
        if (serverId.isNotBlank() && serverId != row.id) {
            dao.delete(row.id)
            dao.upsert(row.copy(id = serverId, syncStatus = SyncStatus.SYNCED))
        } else {
            dao.markStatus(row.id, SyncStatus.SYNCED)
        }
    }

    private fun NetworkError.toOutcome(): Outcome =
        when (this) {
            // Transient: retrying can succeed later. Unauthorized during a
            // background push means "token expired, retry after refresh" (not a
            // permanent rejection); Unknown is treated as a retryable blip.
            NetworkError.Connectivity,
            is NetworkError.Server,
            NetworkError.Unauthorized,
            NetworkError.Unknown,
            -> Outcome.Transient
            // Permanent: Validation / Forbidden / NotFound will keep being rejected.
            else -> Outcome.Permanent(message)
        }

    private sealed interface Outcome {
        data class Accepted(val serverId: String) : Outcome
        data object Transient : Outcome
        data class Permanent(val reason: String) : Outcome
    }

    companion object {
        /** Distinct from `:core:sync`'s reference "readiness" handler key. */
        const val KEY = "time-attendance-leave"
    }
}
