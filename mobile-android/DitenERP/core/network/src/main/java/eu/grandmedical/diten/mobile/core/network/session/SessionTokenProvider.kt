package eu.grandmedical.diten.mobile.core.network.session

/**
 * Read-only view of the current session's authentication material, consumed by
 * the transport layer to stamp outgoing authenticated requests.
 *
 * This is a SEAM: `:core:network` ships only a no-op implementation
 * ([NoOpSessionTokenProvider]) so the Hilt graph compiles today. The real,
 * storage-backed implementation is provided by `:core:auth` in M0.4, which
 * overrides the binding.
 *
 * All accessors are `suspend` because the backing store (DataStore /
 * encrypted prefs) is asynchronous. A null/blank value means "not available",
 * in which case the corresponding header is omitted entirely.
 */
interface SessionTokenProvider {
    suspend fun accessToken(): String?

    suspend fun tenantId(): String?

    suspend fun legalEntityId(): String?
}

/**
 * Attempts a token refresh using the stored refresh token.
 *
 * Also a seam implemented by `:core:auth` in M0.4. Returns `true` when a fresh
 * access token is now available (so the caller may retry), `false` otherwise.
 */
interface TokenRefresher {
    suspend fun refresh(): Boolean
}
