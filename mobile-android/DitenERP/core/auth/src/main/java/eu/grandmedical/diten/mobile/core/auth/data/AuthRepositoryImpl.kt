package eu.grandmedical.diten.mobile.core.auth.data

import eu.grandmedical.diten.mobile.core.auth.jwt.JwtDecoder
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.auth.session.SessionManager
import eu.grandmedical.diten.mobile.core.auth.token.EncryptedTokenStore
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.network.ApiResponse
import eu.grandmedical.diten.mobile.core.network.SafeApiCaller
import eu.grandmedical.diten.mobile.core.network.api.AuthApi
import eu.grandmedical.diten.mobile.core.network.dto.AuthResponse
import eu.grandmedical.diten.mobile.core.network.dto.LoginRequest
import eu.grandmedical.diten.mobile.core.network.dto.MfaVerifyRequest
import eu.grandmedical.diten.mobile.core.network.dto.RefreshTokenRequest
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Data-layer [AuthRepository]. Bridges the transport ([AuthApi] + [SafeApiCaller])
 * to durable state ([EncryptedTokenStore]) and in-memory session state
 * ([SessionManager]). The `legal_entities` CSV claim is parsed from the freshly
 * issued access token to seed the available-entity set and default selection.
 */
@Singleton
class AuthRepositoryImpl @Inject constructor(
    private val authApi: AuthApi,
    private val safeApiCaller: SafeApiCaller,
    private val tokenStore: EncryptedTokenStore,
    private val sessionManager: SessionManager,
    private val jwtDecoder: JwtDecoder,
) : AuthRepository {

    // Tenant id in flight during an MFA challenge, so verifyMfa can re-stamp
    // X-Tenant-Id before the session (and thus a stored tenant) exists.
    @Volatile
    private var pendingTenantId: String? = null

    override suspend fun login(
        email: String,
        password: String,
        tenantId: String,
        rememberMe: Boolean,
    ): UiResult<LoginResult> {
        val response = safeApiCaller.apiCall {
            authApi.login(tenantId, LoginRequest(email = email, password = password, rememberMe = rememberMe))
        }
        return when (response) {
            is ApiResponse.Success -> handleAuthSuccess(response.data, fallbackTenantId = tenantId)
            is ApiResponse.Failure -> UiResult.Error(response.error.message)
        }
    }

    override suspend fun verifyMfa(challengeId: String, code: String): UiResult<LoginResult> {
        val tenantId = pendingTenantId ?: tokenStore.tenantId()
            ?: return UiResult.Error("No tenant context for MFA verification.")
        val response = safeApiCaller.apiCall {
            authApi.verifyMfa(tenantId, MfaVerifyRequest(challengeId = challengeId, code = code))
        }
        return when (response) {
            is ApiResponse.Success -> handleAuthSuccess(response.data, fallbackTenantId = tenantId)
            is ApiResponse.Failure -> UiResult.Error(response.error.message)
        }
    }

    // Fail-fast guard clauses (no tokens / bad response) read more clearly as
    // early returns than as a nested expression.
    @Suppress("ReturnCount")
    override suspend fun refresh(): Boolean {
        val accessToken = tokenStore.accessToken()
        val refreshToken = tokenStore.refreshToken()
        if (accessToken.isNullOrBlank() || refreshToken.isNullOrBlank()) {
            clearSession()
            return false
        }
        val response = safeApiCaller.apiCall {
            authApi.refreshToken(RefreshTokenRequest(accessToken = accessToken, refreshToken = refreshToken))
        }
        val payload = (response as? ApiResponse.Success)?.data
        val newAccess = payload?.accessToken
        if (payload == null || newAccess.isNullOrBlank()) {
            clearSession()
            return false
        }
        val claims = jwtDecoder.decode(newAccess)
        tokenStore.updateTokens(
            accessToken = newAccess,
            refreshToken = payload.refreshToken ?: refreshToken,
            expiresAt = claims.expiresAt,
        )
        sessionManager.restore()
        return true
    }

    override suspend fun logout() {
        runCatching { authApi.logout() }
        clearSession()
    }

    // Distinct success shapes (MFA / missing token / established session) each
    // return early; a nested expression would be less readable here.
    @Suppress("ReturnCount")
    private suspend fun handleAuthSuccess(
        payload: AuthResponse,
        fallbackTenantId: String,
    ): UiResult<LoginResult> {
        if (payload.requiresMfa) {
            pendingTenantId = fallbackTenantId
            return UiResult.Success(
                LoginResult.MfaRequired(
                    challengeId = payload.challengeId.orEmpty(),
                    maskedDestination = payload.maskedDestination,
                    channel = payload.channel,
                ),
            )
        }
        val accessToken = payload.accessToken
            ?: return UiResult.Error("Login response contained no access token.")

        val claims = jwtDecoder.decode(accessToken)
        val available = claims.legalEntities
        tokenStore.persistSession(
            accessToken = accessToken,
            refreshToken = payload.refreshToken,
            expiresAt = claims.expiresAt,
            tenantId = claims.tenantId ?: fallbackTenantId,
            availableLegalEntities = available,
            selectedLegalEntityId = available.firstOrNull(),
        )
        pendingTenantId = null
        sessionManager.restore()

        val state = sessionManager.authState.value as? AuthState.Authenticated
            ?: return UiResult.Error("Session could not be established.")
        return UiResult.Success(LoginResult.Authenticated(state))
    }

    private suspend fun clearSession() {
        tokenStore.clear()
        pendingTenantId = null
        sessionManager.markUnauthenticated()
    }
}
