package eu.grandmedical.diten.mobile.feature.applicantintake.presentation.list

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeListItem

/** List screen state: the offline-first cached list plus the pull-to-refresh flag. */
data class ApplicantIntakeListState(
    val items: UiResult<List<ApplicantIntakeListItem>> = UiResult.Loading,
    val isRefreshing: Boolean = false,
) : UiState

sealed interface ApplicantIntakeListEvent : UiEvent {
    /** Pull-to-refresh: pull the server list into the cache. */
    data object Refresh : ApplicantIntakeListEvent
}

sealed interface ApplicantIntakeListEffect : UiEffect {
    /** A transient message (e.g. a failed refresh) to surface to the user. */
    data class ShowMessage(val message: String) : ApplicantIntakeListEffect
}
