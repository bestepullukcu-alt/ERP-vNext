package eu.grandmedical.diten.mobile.core.network.di

import eu.grandmedical.diten.mobile.core.network.CertificatePinnerProvider
import eu.grandmedical.diten.mobile.core.network.EmptyCertificatePinnerProvider
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

/**
 * Remaining DEFAULT seam binding for `:core:network`.
 *
 * As of M0.4 the [SessionTokenProvider][eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider]
 * and [TokenRefresher][eu.grandmedical.diten.mobile.core.network.session.TokenRefresher]
 * no-op bindings have been REMOVED from here: `:core:auth` now installs the real,
 * storage-backed implementations (Hilt forbids two bindings for one type). Only
 * the [CertificatePinnerProvider] default remains, and may stay until production
 * certificate pins are introduced.
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class NetworkSeamModule {

    @Binds
    @Singleton
    abstract fun bindCertificatePinnerProvider(impl: EmptyCertificatePinnerProvider): CertificatePinnerProvider
}
