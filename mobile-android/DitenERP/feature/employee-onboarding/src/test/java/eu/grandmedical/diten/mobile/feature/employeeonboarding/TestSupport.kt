package eu.grandmedical.diten.mobile.feature.employeeonboarding

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.api.EmployeeOnboardingApi
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingReadinessDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.scope.EmployeeOnboardingScopeProvider
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingListItem
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingReadiness
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingRepository
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.NewEmployeeOnboarding
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : EmployeeOnboardingScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [EmployeeOnboardingApi]. Each endpoint delegates to a lambda so a
 * test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeEmployeeOnboardingApi(
    var onList: () -> Response<NetworkEnvelope<List<EmployeeOnboardingReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<EmployeeOnboardingReadinessDto>> =
        { error(404) },
    var onCreate: (EmployeeOnboardingCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<EmployeeOnboardingReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : EmployeeOnboardingApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    val createdRequests = mutableListOf<EmployeeOnboardingCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<EmployeeOnboardingReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<EmployeeOnboardingReadinessDto>> = onGet(id)

    override suspend fun create(
        request: EmployeeOnboardingCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<EmployeeOnboardingReadinessDto>> = onEvaluate(id)

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
class FakeEmployeeOnboardingRepository : EmployeeOnboardingRepository {
    val listFlow = MutableSharedFlow<List<EmployeeOnboardingListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<EmployeeOnboardingReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<EmployeeOnboardingReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewEmployeeOnboarding? = null
        private set

    override fun observeList(): Flow<List<EmployeeOnboardingListItem>> = listFlow
    override fun observe(id: String): Flow<EmployeeOnboardingReadiness?> = recordFlow
    override suspend fun create(new: NewEmployeeOnboarding): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<EmployeeOnboardingReadiness> = evaluateResult
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
    onboardingReadinessState: Int = 1, // Ready
): EmployeeOnboardingReadinessDto = EmployeeOnboardingReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    onboardingReadinessState = onboardingReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    onboardingReadinessState: Int = 1, // Ready
): EmployeeOnboardingReadinessListItemDto = EmployeeOnboardingReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    onboardingReadinessState = onboardingReadinessState,
)
