package eu.grandmedical.diten.mobile.core.common

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class UiResultTest {

    @Test
    fun success_holds_its_payload() {
        val result: UiResult<String> = UiResult.Success("diten")

        assertTrue(result is UiResult.Success)
        assertEquals("diten", (result as UiResult.Success).data)
    }

    @Test
    fun error_defaults_cause_to_null() {
        val result = UiResult.Error("boom")

        assertEquals("boom", result.message)
        assertNull(result.cause)
    }

    @Test
    fun loading_is_a_singleton() {
        assertTrue(UiResult.Loading === UiResult.Loading)
    }
}
