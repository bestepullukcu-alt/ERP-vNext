package eu.grandmedical.diten.mobile.navigation

import androidx.lifecycle.ViewModel
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.auth.session.SessionManager
import kotlinx.coroutines.flow.StateFlow
import javax.inject.Inject

/**
 * Shell-level ViewModel: surfaces the single source-of-truth [AuthState] so
 * [AppRoot] can pick the start destination and route back to Login whenever the
 * session drops (logout / token expiry).
 */
@HiltViewModel
class AppViewModel @Inject constructor(
    sessionManager: SessionManager,
) : ViewModel() {
    val authState: StateFlow<AuthState> = sessionManager.authState
}
