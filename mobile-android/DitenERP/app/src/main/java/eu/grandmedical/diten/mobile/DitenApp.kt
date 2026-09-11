package eu.grandmedical.diten.mobile

import android.app.Application
import androidx.hilt.work.HiltWorkerFactory
import androidx.work.Configuration
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject

/**
 * Application entry point. [HiltAndroidApp] triggers Hilt code generation and
 * hosts the singleton dependency graph for the whole app.
 *
 * Implements [Configuration.Provider] so WorkManager is initialized ON-DEMAND
 * with a [HiltWorkerFactory]. This is what lets :core:sync's `@HiltWorker`
 * ([eu.grandmedical.diten.mobile.core.sync.SyncWorker]) receive its injected
 * `SyncEngine` at construction time. The default `androidx.startup`
 * WorkManagerInitializer is removed in the manifest (see AndroidManifest.xml)
 * so this factory-backed configuration is the single initialization path.
 */
@HiltAndroidApp
class DitenApp : Application(), Configuration.Provider {

    @Inject
    lateinit var workerFactory: HiltWorkerFactory

    override val workManagerConfiguration: Configuration
        get() = Configuration.Builder()
            .setWorkerFactory(workerFactory)
            .build()
}
