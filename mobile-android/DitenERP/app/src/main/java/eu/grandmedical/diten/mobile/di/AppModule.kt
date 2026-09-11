package eu.grandmedical.diten.mobile.di

import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Named
import javax.inject.Singleton

/**
 * App-wide Hilt bindings. This trivial module exists to prove that the Hilt
 * graph is generated and resolved at runtime; real bindings arrive later.
 */
@Module
@InstallIn(SingletonComponent::class)
object AppModule {

    /** A trivial app-version string provided through the graph and injected into [MainActivity]. */
    @Provides
    @Singleton
    @Named("appVersion")
    fun provideAppVersion(): String = "0.1.0-skeleton"
}
