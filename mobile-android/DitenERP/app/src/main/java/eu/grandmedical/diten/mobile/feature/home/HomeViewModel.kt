package eu.grandmedical.diten.mobile.feature.home

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.auth.jwt.JwtDecoder
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.auth.session.SessionManager
import eu.grandmedical.diten.mobile.core.auth.token.EncryptedTokenStore
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.core.sync.SyncScheduler
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Exposes the authenticated session to the dashboard and builds the
 * permission-gated module menu. The menu is assembled from the installed
 * features' [FeatureEntry] contributions (injected as a Hilt multibinding —
 * `Set<@JvmSuppressWildcards FeatureEntry>`), so the shell holds NO static module
 * list: adding a feature module makes its entry appear here automatically.
 *
 * The permission set is derived from the access token's `permission` claims
 * (decoded via [JwtDecoder]); the session summary is observed from
 * [SessionManager] so a legal-entity switch re-renders live.
 *
 * On first construction (i.e. the app is authenticated and Home is shown) it
 * enqueues the periodic background sync — [SyncScheduler.enqueuePeriodicSync]
 * is idempotent (unique work, KEEP), so repeated visits never stack schedules.
 */
@HiltViewModel
class HomeViewModel @Inject constructor(
    private val sessionManager: SessionManager,
    private val tokenStore: EncryptedTokenStore,
    private val jwtDecoder: JwtDecoder,
    private val authRepository: AuthRepository,
    private val featureEntries: Set<@JvmSuppressWildcards FeatureEntry>,
    syncScheduler: SyncScheduler,
) : MviViewModel<HomeState, HomeEvent, HomeEffect>(HomeState()) {

    init {
        syncScheduler.enqueuePeriodicSync()
        // Also kick an immediate one-shot pass so records left PENDING from an
        // offline session are pushed as soon as the user reaches Home.
        syncScheduler.enqueueOneTimeSync()
        observeSession()
    }

    private fun observeSession() {
        viewModelScope.launch {
            val permissions = loadPermissions()
            sessionManager.authState.collect { authState ->
                if (authState is AuthState.Authenticated) {
                    setState {
                        copy(
                            email = authState.email,
                            tenantId = authState.tenantId,
                            selectedLegalEntityId = authState.selectedLegalEntityId,
                            availableLegalEntities = authState.availableLegalEntities,
                            permissions = permissions,
                            modules = HomeMenu.visibleEntries(featureEntries, permissions),
                            isLoading = false,
                        )
                    }
                }
            }
        }
    }

    private suspend fun loadPermissions(): Set<String> =
        jwtDecoder.decode(tokenStore.accessToken()).permissions.toSet()

    override suspend fun handleEvent(event: HomeEvent) {
        when (event) {
            is HomeEvent.SelectLegalEntity -> sessionManager.selectLegalEntity(event.id)
            HomeEvent.Logout -> authRepository.logout()
        }
    }
}
