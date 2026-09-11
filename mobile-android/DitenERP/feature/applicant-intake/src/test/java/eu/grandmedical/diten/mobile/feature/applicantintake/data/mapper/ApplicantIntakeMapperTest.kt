package eu.grandmedical.diten.mobile.feature.applicantintake.data.mapper

import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeCreateRequestDto
import eu.grandmedical.diten.mobile.feature.applicantintake.data.dto.ApplicantIntakeReadinessDto
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.NewApplicantIntake
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 */
class ApplicantIntakeMapperTest {

    private val json = Json { encodeDefaults = true }

    @Test
    fun everyStateCode_roundTripsThroughFromCode() {
        // All 6 states map by their fixed backend codes.
        assertEquals(ReadinessState.Draft, ReadinessState.fromCode(0))
        assertEquals(ReadinessState.Deferred, ReadinessState.fromCode(1))
        assertEquals(ReadinessState.Ready, ReadinessState.fromCode(2))
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
        val dto = ApplicantIntakeReadinessDto(
            id = "id-1",
            code = "AI-1",
            displayName = "Test",
            intakeState = 2, // Ready
            publicUxBoundaryState = 3, // Blocked
            dependencyStates = mapOf("docs" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.intakeState)
        assertEquals(ReadinessState.Blocked, domain.publicUxBoundaryState)
        assertEquals(ReadinessState.NotRequired, domain.dependencyStates.getValue("docs"))
        assertEquals("2026-09-11T10:15:30Z", domain.lastEvaluatedAt.toString())
    }

    @Test
    fun malformedLastEvaluatedAt_isNull_neverThrows() {
        val dto = readinessDtoWith(lastEvaluatedAt = "not-a-date")
        assertEquals(null, dto.toDomain().lastEvaluatedAt)
    }

    @Test
    fun new_toCreateRequest_encodesEnumsToIntCodes() {
        val new = NewApplicantIntake(code = "AI-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft intake, Deferred facets, Blocked public UX.
        assertEquals(0, request.intakeState)
        assertEquals(1, request.sourceChannelState)
        assertEquals(3, request.publicUxBoundaryState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.applicantIntakeVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewApplicantIntake(code = "AI-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(ApplicantIntakeCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"intakeState\":0"))
        assertTrue(wire, wire.contains("\"publicUxBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"sourceChannelState\":1"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): ApplicantIntakeReadinessDto =
        ApplicantIntakeReadinessDto(
            id = "id-1",
            code = "AI-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
