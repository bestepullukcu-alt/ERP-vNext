package eu.grandmedical.diten.mobile.feature.competencyskills

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.network.NetworkEnvelope
import eu.grandmedical.diten.mobile.feature.competencyskills.data.api.CompetencySkillsApi
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsReadinessDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.dto.CompetencySkillsReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.competencyskills.data.scope.CompetencySkillsScopeProvider
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsListItem
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsReadiness
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsRepository
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.NewCompetencySkills
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response

/** A fixed-scope provider so offline-first tests need no `:core:auth` stack. */
class FakeScopeProvider(private val scope: ScopeKeys?) : CompetencySkillsScopeProvider {
    override suspend fun currentScope(): ScopeKeys? = scope
    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(scope)
}

/**
 * Scriptable fake [CompetencySkillsApi]. Each endpoint delegates to a lambda so a
 * test can return success/error and assert call counts (proving, e.g., that an
 * offline `create` never touches the network).
 */
class FakeCompetencySkillsApi(
    var onList: () -> Response<NetworkEnvelope<List<CompetencySkillsReadinessListItemDto>>> =
        { ok(emptyList()) },
    var onGet: (String) -> Response<NetworkEnvelope<CompetencySkillsReadinessDto>> =
        { error(404) },
    var onCreate: (CompetencySkillsCreateRequestDto) -> Response<NetworkEnvelope<String>> =
        { ok("server-id") },
    var onEvaluate: (String) -> Response<NetworkEnvelope<CompetencySkillsReadinessDto>> =
        { error(404) },
    var onDelete: (String) -> Response<NetworkEnvelope<String>> = { ok("ok") },
) : CompetencySkillsApi {

    var createCount: Int = 0
        private set
    var listCount: Int = 0
        private set
    var deleteCount: Int = 0
        private set
    val createdRequests = mutableListOf<CompetencySkillsCreateRequestDto>()

    override suspend fun list(): Response<NetworkEnvelope<List<CompetencySkillsReadinessListItemDto>>> {
        listCount++
        return onList()
    }

    override suspend fun get(id: String): Response<NetworkEnvelope<CompetencySkillsReadinessDto>> = onGet(id)

    override suspend fun create(
        request: CompetencySkillsCreateRequestDto,
    ): Response<NetworkEnvelope<String>> {
        createCount++
        createdRequests += request
        return onCreate(request)
    }

    override suspend fun evaluate(id: String): Response<NetworkEnvelope<CompetencySkillsReadinessDto>> = onEvaluate(id)

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
class FakeCompetencySkillsRepository : CompetencySkillsRepository {
    val listFlow = MutableSharedFlow<List<CompetencySkillsListItem>>(replay = 0)
    val recordFlow = MutableStateFlow<CompetencySkillsReadiness?>(null)

    var refreshCount: Int = 0
        private set
    var refreshResult: UiResult<Unit> = UiResult.Success(Unit)
    var createResult: UiResult<String> = UiResult.Success("local-id")
    var evaluateResult: UiResult<CompetencySkillsReadiness> = UiResult.Error("not set")
    var deleteResult: UiResult<Unit> = UiResult.Success(Unit)
    var lastCreated: NewCompetencySkills? = null
        private set

    override fun observeList(): Flow<List<CompetencySkillsListItem>> = listFlow
    override fun observe(id: String): Flow<CompetencySkillsReadiness?> = recordFlow
    override suspend fun create(new: NewCompetencySkills): UiResult<String> {
        lastCreated = new
        return createResult
    }
    override suspend fun evaluate(id: String): UiResult<CompetencySkillsReadiness> = evaluateResult
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
    competencySkillsReadinessState: Int = 1,
): CompetencySkillsReadinessDto = CompetencySkillsReadinessDto(
    id = id,
    code = code,
    displayName = displayName,
    competencySkillsReadinessState = competencySkillsReadinessState,
)

/** Builds a list-item DTO for refresh tests. */
fun listItemDto(
    id: String,
    code: String = id,
    displayName: String = "Name $id",
    competencySkillsReadinessState: Int = 1,
): CompetencySkillsReadinessListItemDto = CompetencySkillsReadinessListItemDto(
    id = id,
    code = code,
    displayName = displayName,
    competencySkillsReadinessState = competencySkillsReadinessState,
)
