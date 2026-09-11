package eu.grandmedical.diten.mobile.feature.offermanagement.data

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.core.network.ApiResponse
import eu.grandmedical.diten.mobile.core.network.NetworkError
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.sync.SyncScheduler
import eu.grandmedical.diten.mobile.feature.offermanagement.data.api.OfferManagementApi
import eu.grandmedical.diten.mobile.feature.offermanagement.data.local.OfferManagementDao
import eu.grandmedical.diten.mobile.feature.offermanagement.data.mapper.toDomain
import eu.grandmedical.diten.mobile.feature.offermanagement.data.mapper.toEntity
import eu.grandmedical.diten.mobile.feature.offermanagement.data.mapper.toListItem
import eu.grandmedical.diten.mobile.feature.offermanagement.data.scope.OfferManagementScopeProvider
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.NewOfferManagement
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementListItem
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementReadiness
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emitAll
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.map
import java.io.IOException
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
class OfferManagementRepositoryImpl @Inject constructor(
    private val dao: OfferManagementDao,
    private val api: OfferManagementApi,
    private val safeApiCaller: SafeApiCaller,
    private val scopeProvider: OfferManagementScopeProvider,
    private val syncScheduler: SyncScheduler,
) : OfferManagementRepository {

    override fun observeList(): Flow<List<OfferManagementListItem>> = flow {
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

    override fun observe(id: String): Flow<OfferManagementReadiness?> =
        dao.observe(id).map { it?.toDomain() }

    override suspend fun create(new: NewOfferManagement): UiResult<String> {
        val scope = scopeProvider.currentScope()
            ?: return UiResult.Error("No active legal entity selected.")
        val localId = UUID.randomUUID().toString()
        // Optimistic local write — succeeds with NO server round-trip.
        dao.upsert(new.toEntity(id = localId, scope = scope, syncStatus = SyncStatus.PENDING))
        // Ask the sync framework to push it when connectivity allows.
        syncScheduler.enqueueOneTimeSync()
        return UiResult.Success(localId)
    }

    override suspend fun evaluate(id: String): UiResult<OfferManagementReadiness> {
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

    @Suppress("SwallowedException") // IO failures are translated to a typed UiResult.Error.
    override suspend fun delete(id: String): UiResult<Unit> =
        try {
            val response = api.delete(id)
            val envelope = response.body()
            if (response.isSuccessful && (envelope == null || envelope.isSuccessful)) {
                dao.delete(id)
                UiResult.Success(Unit)
            } else {
                UiResult.Error(envelope?.errors?.firstOrNull() ?: NetworkError.Unknown.message)
            }
        } catch (io: IOException) {
            UiResult.Error(NetworkError.Connectivity.message)
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
