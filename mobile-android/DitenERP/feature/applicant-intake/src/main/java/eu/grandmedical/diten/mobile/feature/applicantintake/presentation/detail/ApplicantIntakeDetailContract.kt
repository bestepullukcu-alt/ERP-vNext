package eu.grandmedical.diten.mobile.feature.applicantintake.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeReadiness

data class ApplicantIntakeDetailState(
    val record: UiResult<ApplicantIntakeReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface ApplicantIntakeDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : ApplicantIntakeDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : ApplicantIntakeDetailEvent
}

sealed interface ApplicantIntakeDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : ApplicantIntakeDetailEffect
    data object NavigateBack : ApplicantIntakeDetailEffect
}
