package eu.grandmedical.diten.mobile.core.auth.seam

import eu.grandmedical.diten.mobile.core.auth.token.EncryptedTokenStore
import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Real [SessionTokenProvider] backing the transport layer's authenticated
 * requests. Reads straight from the [EncryptedTokenStore] so the values survive
 * process death and reflect the persisted legal-entity selection.
 *
 * Deliberately depends ONLY on the store (not on the OkHttp/Retrofit graph it
 * feeds), so wiring it as the real seam introduces no dependency cycle.
 */
@Singleton
class SessionTokenProviderImpl @Inject constructor(
    private val tokenStore: EncryptedTokenStore,
) : SessionTokenProvider {

    override suspend fun accessToken(): String? = tokenStore.accessToken()

    override suspend fun tenantId(): String? = tokenStore.tenantId()

    override suspend fun legalEntityId(): String? = tokenStore.selectedLegalEntityId()
}
