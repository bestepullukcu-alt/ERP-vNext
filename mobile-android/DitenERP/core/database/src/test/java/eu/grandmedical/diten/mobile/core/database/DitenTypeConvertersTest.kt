package eu.grandmedical.diten.mobile.core.database

import eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.Instant

/** Round-trips every [DitenTypeConverters] mapping, including edge cases. */
class DitenTypeConvertersTest {

    private val converters = DitenTypeConverters()

    @Test
    fun instant_roundTrips() {
        val now = Instant.ofEpochMilli(1_726_000_000_000L)
        val stored = converters.instantToEpochMillis(now)
        assertEquals(now, converters.epochMillisToInstant(stored))
    }

    @Test
    fun instant_nullRoundTrips() {
        assertNull(converters.instantToEpochMillis(null))
        assertNull(converters.epochMillisToInstant(null))
    }

    @Test
    fun stringList_roundTrips() {
        val tags = listOf("payroll", "audit", "eu-only")
        val stored = converters.stringListToString(tags)
        assertEquals(tags, converters.stringToStringList(stored))
    }

    @Test
    fun stringList_emptyAndNullAreDistinct() {
        assertEquals(emptyList<String>(), converters.stringToStringList(""))
        assertEquals("", converters.stringListToString(emptyList()))
        assertNull(converters.stringToStringList(null))
        assertNull(converters.stringListToString(null))
    }

    @Test
    fun syncStatus_roundTrips() {
        SyncStatus.entries.forEach { status ->
            val stored = converters.syncStatusToName(status)
            assertEquals(status, converters.nameToSyncStatus(stored))
        }
    }
}
