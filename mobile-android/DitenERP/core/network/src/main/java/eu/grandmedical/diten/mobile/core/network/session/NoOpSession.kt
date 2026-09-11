package eu.grandmedical.diten.mobile.core.network.session

import javax.inject.Inject

/**
 * Default no-session provider so `:app` assembles before `:core:auth` exists.
 * Every accessor returns null, so [HeaderInterceptor]
 * [eu.grandmedical.diten.mobile.core.network.interceptor.HeaderInterceptor]
 * omits all auth headers. Superseded by the real provider in M0.4.
 */
class NoOpSessionTokenProvider @Inject constructor() : SessionTokenProvider {
    override suspend fun accessToken(): String? = null

    override suspend fun tenantId(): String? = null

    override suspend fun legalEntityId(): String? = null
}

/**
 * Default refresher that can never refresh (no stored credentials yet), so a
 * 401 is not retried. Superseded by the real refresher in M0.4.
 */
class NoOpTokenRefresher @Inject constructor() : TokenRefresher {
    override suspend fun refresh(): Boolean = false
}
