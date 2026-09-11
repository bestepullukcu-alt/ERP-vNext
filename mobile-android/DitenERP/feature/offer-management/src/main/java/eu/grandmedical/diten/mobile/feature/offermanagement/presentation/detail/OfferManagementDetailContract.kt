package eu.grandmedical.diten.mobile.feature.offermanagement.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementReadiness

data class OfferManagementDetailState(
    val record: UiResult<OfferManagementReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface OfferManagementDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : OfferManagementDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : OfferManagementDetailEvent
}

sealed interface OfferManagementDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : OfferManagementDetailEffect
    data object NavigateBack : OfferManagementDetailEffect
}
