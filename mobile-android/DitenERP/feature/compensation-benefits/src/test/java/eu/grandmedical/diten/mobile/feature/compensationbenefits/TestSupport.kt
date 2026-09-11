package eu.grandmedical.diten.mobile.feature.compensationbenefits

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.api.CompensationBenefitsApi
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.scope.CompensationBenefitsScopeProvider
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsListItem
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsReadiness
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsRepository
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.NewCompensationBenefits
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : CompensationBenefitsScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [CompensationBenefitsApi]. Each endpoint delegates to a lambda
 * so a test can return success/error and assert call counts (proving, e.g., that
 * an offline `create` never touches the network).
 */
class FakeCompensationBenefitsApi(
    var onList: () -> Response<NetworkEnvelope<List<CompensationBenefitsReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<CompensationBenefitsReadinessDto>> =
        { error(404) },
    var onCreate: (CompensationBenefitsCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<CompensationBenefitsReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : CompensationBenefitsApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    var deleteCount: Int = 0
        private set
    val createdRequests = mutableListOf<CompensationBenefitsCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<CompensationBenefitsReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<CompensationBenefitsReadinessDto>> = onGet(id)

    override suspend fun create(
        request: CompensationBenefitsCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<CompensationBenefitsReadinessDto>> =
        onEvaluate(id)

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
class FakeCompensationBenefitsRepository : CompensationBenefitsRepository {
    val listFlow = MutableSharedFlow<List<CompensationBenefitsListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<CompensationBenefitsReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<CompensationBenefitsReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewCompensationBenefits? = null
        private set

    override fun observeList(): Flow<List<CompensationBenefitsListItem>> = listFlow
    override fun observe(id: String): Flow<CompensationBenefitsReadiness?> = recordFlow
    override suspend fun create(new: NewCompensationBenefits): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<CompensationBenefitsReadiness> = evaluateResult
    override suspend fun delete(id: String): UiResult<Unit> = deleteResult
    override suspend fun refresh(): UiResult<Unit> {
        refreshCount++
        return refreshResult
    }
}

/**
 * Builds a full readiness DTO with sensible defaults for tests. The default main
 * state 1 is Ready in THIS module's encoding (Ready=1, Deferred=2).
 */
fun readinessDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    compensationBenefitsReadinessState: Int = 1,
): CompensationBenefitsReadinessDto = CompensationBenefitsReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    compensationBenefitsReadinessState = compensationBenefitsReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    compensationBenefitsReadinessState: Int = 1,
): CompensationBenefitsReadinessListItemDto = CompensationBenefitsReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    compensationBenefitsReadinessState = compensationBenefitsReadinessState,
)
