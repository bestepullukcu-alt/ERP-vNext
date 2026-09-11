package eu.grandmedical.diten.mobile.feature.login

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState

/** Render state for the minimal MFA (second-factor) code screen. */
data class MfaState(
    val challengeId: String = "",
    val code: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
) : UiState {
    val canSubmit: Boolean
        get() = !isLoading && code.isNotBlank()
}

/** User intents for the MFA screen. */
sealed interface MfaEvent : UiEvent {
    data class CodeChanged(val value: String) : MfaEvent
    data object Submit : MfaEvent
}

/** One-shot effects for the MFA screen. */
sealed interface MfaEffect : UiEffect {
    data object NavigateHome : MfaEffect
}
