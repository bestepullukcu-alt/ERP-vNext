package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.mapper

import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineCreateRequestDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto.CandidatePipelineReadinessDto
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.NewCandidatePipeline
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 *
 * NOTE: candidate-pipeline encodes `Ready=1, Deferred=2` (the OPPOSITE order of
 * applicant-intake). These tests lock that measured encoding in.
 */
class CandidatePipelineMapperTest {

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
        val dto = CandidatePipelineReadinessDto(
            id = "id-1",
            code = "CP-1",
            displayName = "Test",
            pipelineReadinessState = 1, // Ready
            candidateCommunicationBoundaryState = 3, // Blocked
            dependencyStates = mapOf("calendar" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.pipelineReadinessState)
        assertEquals(ReadinessState.Blocked, domain.candidateCommunicationBoundaryState)
        assertEquals(ReadinessState.NotRequired, domain.dependencyStates.getValue("calendar"))
        assertEquals("2026-09-11T10:15:30Z", domain.lastEvaluatedAt.toString())
    }

    @Test
    fun malformedLastEvaluatedAt_isNull_neverThrows() {
        val dto = readinessDtoWith(lastEvaluatedAt = "not-a-date")
        assertEquals(null, dto.toDomain().lastEvaluatedAt)
    }

    @Test
    fun new_toCreateRequest_encodesEnumsToIntCodes() {
        val new = NewCandidatePipeline(code = "CP-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft pipeline readiness, Deferred facets, Blocked boundaries.
        assertEquals(0, request.pipelineReadinessState)
        assertEquals(2, request.pipelineStageGovernanceState)
        assertEquals(3, request.candidateCommunicationBoundaryState)
        assertEquals(3, request.automatedDecisionBoundaryState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.pipelineReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewCandidatePipeline(code = "CP-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(CandidatePipelineCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"pipelineReadinessState\":0"))
        assertTrue(wire, wire.contains("\"candidateCommunicationBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"pipelineStageGovernanceState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): CandidatePipelineReadinessDto =
        CandidatePipelineReadinessDto(
            id = "id-1",
            code = "CP-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
