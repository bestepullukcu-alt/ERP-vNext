package eu.grandmedical.diten.mobile.feature.timeattendanceleave

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.api.TimeAttendanceLeaveApi
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveCreateRequestDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.scope.TimeAttendanceLeaveScopeProvider
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.NewTimeAttendanceLeave
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveListItem
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveReadiness
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : TimeAttendanceLeaveScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [TimeAttendanceLeaveApi]. Each endpoint delegates to a lambda so
 * a test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeTimeAttendanceLeaveApi(
    var onList: () -> Response<NetworkEnvelope<List<TimeAttendanceLeaveReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<TimeAttendanceLeaveReadinessDto>> =
        { error(404) },
    var onCreate: (TimeAttendanceLeaveCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<TimeAttendanceLeaveReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : TimeAttendanceLeaveApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    var deleteCount: Int = 0
        private set
    val createdRequests = mutableListOf<TimeAttendanceLeaveCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<TimeAttendanceLeaveReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<TimeAttendanceLeaveReadinessDto>> = onGet(id)

    override suspend fun create(
        request: TimeAttendanceLeaveCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<TimeAttendanceLeaveReadinessDto>> = onEvaluate(id)

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
class FakeTimeAttendanceLeaveRepository : TimeAttendanceLeaveRepository {
    val listFlow = MutableSharedFlow<List<TimeAttendanceLeaveListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<TimeAttendanceLeaveReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<TimeAttendanceLeaveReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewTimeAttendanceLeave? = null
        private set

    override fun observeList(): Flow<List<TimeAttendanceLeaveListItem>> = listFlow
    override fun observe(id: String): Flow<TimeAttendanceLeaveReadiness?> = recordFlow
    override suspend fun create(new: NewTimeAttendanceLeave): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<TimeAttendanceLeaveReadiness> = evaluateResult
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
    readinessState: Int = 1,
): TimeAttendanceLeaveReadinessDto = TimeAttendanceLeaveReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    timeAttendanceLeaveReadinessState = readinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    readinessState: Int = 1,
): TimeAttendanceLeaveReadinessListItemDto = TimeAttendanceLeaveReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    timeAttendanceLeaveReadinessState = readinessState,
)
