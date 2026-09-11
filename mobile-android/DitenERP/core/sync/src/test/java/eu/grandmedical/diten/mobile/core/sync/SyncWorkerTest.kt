package eu.grandmedical.diten.mobile.core.sync

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.work.ListenableWorker
import androidx.work.ListenableWorker.Result
import androidx.work.WorkerFactory
import androidx.work.WorkerParameters
import androidx.work.testing.TestListenableWorkerBuilder
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

/**
 * Verifies the WorkManager contract mapping via [TestListenableWorkerBuilder]:
 * an engine that succeeds -> [Result.success]; an engine whose pass is retryable
 * -> [Result.retry]. The [SyncEngine] is injected directly (no Hilt/app wiring),
 * mirroring how M0.7's app-level factory will provide it at runtime.
 */
@RunWith(RobolectricTestRunner::class)
class SyncWorkerTest {

    private class FakeHandler(private val result: SyncResult) : SyncHandler {
        override val key: String = "fake"
        override suspend fun sync(): SyncResult = result
    }

    private fun buildWorker(engine: SyncEngine): SyncWorker {
        val context = ApplicationProvider.getApplicationContext<Context>()
        val factory = object : WorkerFactory() {
            override fun createWorker(
                appContext: Context,
                workerClassName: String,
                workerParameters: WorkerParameters,
            ): ListenableWorker = SyncWorker(appContext, workerParameters, engine)
        }
        return TestListenableWorkerBuilder<SyncWorker>(context)
            .setWorkerFactory(factory)
            .build()
    }

    @Test
    fun engine_success_maps_to_result_success() = runTest {
        val engine = SyncEngine(setOf(FakeHandler(SyncResult.Success)))

        val result = buildWorker(engine).doWork()

        assertEquals(Result.success(), result)
    }

    @Test
    fun engine_retryable_maps_to_result_retry() = runTest {
        val engine = SyncEngine(setOf(FakeHandler(SyncResult.Retry("offline"))))

        val result = buildWorker(engine).doWork()

        assertEquals(Result.retry(), result)
    }

    @Test
    fun engine_failure_maps_to_result_failure() = runTest {
        val engine = SyncEngine(setOf(FakeHandler(SyncResult.Failure("permanent"))))

        val result = buildWorker(engine).doWork()

        assertEquals(Result.failure(), result)
    }
}
