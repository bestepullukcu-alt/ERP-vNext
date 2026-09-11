package eu.grandmedical.diten.mobile.feature.login

import androidx.lifecycle.SavedStateHandle
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.auth.data.LoginResult
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.navigation.DitenDestination
import javax.inject.Inject

/**
 * Completes an MFA challenge started by [LoginViewModel]. The challenge id is
 * read from the navigation arguments via [SavedStateHandle]. On a verified
 * second factor the session is established and [MfaEffect.NavigateHome] fires.
 */
@HiltViewModel
class MfaViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val authRepository: AuthRepository,
) : MviViewModel<MfaState, MfaEvent, MfaEffect>(
    MfaState(challengeId = savedStateHandle[DitenDestination.Mfa.ARG_CHALLENGE_ID] ?: ""),
) {

    override suspend fun handleEvent(event: MfaEvent) {
        when (event) {
            is MfaEvent.CodeChanged -> setState { copy(code = event.value, error = null) }
            MfaEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return

        setState { copy(isLoading = true, error = null) }
        when (val result = authRepository.verifyMfa(current.challengeId, current.code)) {
            is UiResult.Loading -> setState { copy(isLoading = true) }
            is UiResult.Success -> handleSuccess(result.data)
            is UiResult.Error -> setState { copy(isLoading = false, error = result.message) }
        }
    }

    private fun handleSuccess(result: LoginResult) {
        when (result) {
            is LoginResult.Authenticated -> {
                setState { copy(isLoading = false, error = null) }
                sendEffect(MfaEffect.NavigateHome)
            }

            is LoginResult.MfaRequired ->
                setState { copy(isLoading = false, error = "Doğrulama kodu geçersiz, tekrar deneyin.") }
        }
    }
}
