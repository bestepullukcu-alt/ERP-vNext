package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.sync

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.ApiResponse
import eu.grandmedical.diten.mobile.core.network.NetworkError
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncHandler
import eu.grandmedical.diten.mobile.core.sync.SyncResult
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.api.CandidatePipelineApi
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local.CandidatePipelineDao
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local.CandidatePipelineEntity
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.mapper.toCreateRequest
import javax.inject.Inject

/**
 * The feature's real [SyncHandler], contributed to the app-wide
 * `Set<SyncHandler>` multibinding (`@IntoSet`, see the feature DI module). This is
 * the concrete, functioning readiness sync handler that M0.6 deferred to M1.
 *
 * The offline-first loop:
 *  1. **pull pending** — read all `PENDING` rows from the DAO;
 *  2. **push** — POST each as a create request via [CandidatePipelineApi];
 *  3. **reconcile** — on accept, stamp the server id and mark `SYNCED`
 *     (delete the temporary local-id row, insert the server-id row); on a
 *     transient error, leave it `PENDING` and ask to retry; on a permanent
 *     (validation/permission) error, mark `FAILED` and report failure.
 */
class CandidatePipelineSyncHandler @Inject constructor(
    private val dao: CandidatePipelineDao,
    private val api: CandidatePipelineApi,
    private val safeApiCaller: SafeApiCaller,
) : SyncHandler {

    override val key: String = KEY

    override suspend fun sync(): SyncResult {
        val pending = dao.getByStatus(SyncStatus.PENDING)
        val failures = mutableListOf<String>()

        for (row in pending) {
            when (val outcome = push(row)) {
                Outcome.Transient ->
                    return SyncResult.Retry("connectivity/server pushing candidate-pipeline row ${row.id}")

                is Outcome.Permanent -> {
                    dao.markStatus(row.id, SyncStatus.FAILED)
                    failures += "${row.id}: ${outcome.reason}"
                }

                is Outcome.Accepted -> reconcileAccepted(row, outcome.serverId)
            }
        }

        return if (failures.isEmpty()) SyncResult.Success else SyncResult.Failure(failures.joinToString())
    }

    private suspend fun push(row: CandidatePipelineEntity): Outcome =
        when (val response = safeApiCaller.apiCall { api.create(row.toCreateRequest()) }) {
            is ApiResponse.Success -> Outcome.Accepted(response.data)
            is ApiResponse.Failure -> response.error.toOutcome()
        }

    /** Replaces the temporary local-id row with the server-authoritative id, marked SYNCED. */
    private suspend fun reconcileAccepted(row: CandidatePipelineEntity, serverId: String) {
        if (serverId.isNotBlank() && serverId != row.id) {
            dao.delete(row.id)
            dao.upsert(row.copy(id = serverId, syncStatus = SyncStatus.SYNCED))
        } else {
            dao.markStatus(row.id, SyncStatus.SYNCED)
        }
    }

    private fun NetworkError.toOutcome(): Outcome =
        when (this) {
            // Transient: retrying with the same input can succeed later.
            NetworkError.Connectivity, is NetworkError.Server -> Outcome.Transient
            // Permanent: the same input will keep being rejected.
            else -> Outcome.Permanent(message)
        }

    private sealed interface Outcome {
        data class Accepted(val serverId: String) : Outcome
        data object Transient : Outcome
        data class Permanent(val reason: String) : Outcome
    }

    companion object {
        /** Distinct from `:core:sync`'s reference "readiness" handler key. */
        const val KEY = "candidate-pipeline"
    }
}
