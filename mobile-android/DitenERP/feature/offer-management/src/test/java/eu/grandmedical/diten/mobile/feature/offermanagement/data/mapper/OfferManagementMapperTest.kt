package eu.grandmedical.diten.mobile.feature.offermanagement.data.mapper

import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementCreateRequestDto
import eu.grandmedical.diten.mobile.feature.offermanagement.data.dto.OfferManagementReadinessDto
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.NewOfferManagement
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 *
 * NOTE: OfferManagement's int codes differ from the applicant-intake pilot's —
 * the measured `OfferReadinessState` enum is Ready=1, Deferred=2.
 */
class OfferManagementMapperTest {

    private val json = Json { encodeDefaults = true }

    @Test
    fun everyStateCode_roundTripsThroughFromCode() {
        // All 6 states map by their fixed OFFER backend codes.
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
        val dto = OfferManagementReadinessDto(
            id = "id-1",
            code = "OM-1",
            displayName = "Test",
            offerReadinessState = 1, // Ready
            offerWorkflowBoundaryState = 3, // Blocked
            dependencyStates = mapOf("docs" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.offerReadinessState)
        assertEquals(ReadinessState.Blocked, domain.offerWorkflowBoundaryState)
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
        val new = NewOfferManagement(code = "OM-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft top-level (0), Blocked workflow boundaries (3),
        // Deferred data boundaries/policies/dependencies (2).
        assertEquals(0, request.offerReadinessState)
        assertEquals(3, request.offerWorkflowBoundaryState)
        assertEquals(2, request.consentPreconditionState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.offerReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewOfferManagement(code = "OM-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(OfferManagementCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"offerReadinessState\":0"))
        assertTrue(wire, wire.contains("\"offerWorkflowBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"consentPreconditionState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): OfferManagementReadinessDto =
        OfferManagementReadinessDto(
            id = "id-1",
            code = "OM-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
