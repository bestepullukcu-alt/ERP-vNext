package eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.mapper

import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveCreateRequestDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.NewTimeAttendanceLeave
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.ReadinessState
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Proves the DTO <-> domain mapping and, crucially, the Int <-> [ReadinessState]
 * wire encoding (HCM serializes the enum as an integer). Pure JVM — no emulator.
 *
 * NOTE: time-attendance-leave encodes `Ready=1, Deferred=2` (the OPPOSITE order of
 * applicant-intake, the SAME as candidate-pipeline). These tests lock that
 * measured encoding in.
 */
class TimeAttendanceLeaveMapperTest {

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
        val dto = TimeAttendanceLeaveReadinessDto(
            id = "id-1",
            code = "TAL-1",
            displayName = "Test",
            timeAttendanceLeaveReadinessState = 1, // Ready
            timesheetIntakeBoundaryState = 3, // Blocked
            dependencyStates = mapOf("timesheet" to 4), // NotRequired
            lastEvaluatedAt = "2026-09-11T10:15:30Z",
        )

        val domain = dto.toDomain()

        assertEquals(ReadinessState.Ready, domain.readinessState)
        assertEquals(ReadinessState.Blocked, domain.timesheetIntakeBoundaryState)
        assertEquals(ReadinessState.NotRequired, domain.dependencyStates.getValue("timesheet"))
        assertEquals("2026-09-11T10:15:30Z", domain.lastEvaluatedAt.toString())
    }

    @Test
    fun malformedLastEvaluatedAt_isNull_neverThrows() {
        val dto = readinessDtoWith(lastEvaluatedAt = "not-a-date")
        assertEquals(null, dto.toDomain().lastEvaluatedAt)
    }

    @Test
    fun new_toCreateRequest_encodesEnumsToIntCodes() {
        val new = NewTimeAttendanceLeave(code = "TAL-1", displayName = "Test")

        val request = new.toCreateRequest()

        // Backend defaults: Draft top-level readiness, Blocked boundaries, Deferred deps.
        assertEquals(0, request.timeAttendanceLeaveReadinessState)
        assertEquals(3, request.timesheetIntakeBoundaryState)
        assertEquals(3, request.automatedDecisionBoundaryState)
        assertEquals(2, request.timeAttendanceSourceDependencyState)
        assertEquals(2, request.consentPreconditionState)
        assertEquals("v1", request.sourceContractVersion)
        assertEquals(1L, request.timeAttendanceLeaveReadinessVersion)
    }

    @Test
    fun serializedCreateRequest_hasIntStates_and_v1ContractVersion() {
        val request = NewTimeAttendanceLeave(code = "TAL-1", displayName = "Test").toCreateRequest()

        val wire = json.encodeToString(TimeAttendanceLeaveCreateRequestDto.serializer(), request)

        // States are integers on the wire (no quotes) — HCM has no string enum converter.
        assertTrue(wire, wire.contains("\"timeAttendanceLeaveReadinessState\":0"))
        assertTrue(wire, wire.contains("\"timesheetIntakeBoundaryState\":3"))
        assertTrue(wire, wire.contains("\"timeAttendanceSourceDependencyState\":2"))
        // Contract version defaults to "v1", never a x.y.z string (marker guard).
        assertTrue(wire, wire.contains("\"sourceContractVersion\":\"v1\""))
    }

    private fun readinessDtoWith(lastEvaluatedAt: String?): TimeAttendanceLeaveReadinessDto =
        TimeAttendanceLeaveReadinessDto(
            id = "id-1",
            code = "TAL-1",
            displayName = "Test",
            lastEvaluatedAt = lastEvaluatedAt,
        )
}
