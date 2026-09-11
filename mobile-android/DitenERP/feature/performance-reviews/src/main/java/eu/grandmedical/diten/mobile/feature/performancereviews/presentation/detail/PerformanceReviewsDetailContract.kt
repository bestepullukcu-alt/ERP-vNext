package eu.grandmedical.diten.mobile.feature.performancereviews.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsReadiness

data class PerformanceReviewsDetailState(
    val record: UiResult<PerformanceReviewsReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface PerformanceReviewsDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : PerformanceReviewsDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : PerformanceReviewsDetailEvent
}

sealed interface PerformanceReviewsDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : PerformanceReviewsDetailEffect
    data object NavigateBack : PerformanceReviewsDetailEffect
}
