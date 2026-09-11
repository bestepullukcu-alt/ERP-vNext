package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineReadiness

data class CandidatePipelineDetailState(
    val record: UiResult<CandidatePipelineReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface CandidatePipelineDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : CandidatePipelineDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : CandidatePipelineDetailEvent
}

sealed interface CandidatePipelineDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : CandidatePipelineDetailEffect
    data object NavigateBack : CandidatePipelineDetailEffect
}
