package eu.grandmedical.diten.mobile.feature.offermanagement.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([OfferManagementRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [OfferManagementRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class OfferManagementListViewModel @Inject constructor(
    private val repository: OfferManagementRepository,
) : MviViewModel<OfferManagementListState, OfferManagementListEvent, OfferManagementListEffect>(
    OfferManagementListState(),
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

    override suspend fun handleEvent(event: OfferManagementListEvent) {
        when (event) {
            OfferManagementListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(OfferManagementListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
