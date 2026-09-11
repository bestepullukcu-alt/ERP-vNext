package eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.employeeonboarding.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingRepository
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.NewEmployeeOnboarding
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [EmployeeOnboardingRepository.create] writes
 * a PENDING row locally and returns success even with no connectivity, upon which
 * we emit [EmployeeOnboardingCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class EmployeeOnboardingCreateViewModel @Inject constructor(
    private val repository: EmployeeOnboardingRepository,
) : MviViewModel<EmployeeOnboardingCreateState, EmployeeOnboardingCreateEvent, EmployeeOnboardingCreateEffect>(
    EmployeeOnboardingCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewEmployeeOnboarding.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: EmployeeOnboardingCreateEvent) {
        when (event) {
            is EmployeeOnboardingCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is EmployeeOnboardingCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is EmployeeOnboardingCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is EmployeeOnboardingCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is EmployeeOnboardingCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            EmployeeOnboardingCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewEmployeeOnboarding())
        when (result) {
            is UiResult.Success -> sendEffect(EmployeeOnboardingCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun EmployeeOnboardingCreateState.toNewEmployeeOnboarding(): NewEmployeeOnboarding {
    fun field(f: OnboardingStateField): ReadinessState = states[f] ?: f.default
    return NewEmployeeOnboarding(
        code = code.trim(),
        displayName = displayName.trim(),
        onboardingReadinessState = field(OnboardingStateField.OnboardingReadiness),
        lifecycleBoundaryState = field(OnboardingStateField.LifecycleBoundary),
        checklistBoundaryState = field(OnboardingStateField.ChecklistBoundary),
        managerActionBoundaryState = field(OnboardingStateField.ManagerActionBoundary),
        employeeActionBoundaryState = field(OnboardingStateField.EmployeeActionBoundary),
        candidateTransitionBoundaryState = field(OnboardingStateField.CandidateTransitionBoundary),
        identityProvisioningBoundaryState = field(OnboardingStateField.IdentityProvisioningBoundary),
        accessProvisioningBoundaryState = field(OnboardingStateField.AccessProvisioningBoundary),
        deviceEquipmentProvisioningBoundaryState = field(OnboardingStateField.DeviceEquipmentProvisioningBoundary),
        documentDependencyState = field(OnboardingStateField.DocumentDependency),
        notificationDependencyState = field(OnboardingStateField.NotificationDependency),
        consentPreconditionState = field(OnboardingStateField.ConsentPrecondition),
        dataMinimizationState = field(OnboardingStateField.DataMinimization),
        retentionPolicyState = field(OnboardingStateField.RetentionPolicy),
        evidencePolicyState = field(OnboardingStateField.EvidencePolicy),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewEmployeeOnboarding.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
