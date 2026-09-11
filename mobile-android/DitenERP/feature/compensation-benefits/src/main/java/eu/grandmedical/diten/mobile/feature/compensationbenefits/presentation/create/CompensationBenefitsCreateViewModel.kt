package eu.grandmedical.diten.mobile.feature.compensationbenefits.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.compensationbenefits.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.CompensationBenefitsRepository
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.NewCompensationBenefits
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [CompensationBenefitsRepository.create]
 * writes a PENDING row locally and returns success even with no connectivity,
 * upon which we emit [CompensationBenefitsCreateEffect.NavigateBack]. A failure
 * surfaces a message.
 */
@HiltViewModel
class CompensationBenefitsCreateViewModel @Inject constructor(
    private val repository: CompensationBenefitsRepository,
) : MviViewModel<CompensationBenefitsCreateState, CompensationBenefitsCreateEvent, CompensationBenefitsCreateEffect>(
    CompensationBenefitsCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewCompensationBenefits.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: CompensationBenefitsCreateEvent) {
        when (event) {
            is CompensationBenefitsCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is CompensationBenefitsCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is CompensationBenefitsCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is CompensationBenefitsCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is CompensationBenefitsCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            CompensationBenefitsCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewCompensationBenefits())
        when (result) {
            is UiResult.Success -> sendEffect(CompensationBenefitsCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun CompensationBenefitsCreateState.toNewCompensationBenefits(): NewCompensationBenefits {
    fun field(f: CompensationBenefitsStateField): ReadinessState = states[f] ?: f.default
    return NewCompensationBenefits(
        code = code.trim(),
        displayName = displayName.trim(),
        compensationBenefitsReadinessState = field(CompensationBenefitsStateField.Readiness),
        compensationPlanBoundaryState = field(CompensationBenefitsStateField.CompensationPlanBoundary),
        benefitProgramBoundaryState = field(CompensationBenefitsStateField.BenefitProgramBoundary),
        payGradeMappingBoundaryState = field(CompensationBenefitsStateField.PayGradeMappingBoundary),
        benefitEnrollmentBoundaryState = field(CompensationBenefitsStateField.BenefitEnrollmentBoundary),
        compensationReviewBoundaryState = field(CompensationBenefitsStateField.CompensationReviewBoundary),
        automatedDecisionBoundaryState = field(CompensationBenefitsStateField.AutomatedDecisionBoundary),
        compensationSourceDependencyState = field(CompensationBenefitsStateField.CompensationSourceDependency),
        benefitProviderSourceDependencyState = field(CompensationBenefitsStateField.BenefitProviderSourceDependency),
        documentDependencyState = field(CompensationBenefitsStateField.DocumentDependency),
        notificationDependencyState = field(CompensationBenefitsStateField.NotificationDependency),
        consentPreconditionState = field(CompensationBenefitsStateField.ConsentPrecondition),
        dataMinimizationState = field(CompensationBenefitsStateField.DataMinimization),
        retentionPolicyState = field(CompensationBenefitsStateField.RetentionPolicy),
        evidencePolicyState = field(CompensationBenefitsStateField.EvidencePolicy),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewCompensationBenefits.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
