package eu.grandmedical.diten.mobile.feature.learningtraining.data.mapper

import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.learningtraining.data.dto.LearningTrainingReadinessDto
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.NewLearningTraining
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 *
 * ⚠️ This module's backend enum is `Draft=0, Ready=1, Deferred=2, Blocked=3,
 * NotRequired=4, Archived=5` — NOT the applicant-intake pilot's `Deferred=1,
 * Ready=2`. These assertions lock the correct positions in.
 */
class LearningTrainingMapperTest {

    private val json = Json { encodeDefaults = true }

    @Test
    fun everyStateCode_roundTripsThroughFromCode() {
        // All 6 states map by their fixed backend codes (Ready=1, Deferred=2).
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
        val dto = LearningTrainingReadinessDto(
            id = "id-1",
            code = "LT-1",
            displayName = "Test",
            learningTrainingReadinessState = 1, // Ready
            courseCatalogBoundaryState = 3, // Blocked
            dependencyStates = mapOf("content" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.learningTrainingReadinessState)
        assertEquals(ReadinessState.Blocked, domain.courseCatalogBoundaryState)
        assertEquals(ReadinessState.NotRequired, domain.dependencyStates.getValue("content"))
        assertEquals("2026-09-11T10:15:30Z", domain.lastEvaluatedAt.toString())
    }

    @Test
    fun malformedLastEvaluatedAt_isNull_neverThrows() {
        val dto = readinessDtoWith(lastEvaluatedAt = "not-a-date")
        assertEquals(null, dto.toDomain().lastEvaluatedAt)
    }

    @Test
    fun new_toCreateRequest_encodesEnumsToIntCodes() {
        val new = NewLearningTraining(code = "LT-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft top-level state, Blocked=3 boundaries, Deferred=2 deps.
        assertEquals(0, request.learningTrainingReadinessState)
        assertEquals(3, request.courseCatalogBoundaryState)
        assertEquals(2, request.consentPreconditionState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.learningTrainingReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewLearningTraining(code = "LT-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(LearningTrainingCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"learningTrainingReadinessState\":0"))
        assertTrue(wire, wire.contains("\"courseCatalogBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"consentPreconditionState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): LearningTrainingReadinessDto =
        LearningTrainingReadinessDto(
            id = "id-1",
            code = "LT-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
