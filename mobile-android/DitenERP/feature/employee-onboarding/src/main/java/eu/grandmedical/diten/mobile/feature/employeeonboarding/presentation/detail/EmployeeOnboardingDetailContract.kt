package eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingReadiness

data class EmployeeOnboardingDetailState(
    val record: UiResult<EmployeeOnboardingReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface EmployeeOnboardingDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : EmployeeOnboardingDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : EmployeeOnboardingDetailEvent
}

sealed interface EmployeeOnboardingDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : EmployeeOnboardingDetailEffect
    data object NavigateBack : EmployeeOnboardingDetailEffect
}
