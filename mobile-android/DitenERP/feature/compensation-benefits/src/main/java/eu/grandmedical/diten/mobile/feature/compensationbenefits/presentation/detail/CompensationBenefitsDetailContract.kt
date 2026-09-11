package eu.grandmedical.diten.mobile.feature.compensationbenefits.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsReadiness

data class CompensationBenefitsDetailState(
    val record: UiResult<CompensationBenefitsReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface CompensationBenefitsDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : CompensationBenefitsDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : CompensationBenefitsDetailEvent
}

sealed interface CompensationBenefitsDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : CompensationBenefitsDetailEffect
    data object NavigateBack : CompensationBenefitsDetailEffect
}
