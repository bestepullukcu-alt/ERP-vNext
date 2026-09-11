package eu.grandmedical.diten.mobile.feature.login

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.auth.data.LoginResult
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import javax.inject.Inject

/**
 * Drives the login screen. Field-change events reduce into [LoginState]; [Submit]
 * calls [AuthRepository.login] and maps the [UiResult] onto state + one-shot
 * [LoginEffect]s:
 *  - [UiResult.Loading]                     -> `isLoading = true`
 *  - [UiResult.Success] + [LoginResult.Authenticated] -> [LoginEffect.NavigateHome]
 *  - [UiResult.Success] + [LoginResult.MfaRequired]   -> [LoginEffect.NavigateMfa]
 *  - [UiResult.Error]                       -> surface `error`, no navigation
 */
@HiltViewModel
class LoginViewModel @Inject constructor(
    private val authRepository: AuthRepository,
) : MviViewModel<LoginState, LoginEvent, LoginEffect>(LoginState()) {

    override suspend fun handleEvent(event: LoginEvent) {
        when (event) {
            is LoginEvent.EmailChanged -> setState { copy(email = event.value, error = null) }
            is LoginEvent.PasswordChanged -> setState { copy(password = event.value, error = null) }
            is LoginEvent.TenantIdChanged -> setState { copy(tenantId = event.value, error = null) }
            is LoginEvent.RememberMeChanged -> setState { copy(rememberMe = event.value) }
            LoginEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return

        setState { copy(isLoading = true, error = null) }
        when (
            val result = authRepository.login(
                current.email,
                current.password,
                current.tenantId,
                current.rememberMe,
            )
        ) {
            is UiResult.Loading -> setState { copy(isLoading = true) }
            is UiResult.Success -> handleSuccess(result.data)
            is UiResult.Error -> setState { copy(isLoading = false, error = result.message) }
        }
    }

    private fun handleSuccess(result: LoginResult) {
        setState { copy(isLoading = false, error = null) }
        when (result) {
            is LoginResult.Authenticated -> sendEffect(LoginEffect.NavigateHome)
            is LoginResult.MfaRequired -> sendEffect(LoginEffect.NavigateMfa(result.challengeId))
        }
    }
}
