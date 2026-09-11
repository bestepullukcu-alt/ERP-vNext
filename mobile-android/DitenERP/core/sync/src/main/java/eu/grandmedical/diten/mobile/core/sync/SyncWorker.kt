package eu.grandmedical.diten.mobile.core.sync

import android.content.Context
import androidx.hilt.work.HiltWorker
import androidx.work.CoroutineWorker
import androidx.work.ListenableWorker.Result
import androidx.work.WorkerParameters
import dagger.assisted.Assisted
import dagger.assisted.AssistedInject

/**
 * The WorkManager entry point for a background sync pass.
 *
 * `@HiltWorker` + `@AssistedInject` let Hilt inject the [SyncEngine] while
 * WorkManager supplies the [Context] and [WorkerParameters]. The result maps the
 * aggregated [SyncSummary] onto WorkManager's retry contract:
 *  - retryable (any transient failure) -> [Result.retry];
 *  - permanent failures present -> [Result.failure];
 *  - otherwise -> [Result.success].
 *
 * Retry precedence over failure is deliberate: a transient issue should be retried
 * rather than abandoned.
 *
 * NOTE: for `@HiltWorker` injection to work at runtime the `:app` module must
 * provide a `HiltWorkerFactory` (Application implements `Configuration.Provider`,
 * default WorkManager initializer removed in the manifest). That app wiring lands
 * in M0.7; unit tests here inject the engine directly via `TestListenableWorkerBuilder`.
 */
@HiltWorker
class SyncWorker @AssistedInject constructor(
    @Assisted appContext: Context,
    @Assisted params: WorkerParameters,
    private val syncEngine: SyncEngine,
) : CoroutineWorker(appContext, params) {

    override suspend fun doWork(): Result {
        val summary = syncEngine.syncAll()
        return when {
            summary.retryable -> Result.retry()
            summary.failures.isNotEmpty() -> Result.failure()
            else -> Result.success()
        }
    }
}
