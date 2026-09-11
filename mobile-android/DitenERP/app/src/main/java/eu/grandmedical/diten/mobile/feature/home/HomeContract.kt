package eu.grandmedical.diten.mobile.feature.home

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState

/**
 * Dashboard render state: the current session summary plus the permission-gated
 * module menu. [modules] is already filtered to what the user may see.
 */
data class HomeState(
    val email: String? = null,
    val tenantId: String? = null,
    val selectedLegalEntityId: String? = null,
    val availableLegalEntities: List<String> = emptyList(),
    val permissions: Set<String> = emptySet(),
    val modules: List<ModuleEntry> = emptyList(),
    val isLoading: Boolean = true,
) : UiState

/** User intents for the Home screen. */
sealed interface HomeEvent : UiEvent {
    /** Switch the active legal entity (drives the `X-Legal-Entity-Id` header). */
    data class SelectLegalEntity(val id: String) : HomeEvent

    /** Sign out; clears the session so the shell routes back to Login. */
    data object Logout : HomeEvent
}

/** Home emits no one-shot effects: logout-driven navigation is observed from the session flow. */
sealed interface HomeEffect : UiEffect
