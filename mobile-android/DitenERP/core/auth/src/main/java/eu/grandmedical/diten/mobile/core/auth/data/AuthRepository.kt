package eu.grandmedical.diten.mobile.core.auth.data

import eu.grandmedical.diten.mobile.core.common.UiResult

/**
 * Domain contract for authentication. Implementations own token persistence and
 * session-state transitions; callers see only [UiResult] outcomes.
 */
interface AuthRepository {

    /**
     * Tenant login. [tenantId] is stamped as `X-Tenant-Id` on the request and
     * kept until the `tenant_id` JWT claim supersedes it. Returns a [LoginResult]
     * (authenticated OR MFA-required) on success, or `UiResult.Error` on failure.
     */
    suspend fun login(email: String, password: String, tenantId: String): UiResult<LoginResult>

    /** Completes an MFA challenge started by [login]. */
    suspend fun verifyMfa(challengeId: String, code: String): UiResult<LoginResult>

    /**
     * Refreshes the access token from the stored refresh token. Returns `true`
     * when a fresh token is now available; on failure the session is cleared and
     * `false` is returned. This is what the transport-layer [TokenRefresher] calls.
     */
    suspend fun refresh(): Boolean

    /** Best-effort server logout, then wipes the store and clears the session. */
    suspend fun logout()
}
