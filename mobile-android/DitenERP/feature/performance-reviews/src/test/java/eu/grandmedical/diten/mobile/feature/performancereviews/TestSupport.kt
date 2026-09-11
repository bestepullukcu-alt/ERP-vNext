package eu.grandmedical.diten.mobile.feature.performancereviews

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.performancereviews.data.api.PerformanceReviewsApi
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.scope.PerformanceReviewsScopeProvider
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.NewPerformanceReviews
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsListItem
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsReadiness
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : PerformanceReviewsScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [PerformanceReviewsApi]. Each endpoint delegates to a lambda so
 * a test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakePerformanceReviewsApi(
    var onList: () -> Response<NetworkEnvelope<List<PerformanceReviewsReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<PerformanceReviewsReadinessDto>> =
        { error(404) },
    var onCreate: (PerformanceReviewsCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<PerformanceReviewsReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : PerformanceReviewsApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    val createdRequests = mutableListOf<PerformanceReviewsCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<PerformanceReviewsReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<PerformanceReviewsReadinessDto>> = onGet(id)

    override suspend fun create(
        request: PerformanceReviewsCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<PerformanceReviewsReadinessDto>> = onEvaluate(id)

    override suspend fun delete(id: String): Response<NetworkEnvelope<String>> = onDelete(id)

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
class FakePerformanceReviewsRepository : PerformanceReviewsRepository {
    val listFlow = MutableSharedFlow<List<PerformanceReviewsListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<PerformanceReviewsReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<PerformanceReviewsReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewPerformanceReviews? = null
        private set

    override fun observeList(): Flow<List<PerformanceReviewsListItem>> = listFlow
    override fun observe(id: String): Flow<PerformanceReviewsReadiness?> = recordFlow
    override suspend fun create(new: NewPerformanceReviews): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<PerformanceReviewsReadiness> = evaluateResult
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
    performanceReviewReadinessState: Int = 0,
): PerformanceReviewsReadinessDto = PerformanceReviewsReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    performanceReviewReadinessState = performanceReviewReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    performanceReviewReadinessState: Int = 0,
): PerformanceReviewsReadinessListItemDto = PerformanceReviewsReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    performanceReviewReadinessState = performanceReviewReadinessState,
)
