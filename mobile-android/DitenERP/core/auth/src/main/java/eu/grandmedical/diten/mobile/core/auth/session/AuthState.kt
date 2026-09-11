package eu.grandmedical.diten.mobile.core.auth.session

/**
 * Coarse authentication state exposed to the rest of the app as a StateFlow.
 * The legal-entity SELECTOR is modelled here as data ([availableLegalEntities] +
 * [selectedLegalEntityId]); the switcher UI itself lands in M0.7.
 */
sealed interface AuthState {

    /** No valid, non-expired session. */
    data object Unauthenticated : AuthState

    /** A valid session restored from, or established into, the token store. */
    data class Authenticated(
        val userId: String?,
        val email: String?,
        val tenantId: String?,
        val selectedLegalEntityId: String?,
        val availableLegalEntities: List<String>,
    ) : AuthState
}
