package eu.grandmedical.diten.mobile.core.auth.di

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import eu.grandmedical.diten.mobile.core.auth.seam.SessionTokenProviderImpl
import eu.grandmedical.diten.mobile.core.auth.seam.TokenRefresherImpl
import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import eu.grandmedical.diten.mobile.core.network.session.TokenRefresher
import javax.inject.Singleton

/**
 * REAL network seam bindings (M0.4). These SUPERSEDE the no-op bindings that
 * `:core:network`'s NetworkSeamModule used to provide - those have been removed,
 * so exactly one binding exists per interface (Hilt forbids duplicates). The app
 * assembling end-to-end proves these resolve with no missing/duplicate binding
 * and no dependency cycle (see [TokenRefresherImpl] for the Lazy cycle-breaker).
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class AuthNetworkModule {

    @Binds
    @Singleton
    abstract fun bindSessionTokenProvider(impl: SessionTokenProviderImpl): SessionTokenProvider

    @Binds
    @Singleton
    abstract fun bindTokenRefresher(impl: TokenRefresherImpl): TokenRefresher
}
