package eu.grandmedical.diten.mobile.feature.compensationbenefits.data

import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.LocalDate

/** Proves the auto-default code is marker-safe, well-formed, and unique-ish. */
class CodeDefaultsTest {

    @Test
    fun defaultCode_isMarkerSafe_andWellFormed() {
        val code = CodeDefaults.defaultCode(today = LocalDate.of(2026, 9, 11))

        // Shape: CB-<yyMMdd>-<4 crockford base32 chars>.
        assertTrue(code, Regex("^CB-260911-[0-9A-HJKMNP-TV-Z]{4}$").matches(code))
        // Marker-safe: only the safe prefix, digits and unambiguous letters.
        assertTrue(code, code.all { it.isLetterOrDigit() || it == '-' })
    }

    @Test
    fun twoCalls_produceDifferentCodes() {
        val first = CodeDefaults.defaultCode()
        val second = CodeDefaults.defaultCode()
        // The random suffix makes collisions overwhelmingly unlikely.
        assertNotEquals(first, second)
    }
}
