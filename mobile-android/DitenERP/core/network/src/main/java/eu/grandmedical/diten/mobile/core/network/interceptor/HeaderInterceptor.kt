package eu.grandmedical.diten.mobile.core.network.interceptor

import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject

/**
 * Application interceptor that:
 *  - always advertises `Accept: application/json`;
 *  - for requests marked authenticated (they carry the internal
 *    [NetworkHeaders.AUTH_MARKER]), attaches `Authorization`, `X-Tenant-Id` and
 *    `X-Legal-Entity-Id` from the [SessionTokenProvider], OMITTING any header
 *    whose value is null or blank, then strips the marker so it never ships.
 *
 * Unauthenticated requests (e.g. tenant login) are left without auth headers.
 */
class HeaderInterceptor @Inject constructor(
    private val sessionTokenProvider: SessionTokenProvider,
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val original = chain.request()
        val builder = original.newBuilder()
            .header(NetworkHeaders.ACCEPT, NetworkHeaders.JSON_MEDIA_TYPE)

        val requiresAuth = original.header(NetworkHeaders.AUTH_MARKER) != null
        if (requiresAuth) {
            builder.removeHeader(NetworkHeaders.AUTH_MARKER)
            runBlocking {
                sessionTokenProvider.accessToken().addIfPresent(builder, NetworkHeaders.AUTHORIZATION) {
                    NetworkHeaders.BEARER_PREFIX + it
                }
                sessionTokenProvider.tenantId().addIfPresent(builder, NetworkHeaders.TENANT_ID)
                sessionTokenProvider.legalEntityId().addIfPresent(builder, NetworkHeaders.LEGAL_ENTITY_ID)
            }
        }

        return chain.proceed(builder.build())
    }

    private inline fun String?.addIfPresent(
        builder: okhttp3.Request.Builder,
        name: String,
        transform: (String) -> String = { it },
    ) {
        if (!this.isNullOrBlank()) {
            builder.header(name, transform(this))
        }
    }
}
