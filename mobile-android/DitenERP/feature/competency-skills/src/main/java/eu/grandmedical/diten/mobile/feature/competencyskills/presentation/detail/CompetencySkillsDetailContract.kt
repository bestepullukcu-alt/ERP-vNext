package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsReadiness

data class CompetencySkillsDetailState(
    val record: UiResult<CompetencySkillsReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface CompetencySkillsDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : CompetencySkillsDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : CompetencySkillsDetailEvent
}

sealed interface CompetencySkillsDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : CompetencySkillsDetailEffect
    data object NavigateBack : CompetencySkillsDetailEffect
}
