package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([CompetencySkillsRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [CompetencySkillsRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class CompetencySkillsListViewModel @Inject constructor(
    private val repository: CompetencySkillsRepository,
) : MviViewModel<CompetencySkillsListState, CompetencySkillsListEvent, CompetencySkillsListEffect>(
    CompetencySkillsListState(),
) {

    init {
        observeList()
    }

    private fun observeList() {
        viewModelScope.launch {
            repository.observeList().collect { list ->
                setState { copy(items = UiResult.Success(list)) }
            }
        }
    }

    override suspend fun handleEvent(event: CompetencySkillsListEvent) {
        when (event) {
            CompetencySkillsListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(CompetencySkillsListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
