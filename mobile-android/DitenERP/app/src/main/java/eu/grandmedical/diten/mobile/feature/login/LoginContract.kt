package eu.grandmedical.diten.mobile.feature.login

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState

/**
 * Immutable render state for the login screen. The three inputs are needed
 * because tenant login stamps [tenantId] as the `X-Tenant-Id` header.
 */
data class LoginState(
    val email: String = "",
    val password: String = "",
    val tenantId: String = "",
    val rememberMe: Boolean = true,
    val isLoading: Boolean = false,
    val error: String? = null,
) : UiState {
    /** Submit is only enabled once all three fields are non-blank and idle. */
    val canSubmit: Boolean
        get() = !isLoading && email.isNotBlank() && password.isNotBlank() && tenantId.isNotBlank()
}

/** User intents for the login screen. */
sealed interface LoginEvent : UiEvent {
    data class EmailChanged(val value: String) : LoginEvent
    data class PasswordChanged(val value: String) : LoginEvent
    data class TenantIdChanged(val value: String) : LoginEvent
    data class RememberMeChanged(val value: Boolean) : LoginEvent
    data object Submit : LoginEvent
}

/** One-shot navigation effects emitted by the login screen. */
sealed interface LoginEffect : UiEffect {
    /** Credentials accepted and a session established. */
    data object NavigateHome : LoginEffect

    /** Backend requires a second factor; carry the challenge to the MFA screen. */
    data class NavigateMfa(val challengeId: String) : LoginEffect
}
