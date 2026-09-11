package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.mapper

import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsCreateRequestDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto.CompensationBenefitsReadinessDto
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.NewCompensationBenefits
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 *
 * ⚠️ THIS module's codes DIFFER from the pilot: `Ready=1, Deferred=2`.
 */
class CompensationBenefitsMapperTest {

    private val json = Json { encodeDefaults = true }

    @Test
    fun everyStateCode_roundTripsThroughFromCode() {
        // All 6 states map by their fixed backend codes (Ready=1, Deferred=2 here).
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
        val dto = CompensationBenefitsReadinessDto(
            id = "id-1",
            code = "CB-1",
            displayName = "Test",
            compensationBenefitsReadinessState = 1, // Ready
            compensationPlanBoundaryState = 3, // Blocked
            dependencyStates = mapOf("docs" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.compensationBenefitsReadinessState)
        assertEquals(ReadinessState.Blocked, domain.compensationPlanBoundaryState)
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
        val new = NewCompensationBenefits(code = "CB-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft(0) top-level, Blocked(3) boundaries, Deferred(2) dependencies.
        assertEquals(0, request.compensationBenefitsReadinessState)
        assertEquals(3, request.compensationPlanBoundaryState)
        assertEquals(2, request.compensationSourceDependencyState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.compensationBenefitsReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewCompensationBenefits(code = "CB-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(CompensationBenefitsCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"compensationBenefitsReadinessState\":0"))
        assertTrue(wire, wire.contains("\"compensationPlanBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"compensationSourceDependencyState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): CompensationBenefitsReadinessDto =
        CompensationBenefitsReadinessDto(
            id = "id-1",
            code = "CB-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
