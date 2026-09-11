package eu.grandmedical.diten.mobile.feature.offermanagement.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.offermanagement.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.NewOfferManagement
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.OfferManagementRepository
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [OfferManagementRepository.create] writes a
 * PENDING row locally and returns success even with no connectivity, upon which
 * we emit [OfferManagementCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class OfferManagementCreateViewModel @Inject constructor(
    private val repository: OfferManagementRepository,
) : MviViewModel<OfferManagementCreateState, OfferManagementCreateEvent, OfferManagementCreateEffect>(
    OfferManagementCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewOfferManagement.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: OfferManagementCreateEvent) {
        when (event) {
            is OfferManagementCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is OfferManagementCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is OfferManagementCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is OfferManagementCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is OfferManagementCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            OfferManagementCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewOfferManagement())
        when (result) {
            is UiResult.Success -> sendEffect(OfferManagementCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun OfferManagementCreateState.toNewOfferManagement(): NewOfferManagement {
    fun field(f: OfferStateField): ReadinessState = states[f] ?: f.default
    return NewOfferManagement(
        code = code.trim(),
        displayName = displayName.trim(),
        offerReadinessState = field(OfferStateField.OfferReadiness),
        offerWorkflowBoundaryState = field(OfferStateField.OfferWorkflowBoundary),
        approvalWorkflowBoundaryState = field(OfferStateField.ApprovalWorkflowBoundary),
        candidateAcceptanceBoundaryState = field(OfferStateField.CandidateAcceptanceBoundary),
        offerDocumentBoundaryState = field(OfferStateField.OfferDocumentBoundary),
        compensationDataBoundaryState = field(OfferStateField.CompensationDataBoundary),
        benefitsDataBoundaryState = field(OfferStateField.BenefitsDataBoundary),
        payrollDataBoundaryState = field(OfferStateField.PayrollDataBoundary),
        consentPreconditionState = field(OfferStateField.ConsentPrecondition),
        dataMinimizationState = field(OfferStateField.DataMinimization),
        retentionPolicyState = field(OfferStateField.RetentionPolicy),
        evidencePolicyState = field(OfferStateField.EvidencePolicy),
        notificationDependencyState = field(OfferStateField.NotificationDependency),
        documentDependencyState = field(OfferStateField.DocumentDependency),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewOfferManagement.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
