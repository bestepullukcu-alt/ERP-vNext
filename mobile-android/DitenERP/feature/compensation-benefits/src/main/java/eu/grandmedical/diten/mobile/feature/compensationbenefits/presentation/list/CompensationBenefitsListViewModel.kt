package eu.grandmedical.diten.mobile.feature.compensationbenefits.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list
 * ([CompensationBenefitsRepository.observeList]) and exposes it as a [UiResult].
 * Pull-to-refresh delegates to [CompensationBenefitsRepository.refresh]; a
 * failure surfaces as a one-shot message effect (the cached list keeps rendering).
 */
@HiltViewModel
class CompensationBenefitsListViewModel @Inject constructor(
    private val repository: CompensationBenefitsRepository,
) : MviViewModel<CompensationBenefitsListState, CompensationBenefitsListEvent, CompensationBenefitsListEffect>(
    CompensationBenefitsListState(),
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

    override suspend fun handleEvent(event: CompensationBenefitsListEvent) {
        when (event) {
            CompensationBenefitsListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(CompensationBenefitsListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
