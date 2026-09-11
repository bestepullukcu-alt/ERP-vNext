package eu.grandmedical.diten.mobile.feature.candidatepipeline

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.api.CandidatePipelineApi
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineCreateRequestDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.scope.CandidatePipelineScopeProvider
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineListItem
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineReadiness
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineRepository
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.NewCandidatePipeline
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : CandidatePipelineScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [CandidatePipelineApi]. Each endpoint delegates to a lambda so a
 * test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeCandidatePipelineApi(
    var onList: () -> Response<NetworkEnvelope<List<CandidatePipelineReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<CandidatePipelineReadinessDto>> =
        { error(404) },
    var onCreate: (CandidatePipelineCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<CandidatePipelineReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : CandidatePipelineApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    var deleteCount: Int = 0
        private set
    val createdRequests = mutableListOf<CandidatePipelineCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<CandidatePipelineReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<CandidatePipelineReadinessDto>> = onGet(id)

    override suspend fun create(
        request: CandidatePipelineCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<CandidatePipelineReadinessDto>> = onEvaluate(id)

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
class FakeCandidatePipelineRepository : CandidatePipelineRepository {
    val listFlow = MutableSharedFlow<List<CandidatePipelineListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<CandidatePipelineReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<CandidatePipelineReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewCandidatePipeline? = null
        private set

    override fun observeList(): Flow<List<CandidatePipelineListItem>> = listFlow
    override fun observe(id: String): Flow<CandidatePipelineReadiness?> = recordFlow
    override suspend fun create(new: NewCandidatePipeline): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<CandidatePipelineReadiness> = evaluateResult
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
    pipelineReadinessState: Int = 1,
): CandidatePipelineReadinessDto = CandidatePipelineReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    pipelineReadinessState = pipelineReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    pipelineReadinessState: Int = 1,
): CandidatePipelineReadinessListItemDto = CandidatePipelineReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    pipelineReadinessState = pipelineReadinessState,
)
