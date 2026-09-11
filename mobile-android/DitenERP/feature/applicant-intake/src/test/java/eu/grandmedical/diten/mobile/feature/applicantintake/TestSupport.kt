package eu.grandmedical.diten.mobile.feature.applicantintake

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.feature.applicantintake.data.api.ApplicantIntakeApi
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeCreateRequestDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.scope.ApplicantIntakeScopeProvider
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeListItem
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeReadiness
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeRepository
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.NewApplicantIntake
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : ApplicantIntakeScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [ApplicantIntakeApi]. Each endpoint delegates to a lambda so a
 * test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeApplicantIntakeApi(
    var onList: () -> Response<NetworkEnvelope<List<ApplicantIntakeReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<ApplicantIntakeReadinessDto>> =
        { error(404) },
    var onCreate: (ApplicantIntakeCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<ApplicantIntakeReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : ApplicantIntakeApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    val createdRequests = mutableListOf<ApplicantIntakeCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<ApplicantIntakeReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<ApplicantIntakeReadinessDto>> = onGet(id)

    override suspend fun create(
        request: ApplicantIntakeCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<ApplicantIntakeReadinessDto>> = onEvaluate(id)

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
class FakeApplicantIntakeRepository : ApplicantIntakeRepository {
    val listFlow = MutableSharedFlow<List<ApplicantIntakeListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<ApplicantIntakeReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<ApplicantIntakeReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewApplicantIntake? = null
        private set

    override fun observeList(): Flow<List<ApplicantIntakeListItem>> = listFlow
    override fun observe(id: String): Flow<ApplicantIntakeReadiness?> = recordFlow
    override suspend fun create(new: NewApplicantIntake): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<ApplicantIntakeReadiness> = evaluateResult
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
    intakeState: Int = 2,
): ApplicantIntakeReadinessDto = ApplicantIntakeReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    intakeState = intakeState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    intakeState: Int = 2,
): ApplicantIntakeReadinessListItemDto = ApplicantIntakeReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    intakeState = intakeState,
)
