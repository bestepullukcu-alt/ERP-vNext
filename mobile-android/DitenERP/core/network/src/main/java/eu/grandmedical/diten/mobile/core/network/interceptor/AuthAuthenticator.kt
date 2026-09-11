package eu.grandmedical.diten.mobile.core.network.interceptor

import eu.grandmedical.diten.mobile.core.network.NetworkHeaders
import eu.grandmedical.diten.mobile.core.network.session.SessionTokenProvider
import eu.grandmedical.diten.mobile.core.network.session.TokenRefresher
import kotlinx.coroutines.runBlocking
import okhttp3.Authenticator
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route
import javax.inject.Inject

/**
 * OkHttp [Authenticator] that reacts to a 401 on an *authenticated* request by
 * asking [TokenRefresher] to refresh, then retrying the request ONCE with the
 * new bearer token.
 *
 * Loop safety:
 *  - only authenticated requests are retried (those that carried an
 *    `Authorization` header when they hit the wire);
 *  - the request is retried at most once, guarded by [responseCount]; a second
 *    401 (or a failed refresh, or a missing token) returns null and gives up.
 *
 * `runBlocking` is acceptable here: OkHttp invokes the authenticator on a
 * background dispatcher thread, and refresh must complete before the retry.
 */
class AuthAuthenticator @Inject constructor(
    private val tokenRefresher: TokenRefresher,
    private val sessionTokenProvider: SessionTokenProvider,
) : Authenticator {

    // Sequential guard clauses (each an early return) read more clearly than a
    // nested expression for this fail-fast refresh path.
    @Suppress("ReturnCount")
    override fun authenticate(route: Route?, response: Response): Request? {
        val failedRequest = response.request

        // Only unauthenticated-by-omission requests get here without this header;
        // never try to refresh for e.g. the login endpoint.
        if (failedRequest.header(NetworkHeaders.AUTHORIZATION) == null) {
            return null
        }

        // Already retried once -> stop to avoid an infinite refresh loop.
        if (responseCount(response) >= MAX_ATTEMPTS) {
            return null
        }

        val refreshed = runBlocking { tokenRefresher.refresh() }
        if (!refreshed) {
            return null
        }

        val newToken = runBlocking { sessionTokenProvider.accessToken() }
        if (newToken.isNullOrBlank()) {
            return null
        }

        return failedRequest.newBuilder()
            .header(NetworkHeaders.AUTHORIZATION, NetworkHeaders.BEARER_PREFIX + newToken)
            .build()
    }

    private fun responseCount(response: Response): Int {
        var count = 1
        var prior = response.priorResponse
        while (prior != null) {
            count++
            prior = prior.priorResponse
        }
        return count
    }

    private companion object {
        const val MAX_ATTEMPTS = 2
    }
}
