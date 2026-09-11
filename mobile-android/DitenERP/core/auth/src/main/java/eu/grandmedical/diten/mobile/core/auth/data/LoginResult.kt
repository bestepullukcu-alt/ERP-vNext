package eu.grandmedical.diten.mobile.core.auth.data

import eu.grandmedical.diten.mobile.core.auth.session.AuthState

/**
 * Outcome of a login / MFA-verify attempt that SUCCEEDED at the transport level.
 * Transport/validation failures are conveyed by wrapping this in
 * `UiResult.Error`; this type only distinguishes the two happy-path shapes:
 * a fully authenticated session vs. a second-factor challenge.
 */
sealed interface LoginResult {

    /** Credentials accepted and a session established. */
    data class Authenticated(val state: AuthState.Authenticated) : LoginResult

    /** Credentials accepted but the backend requires a second factor. */
    data class MfaRequired(
        val challengeId: String,
        val maskedDestination: String? = null,
        val channel: String? = null,
    ) : LoginResult
}
