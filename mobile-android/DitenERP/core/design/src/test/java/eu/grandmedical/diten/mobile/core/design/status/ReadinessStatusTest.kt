package eu.grandmedical.diten.mobile.core.design.status

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Pure-JVM tests for the readiness status mapper. No Compose / no emulator:
 * only the framework-free token + label decision is exercised here.
 */
class ReadinessStatusTest {

    @Test
    fun ready_maps_to_success_green() {
        assertEquals(StatusToken.Success, statusToken("Ready"))
        assertEquals("Ready", statusLabel("Ready"))
    }

    @Test
    fun deferred_maps_to_warning_amber() {
        assertEquals(StatusToken.Warning, statusToken("Deferred"))
        assertEquals("Deferred", statusLabel("Deferred"))
    }

    @Test
    fun draft_maps_to_secondary_gray() {
        assertEquals(StatusToken.Secondary, statusToken("Draft"))
        assertEquals("Draft", statusLabel("Draft"))
    }

    @Test
    fun blocked_maps_to_danger_red() {
        assertEquals(StatusToken.Danger, statusToken("Blocked"))
        assertEquals("Blocked", statusLabel("Blocked"))
    }

    @Test
    fun not_required_maps_to_info_cyan() {
        assertEquals(StatusToken.Info, statusToken("NotRequired"))
        assertEquals("Not Required", statusLabel("NotRequired"))
    }

    @Test
    fun archived_maps_to_muted() {
        assertEquals(StatusToken.Muted, statusToken("Archived"))
        assertEquals("Archived", statusLabel("Archived"))
    }

    @Test
    fun mapping_is_case_insensitive() {
        assertEquals(StatusToken.Success, statusToken("ready"))
        assertEquals(StatusToken.Success, statusToken("READY"))
        assertEquals(StatusToken.Warning, statusToken("dEfErReD"))
    }

    @Test
    fun not_required_accepts_spelling_variants() {
        assertEquals(StatusToken.Info, statusToken("not_required"))
        assertEquals(StatusToken.Info, statusToken("not-required"))
        assertEquals(StatusToken.Info, statusToken("Not Required"))
    }

    @Test
    fun surrounding_whitespace_is_ignored() {
        assertEquals(StatusToken.Success, statusToken("  Ready  "))
    }

    @Test
    fun unknown_state_falls_back_safely() {
        assertEquals(StatusToken.Secondary, statusToken("gibberish"))
        assertEquals("Unknown", statusLabel("gibberish"))
        assertEquals(ReadinessStatus.Unknown, ReadinessStatus.from("gibberish"))
    }

    @Test
    fun null_and_blank_fall_back_safely() {
        assertEquals(StatusToken.Secondary, statusToken(null))
        assertEquals("Unknown", statusLabel(null))
        assertEquals(ReadinessStatus.Unknown, ReadinessStatus.from(""))
    }
}
