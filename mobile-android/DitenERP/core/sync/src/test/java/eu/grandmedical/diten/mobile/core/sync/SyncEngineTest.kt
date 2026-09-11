package eu.grandmedical.diten.mobile.core.sync

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pure-JVM checks (no Robolectric) of the aggregation + robustness contract:
 * all-success, retryable propagation, recorded failures, and — importantly —
 * that a throwing or failing handler never prevents the others from running.
 */
class SyncEngineTest {

    private class FakeHandler(
        override val key: String,
        private val result: () -> SyncResult,
        private val onRun: () -> Unit = {},
    ) : SyncHandler {
        override suspend fun sync(): SyncResult {
            onRun()
            return result()
        }
    }

    @Test
    fun all_success_yields_overall_success() = runTest {
        val engine = SyncEngine(
            setOf(
                FakeHandler("a", { SyncResult.Success }),
                FakeHandler("b", { SyncResult.Success }),
            ),
        )

        val summary = engine.syncAll()

        assertEquals(2, summary.total)
        assertEquals(2, summary.succeeded)
        assertTrue(summary.isSuccess)
        assertFalse(summary.retryable)
        assertTrue(summary.failures.isEmpty())
    }

    @Test
    fun any_retry_makes_the_pass_retryable() = runTest {
        val engine = SyncEngine(
            setOf(
                FakeHandler("a", { SyncResult.Success }),
                FakeHandler("b", { SyncResult.Retry("offline") }),
            ),
        )

        val summary = engine.syncAll()

        assertTrue(summary.retryable)
        assertFalse(summary.isSuccess)
    }

    @Test
    fun failure_is_recorded_and_other_handlers_still_run() = runTest {
        var ranAfterFailure = false
        val engine = SyncEngine(
            // LinkedHashSet keeps insertion order so the failing handler runs first.
            linkedSetOf(
                FakeHandler("failer", { SyncResult.Failure("bad request") }),
                FakeHandler("later", { SyncResult.Success }, onRun = { ranAfterFailure = true }),
            ),
        )

        val summary = engine.syncAll()

        assertTrue("handler after a failure must still run", ranAfterFailure)
        assertEquals(1, summary.failures.size)
        assertTrue(summary.failures.first().contains("failer"))
        assertTrue(summary.failures.first().contains("bad request"))
        assertEquals(1, summary.succeeded)
        assertFalse(summary.isSuccess)
    }

    @Test
    fun a_throwing_handler_is_contained_and_recorded_as_failure() = runTest {
        var ranAfterThrow = false
        val engine = SyncEngine(
            linkedSetOf(
                FakeHandler("thrower", { throw IllegalStateException("boom") }),
                FakeHandler("later", { SyncResult.Success }, onRun = { ranAfterThrow = true }),
            ),
        )

        val summary = engine.syncAll()

        assertTrue("handler after a thrown exception must still run", ranAfterThrow)
        assertEquals(1, summary.failures.size)
        assertTrue(summary.failures.first().contains("thrower"))
        assertTrue(summary.failures.first().contains("boom"))
        assertEquals(1, summary.succeeded)
    }
}
