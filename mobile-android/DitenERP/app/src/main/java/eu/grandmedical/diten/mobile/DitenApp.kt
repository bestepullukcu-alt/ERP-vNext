package eu.grandmedical.diten.mobile

import android.app.Application
import dagger.hilt.android.HiltAndroidApp

/**
 * Application entry point. [HiltAndroidApp] triggers Hilt code generation and
 * hosts the singleton dependency graph for the whole app.
 */
@HiltAndroidApp
class DitenApp : Application()
