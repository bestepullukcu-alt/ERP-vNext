package eu.grandmedical.diten.mobile.core.auth.session

import eu.grandmedical.diten.mobile.core.auth.jwt.JwtDecoder
import eu.grandmedical.diten.mobile.core.auth.token.EncryptedTokenStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Single source of truth for the current [AuthState], surfaced as a
 * [StateFlow]. On construction it kicks off a best-effort [restore] from the
 * [EncryptedTokenStore]; callers that need determinism (tests, an explicit app
 * bootstrap) can `await` [restore] directly - it is idempotent.
 *
 * It also owns the legal-entity selection LOGIC: [selectLegalEntity] validates
 * the id against the available set, persists it, and updates the flow. The token
 * store remains the durable source of truth, so [SessionTokenProviderImpl] and a
 * fresh process both see the selection.
 */
@Singleton
class SessionManager @Inject constructor(
    private val tokenStore: EncryptedTokenStore,
    private val jwtDecoder: JwtDecoder,
) {

    private val restoreScope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    private val _authState = MutableStateFlow<AuthState>(AuthState.Unauthenticated)
    val authState: StateFlow<AuthState> = _authState.asStateFlow()

    init {
        restoreScope.launch { restore() }
    }

    val availableLegalEntities: List<String>
        get() = (_authState.value as? AuthState.Authenticated)?.availableLegalEntities.orEmpty()

    val selectedLegalEntityId: String?
        get() = (_authState.value as? AuthState.Authenticated)?.selectedLegalEntityId

    /**
     * Recomputes [authState] from the persisted store. A present, non-expired
     * access token yields [AuthState.Authenticated]; anything else yields
     * [AuthState.Unauthenticated]. Safe to call repeatedly.
     */
    suspend fun restore() {
        val accessToken = tokenStore.accessToken()
        if (accessToken.isNullOrBlank()) {
            _authState.value = AuthState.Unauthenticated
            return
        }
        val claims = jwtDecoder.decode(accessToken)
        val expirySeconds = tokenStore.expiresAt() ?: claims.expiresAt
        if (expirySeconds != null && expirySeconds * MILLIS_PER_SECOND <= System.currentTimeMillis()) {
            _authState.value = AuthState.Unauthenticated
            return
        }
        val available = tokenStore.availableLegalEntities()
        _authState.value = AuthState.Authenticated(
            userId = claims.subject,
            email = claims.email,
            tenantId = tokenStore.tenantId() ?: claims.tenantId,
            selectedLegalEntityId = tokenStore.selectedLegalEntityId() ?: available.firstOrNull(),
            availableLegalEntities = available,
        )
    }

    /** Drops the in-memory session to [AuthState.Unauthenticated]. */
    fun markUnauthenticated() {
        _authState.value = AuthState.Unauthenticated
    }

    /**
     * Selects [id] as the active legal entity. Returns `false` (and changes
     * nothing) when [id] is not in the available set; otherwise persists the
     * choice and updates the flow.
     */
    @Suppress("ReturnCount")
    suspend fun selectLegalEntity(id: String): Boolean {
        val current = _authState.value as? AuthState.Authenticated ?: return false
        if (id !in current.availableLegalEntities) return false
        tokenStore.setSelectedLegalEntity(id)
        _authState.value = current.copy(selectedLegalEntityId = id)
        return true
    }

    private companion object {
        const val MILLIS_PER_SECOND = 1000L
    }
}
