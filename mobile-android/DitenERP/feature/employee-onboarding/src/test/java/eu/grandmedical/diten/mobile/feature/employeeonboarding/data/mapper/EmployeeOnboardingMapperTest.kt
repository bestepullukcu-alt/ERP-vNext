package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.mapper

import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingCreateRequestDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto.EmployeeOnboardingReadinessDto
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.NewEmployeeOnboarding
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 *
 * NOTE: this module's enum codes are `Draft=0, Ready=1, Deferred=2, Blocked=3,
 * NotRequired=4, Archived=5` — Ready/Deferred are swapped relative to the
 * applicant-intake pilot, matching the measured backend enum EXACTLY.
 */
class EmployeeOnboardingMapperTest {

    private val json = Json { encodeDefaults = true }

    @Test
    fun everyStateCode_roundTripsThroughFromCode() {
        // All 6 states map by their fixed backend codes.
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
        val dto = EmployeeOnboardingReadinessDto(
            id = "id-1",
            code = "EO-1",
            displayName = "Test",
            onboardingReadinessState = 1, // Ready
            lifecycleBoundaryState = 3, // Blocked
            dependencyStates = mapOf("docs" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.onboardingReadinessState)
        assertEquals(ReadinessState.Blocked, domain.lifecycleBoundaryState)
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
        val new = NewEmployeeOnboarding(code = "EO-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft onboarding, Blocked execution boundaries, Deferred facets.
        assertEquals(0, request.onboardingReadinessState)
        assertEquals(3, request.lifecycleBoundaryState)
        assertEquals(2, request.candidateTransitionBoundaryState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.onboardingReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewEmployeeOnboarding(code = "EO-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(EmployeeOnboardingCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"onboardingReadinessState\":0"))
        assertTrue(wire, wire.contains("\"lifecycleBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"candidateTransitionBoundaryState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): EmployeeOnboardingReadinessDto =
        EmployeeOnboardingReadinessDto(
            id = "id-1",
            code = "EO-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
