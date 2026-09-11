package eu.grandmedical.diten.mobile.core.sync

import android.content.Context
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.OutOfQuotaPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import dagger.hilt.android.qualifiers.ApplicationContext
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Enqueues the [SyncWorker] with WorkManager.
 *
 * Two entry points:
 *  - [enqueuePeriodicSync] — a battery-friendly recurring background pass
 *    (every 15 minutes, the WorkManager minimum) that only runs while connected.
 *    Uses unique periodic work with [ExistingPeriodicWorkPolicy.KEEP] so repeated
 *    calls (e.g. every app start) never stack duplicate schedules.
 *  - [enqueueOneTimeSync] — an expedited one-shot pass for "sync now" moments
 *    (e.g. right after a local write), deduplicated by unique work name.
 */
@Singleton
class SyncScheduler @Inject constructor(
    @ApplicationContext private val context: Context,
) {

    fun enqueuePeriodicSync() {
        val request = PeriodicWorkRequestBuilder<SyncWorker>(
            PERIODIC_INTERVAL_MINUTES,
            TimeUnit.MINUTES,
        ).setConstraints(connectedConstraints()).build()

        WorkManager.getInstance(context).enqueueUniquePeriodicWork(
            PERIODIC_WORK_NAME,
            ExistingPeriodicWorkPolicy.KEEP,
            request,
        )
    }

    fun enqueueOneTimeSync() {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(connectedConstraints())
            .setExpedited(OutOfQuotaPolicy.RUN_AS_NON_EXPEDITED_WORK_REQUEST)
            .build()

        WorkManager.getInstance(context).enqueueUniqueWork(
            ONE_TIME_WORK_NAME,
            ExistingWorkPolicy.KEEP,
            request,
        )
    }

    private fun connectedConstraints(): Constraints =
        Constraints.Builder()
            .setRequiredNetworkType(NetworkType.CONNECTED)
            .build()

    companion object {
        /** WorkManager's minimum periodic interval. */
        private const val PERIODIC_INTERVAL_MINUTES = 15L

        /** Unique work name for the recurring background sync. */
        const val PERIODIC_WORK_NAME = "diten-sync-periodic"

        /** Unique work name for the on-demand one-shot sync. */
        const val ONE_TIME_WORK_NAME = "diten-sync-one-time"
    }
}
