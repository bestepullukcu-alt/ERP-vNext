package eu.grandmedical.diten.mobile.navigation

import androidx.lifecycle.ViewModel
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.auth.session.SessionManager
import eu.grandmedical.diten.mobile.core.feature.FeatureNavGraph
import kotlinx.coroutines.flow.StateFlow
import javax.inject.Inject

/**
 * Shell-level ViewModel: surfaces the single source-of-truth [AuthState] so
 * [AppRoot] can pick the start destination and route back to Login whenever the
 * session drops (logout / token expiry).
 *
 * It also holds the Hilt-multibound [featureNavGraphs] — every installed
 * feature's Compose destinations — which [AppRoot] iterates while building its
 * `NavHost`. The shell references no feature type directly; discovery is entirely
 * through this injected set.
 */
@HiltViewModel
class AppViewModel @Inject constructor(
    sessionManager: SessionManager,
    val featureNavGraphs: Set<@JvmSuppressWildcards FeatureNavGraph>,
) : ViewModel() {
    val authState: StateFlow<AuthState> = sessionManager.authState
}
