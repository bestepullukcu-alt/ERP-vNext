package eu.grandmedical.diten.mobile.core.network.di

import eu.grandmedical.diten.mobile.core.network.CertificatePinnerProvider
import eu.grandmedical.diten.mobile.core.network.EmptyCertificatePinnerProvider
import eu.grandmedical.diten.mobile.core.network.session.NoOpSessionTokenProvider
import eu.grandmedical.diten.mobile.core.network.session.NoOpTokenRefresher
import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import eu.grandmedical.diten.mobile.core.network.session.TokenRefresher
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

/**
 * DEFAULT seam bindings so the Hilt graph is satisfied and `:app` assembles
 * today, before `:core:auth` exists.
 *
 * OVERRIDE PLAN (M0.4): `:core:auth` ships storage-backed implementations of
 * [SessionTokenProvider] and [TokenRefresher]. Hilt forbids two bindings for
 * the same type, so this module is the one to REPLACE: when `:core:auth` lands,
 * delete (or empty) this module and let auth's bindings module install the real
 * ones. Nothing else in `:core:network` needs to change - it depends only on
 * the interfaces. The [CertificatePinnerProvider] default may stay until prod
 * pins are introduced.
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class NetworkSeamModule {

    @Binds
    @Singleton
    abstract fun bindSessionTokenProvider(impl: NoOpSessionTokenProvider): SessionTokenProvider

    @Binds
    @Singleton
    abstract fun bindTokenRefresher(impl: NoOpTokenRefresher): TokenRefresher

    @Binds
    @Singleton
    abstract fun bindCertificatePinnerProvider(impl: EmptyCertificatePinnerProvider): CertificatePinnerProvider
}
