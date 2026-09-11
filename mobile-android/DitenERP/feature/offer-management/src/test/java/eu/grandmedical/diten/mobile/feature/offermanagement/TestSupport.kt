package eu.grandmedical.diten.mobile.feature.offermanagement

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.feature.offermanagement.data.api.OfferManagementApi
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementCreateRequestDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.scope.OfferManagementScopeProvider
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.NewOfferManagement
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementListItem
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementReadiness
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : OfferManagementScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [OfferManagementApi]. Each endpoint delegates to a lambda so a
 * test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeOfferManagementApi(
    var onList: () -> Response<NetworkEnvelope<List<OfferManagementReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<OfferManagementReadinessDto>> =
        { error(404) },
    var onCreate: (OfferManagementCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<OfferManagementReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : OfferManagementApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    var deleteCount: Int = 0
        private set
    val createdRequests = mutableListOf<OfferManagementCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<OfferManagementReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<OfferManagementReadinessDto>> = onGet(id)

    override suspend fun create(
        request: OfferManagementCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<OfferManagementReadinessDto>> = onEvaluate(id)

    override suspend fun delete(id: String): Response<NetworkEnvelope<String>> {
        deleteCount++
        return onDelete(id)
    }

    companion object {
        fun <T> ok(data: T): Response<NetworkEnvelope<T>> =
            Response.success(NetworkEnvelope(data = data, statusCode = 200, isSuccessful = true))

        fun <T> error(code: Int): Response<NetworkEnvelope<T>> =
            Response.error(code, "{\"statusCode\":$code,\"isSuccessful\":false,\"errors\":[\"boom\"]}".toResponseBody(JSON))

        private val JSON = "application/json".toMediaType()
    }
}

/**
 * Fake repository for the ViewModel tests. [listFlow] drives the list screen
 * (emit to move Loading -> content); the mutating calls return scripted results
 * and record whether they were invoked.
 */
class FakeOfferManagementRepository : OfferManagementRepository {
    val listFlow = MutableSharedFlow<List<OfferManagementListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<OfferManagementReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<OfferManagementReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewOfferManagement? = null
        private set

    override fun observeList(): Flow<List<OfferManagementListItem>> = listFlow
    override fun observe(id: String): Flow<OfferManagementReadiness?> = recordFlow
    override suspend fun create(new: NewOfferManagement): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<OfferManagementReadiness> = evaluateResult
    override suspend fun delete(id: String): UiResult<Unit> = deleteResult
    override suspend fun refresh(): UiResult<Unit> {
        refreshCount++
        return refreshResult
    }
}

/** Builds a full readiness DTO with sensible defaults for tests. */
fun readinessDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    offerReadinessState: Int = 1,
): OfferManagementReadinessDto = OfferManagementReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    offerReadinessState = offerReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    offerReadinessState: Int = 1,
): OfferManagementReadinessListItemDto = OfferManagementReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    offerReadinessState = offerReadinessState,
)
