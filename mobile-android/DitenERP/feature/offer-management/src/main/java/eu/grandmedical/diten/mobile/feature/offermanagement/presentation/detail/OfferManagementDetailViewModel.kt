package eu.grandmedical.diten.mobile.feature.offermanagement.presentation.detail

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementRepository
import eu.grandmedical.diten.mobile.feature.offermanagement.presentation.navigation.OfferManagementFeature
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes a single record from the cache (offline-first) and runs the
 * server-authoritative Evaluate / Delete actions. The record [id] arrives as the
 * nav argument via [SavedStateHandle].
 */
@HiltViewModel
class OfferManagementDetailViewModel @Inject constructor(
    private val repository: OfferManagementRepository,
    savedStateHandle: SavedStateHandle,
) : MviViewModel<OfferManagementDetailState, OfferManagementDetailEvent, OfferManagementDetailEffect>(
    OfferManagementDetailState(),
) {

    private val id: String = savedStateHandle.get<String>(OfferManagementFeature.ARG_ID).orEmpty()

    init {
        observeRecord()
    }

    private fun observeRecord() {
        viewModelScope.launch {
            repository.observe(id).collect { record ->
                setState { copy(record = UiResult.Success(record)) }
            }
        }
    }

    override suspend fun handleEvent(event: OfferManagementDetailEvent) {
        when (event) {
            OfferManagementDetailEvent.Evaluate -> evaluate()
            OfferManagementDetailEvent.Delete -> delete()
        }
    }

    private suspend fun evaluate() {
        setState { copy(isBusy = true) }
        val result = repository.evaluate(id)
        if (result is UiResult.Error) {
            sendEffect(OfferManagementDetailEffect.ShowMessage(result.message))
        }
        setState { copy(isBusy = false) }
    }

    private suspend fun delete() {
        setState { copy(isBusy = true) }
        when (val result = repository.delete(id)) {
            is UiResult.Success -> sendEffect(OfferManagementDetailEffect.NavigateBack)
            is UiResult.Error -> {
                sendEffect(OfferManagementDetailEffect.ShowMessage(result.message))
                setState { copy(isBusy = false) }
            }

            UiResult.Loading -> setState { copy(isBusy = false) }
        }
    }
}
