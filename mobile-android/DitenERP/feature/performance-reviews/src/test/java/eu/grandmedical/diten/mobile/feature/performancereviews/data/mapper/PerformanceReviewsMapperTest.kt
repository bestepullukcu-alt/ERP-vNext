package eu.grandmedical.diten.mobile.feature.performancereviews.data.mapper

import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.performancereviews.data.dto.PerformanceReviewsReadinessDto
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.NewPerformanceReviews
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). The performance-reviews
 * enum codes DIFFER from the applicant-intake pilot's (here Ready=1, Deferred=2).
 * Pure JVM — no emulator.
 */
class PerformanceReviewsMapperTest {

    private val json = Json { encodeDefaults = true }

    @Test
    fun everyStateCode_roundTripsThroughFromCode() {
        // All 6 states map by their fixed backend codes (measured from HCM).
        assertEquals(ReadinessState.Draft, ReadinessState.fromCode(0))
        assertEquals(ReadinessState.Ready, ReadinessState.fromCode(1))
        assertEquals(ReadinessState.Deferred, ReadinessState.fromCode(2))
        assertEquals(ReadinessState.Blocked, ReadinessState.fromCode(3))
        assertEquals(ReadinessState.NotRequired, ReadinessState.fromCode(4))
        assertEquals(ReadinessState.Archived, ReadinessState.fromCode(5))
        ReadinessState.entries.forEach { assertEquals(it, ReadinessState.fromCode(it.code)) }
    }

    @Test
    fun unknownCode_fallsBackToDraft_neverThrows() {
        assertEquals(ReadinessState.Draft, ReadinessState.fromCode(99))
        assertEquals(ReadinessState.Draft, ReadinessState.fromCode(-1))
    }

    @Test
    fun dto_toDomain_decodesIntStatesToEnums() {
        val dto = PerformanceReviewsReadinessDto(
            id = "id-1",
            code = "PR-1",
            displayName = "Test",
            performanceReviewReadinessState = 1, // Ready
            reviewCycleBoundaryState = 3, // Blocked
            dependencyStates = mapOf("goals" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.performanceReviewReadinessState)
        assertEquals(ReadinessState.Blocked, domain.reviewCycleBoundaryState)
        assertEquals(ReadinessState.NotRequired, domain.dependencyStates.getValue("goals"))
        assertEquals("2026-09-11T10:15:30Z", domain.lastEvaluatedAt.toString())
    }

    @Test
    fun malformedLastEvaluatedAt_isNull_neverThrows() {
        val dto = readinessDtoWith(lastEvaluatedAt = "not-a-date")
        assertEquals(null, dto.toDomain().lastEvaluatedAt)
    }

    @Test
    fun new_toCreateRequest_encodesEnumsToIntCodes() {
        val new = NewPerformanceReviews(code = "PR-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft readiness (0), Blocked review-cycle (3),
        // Deferred goal-dependency (2).
        assertEquals(0, request.performanceReviewReadinessState)
        assertEquals(3, request.reviewCycleBoundaryState)
        assertEquals(2, request.goalDependencyState)
        assertEquals(3, request.scoringBoundaryState)
        assertEquals(2, request.consentPreconditionState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.performanceReviewReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewPerformanceReviews(code = "PR-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(PerformanceReviewsCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"performanceReviewReadinessState\":0"))
        assertTrue(wire, wire.contains("\"reviewCycleBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"goalDependencyState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): PerformanceReviewsReadinessDto =
        PerformanceReviewsReadinessDto(
            id = "id-1",
            code = "PR-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
