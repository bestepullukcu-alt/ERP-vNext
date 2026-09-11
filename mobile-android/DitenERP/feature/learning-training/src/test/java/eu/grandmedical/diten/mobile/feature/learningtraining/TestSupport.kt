package eu.grandmedical.diten.mobile.feature.learningtraining

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.learningtraining.data.api.LearningTrainingApi
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.scope.LearningTrainingScopeProvider
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingListItem
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingReadiness
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingRepository
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.NewLearningTraining
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : LearningTrainingScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [LearningTrainingApi]. Each endpoint delegates to a lambda so a
 * test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeLearningTrainingApi(
    var onList: () -> Response<NetworkEnvelope<List<LearningTrainingReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<LearningTrainingReadinessDto>> =
        { error(404) },
    var onCreate: (LearningTrainingCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<LearningTrainingReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : LearningTrainingApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    var deleteCount: Int = 0
        private set
    val createdRequests = mutableListOf<LearningTrainingCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<LearningTrainingReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<LearningTrainingReadinessDto>> = onGet(id)

    override suspend fun create(
        request: LearningTrainingCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<LearningTrainingReadinessDto>> = onEvaluate(id)

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
class FakeLearningTrainingRepository : LearningTrainingRepository {
    val listFlow = MutableSharedFlow<List<LearningTrainingListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<LearningTrainingReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<LearningTrainingReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewLearningTraining? = null
        private set

    override fun observeList(): Flow<List<LearningTrainingListItem>> = listFlow
    override fun observe(id: String): Flow<LearningTrainingReadiness?> = recordFlow
    override suspend fun create(new: NewLearningTraining): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<LearningTrainingReadiness> = evaluateResult
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
    learningTrainingReadinessState: Int = 1,
): LearningTrainingReadinessDto = LearningTrainingReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    learningTrainingReadinessState = learningTrainingReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    learningTrainingReadinessState: Int = 1,
): LearningTrainingReadinessListItemDto = LearningTrainingReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    learningTrainingReadinessState = learningTrainingReadinessState,
)
