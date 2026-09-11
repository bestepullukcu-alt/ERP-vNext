package eu.grandmedical.diten.mobile.feature.employeeonboarding.data

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.ApiResponse
import eu.grandmedical.diten.mobile.core.network.NetworkError
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncScheduler
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.api.EmployeeOnboardingApi
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local.EmployeeOnboardingDao
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.mapper.toDomain
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.mapper.toEntity
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.mapper.toListItem
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.scope.EmployeeOnboardingScopeProvider
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingListItem
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingReadiness
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingRepository
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.NewEmployeeOnboarding
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emitAll
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.map
import java.util.UUID
import javax.inject.Inject

/**
 * Offline-first repository. Room is the Single Source Of Truth:
 *  - [observeList] / [observe] read the local cache, scoped to the current
 *    tenant + selected legal entity;
 *  - [create] writes a `PENDING` row immediately (optimistic, no network) and
 *    schedules a background push via [SyncScheduler];
 *  - [refresh] / [evaluate] / [delete] reconcile with the server-authoritative
 *    backend, then upsert/remove the cache.
 */
class EmployeeOnboardingRepositoryImpl @Inject constructor(
    private val dao: EmployeeOnboardingDao,
    private val api: EmployeeOnboardingApi,
    private val safeApiCaller: SafeApiCaller,
    private val scopeProvider: EmployeeOnboardingScopeProvider,
    private val syncScheduler: SyncScheduler,
) : EmployeeOnboardingRepository {

    override fun observeList(): Flow<List<EmployeeOnboardingListItem>> = flow {
        val scope = scopeProvider.currentScope()
        if (scope == null) {
            emitAll(flowOf(emptyList()))
            return@flow
        }
        emitAll(
            dao.observeInScope(scope.tenantId, scope.legalEntityId)
                .map { rows -> rows.map { it.toListItem() } },
        )
    }

    override fun observe(id: String): Flow<EmployeeOnboardingReadiness?> =
        dao.observe(id).map { it?.toDomain() }

    override suspend fun create(new: NewEmployeeOnboarding): UiResult<String> {
        val scope = scopeProvider.currentScope()
            ?: return UiResult.Error("No active legal entity selected.")
        val localId = UUID.randomUUID().toString()
        // Optimistic local write — succeeds with NO server round-trip.
        dao.upsert(new.toEntity(id = localId, scope = scope, syncStatus = SyncStatus.PENDING))
        // Ask the sync framework to push it when connectivity allows.
        syncScheduler.enqueueOneTimeSync()
        return UiResult.Success(localId)
    }

    override suspend fun evaluate(id: String): UiResult<EmployeeOnboardingReadiness> {
        val scope = scopeProvider.currentScope()
            ?: return UiResult.Error("No active legal entity selected.")
        return when (val response = safeApiCaller.apiCall { api.evaluate(id) }) {
            is ApiResponse.Success -> {
                val dto = response.data
                dao.upsert(dto.toEntity(scope))
                UiResult.Success(dto.toDomain())
            }

            is ApiResponse.Failure -> UiResult.Error(response.error.message)
        }
    }

    override suspend fun delete(id: String): UiResult<Unit> {
        val row = dao.getById(id)
        // Never-synced rows (PENDING/FAILED) carry a LOCAL id the backend has
        // never seen — deleting them server-side would 404. Remove locally with
        // no round-trip so the UI can drop them.
        if (row != null && row.syncStatus != SyncStatus.SYNCED) {
            dao.delete(id)
            return UiResult.Success(Unit)
        }
        return when (val response = safeApiCaller.apiCall { api.delete(id) }) {
            is ApiResponse.Success -> {
                dao.delete(id)
                UiResult.Success(Unit)
            }
            // A 404 means the server row is already gone — reconcile by removing
            // the local row and reporting success. Genuine errors still surface.
            is ApiResponse.Failure ->
                if (response.error is NetworkError.NotFound) {
                    dao.delete(id)
                    UiResult.Success(Unit)
                } else {
                    UiResult.Error(response.error.message)
                }
        }
    }

    override suspend fun refresh(): UiResult<Unit> {
        val scope = scopeProvider.currentScope()
            ?: return UiResult.Error("No active legal entity selected.")
        return when (val response = safeApiCaller.apiCall { api.list() }) {
            is ApiResponse.Success -> {
                // Server-authoritative: upsert every server row as SYNCED. Local
                // PENDING creates not yet on the server are left for the sync pass.
                dao.upsert(response.data.map { it.toEntity(scope) })
                UiResult.Success(Unit)
            }

            is ApiResponse.Failure -> UiResult.Error(response.error.message)
        }
    }
}
