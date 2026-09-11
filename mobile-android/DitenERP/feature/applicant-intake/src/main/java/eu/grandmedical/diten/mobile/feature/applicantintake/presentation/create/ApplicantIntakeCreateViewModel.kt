package eu.grandmedical.diten.mobile.feature.applicantintake.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.applicantintake.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeRepository
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.NewApplicantIntake
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [ApplicantIntakeRepository.create] writes a
 * PENDING row locally and returns success even with no connectivity, upon which
 * we emit [ApplicantIntakeCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class ApplicantIntakeCreateViewModel @Inject constructor(
    private val repository: ApplicantIntakeRepository,
) : MviViewModel<ApplicantIntakeCreateState, ApplicantIntakeCreateEvent, ApplicantIntakeCreateEffect>(
    ApplicantIntakeCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewApplicantIntake.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: ApplicantIntakeCreateEvent) {
        when (event) {
            is ApplicantIntakeCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is ApplicantIntakeCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is ApplicantIntakeCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is ApplicantIntakeCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is ApplicantIntakeCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            ApplicantIntakeCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewApplicantIntake())
        when (result) {
            is UiResult.Success -> sendEffect(ApplicantIntakeCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun ApplicantIntakeCreateState.toNewApplicantIntake(): NewApplicantIntake {
    fun field(f: IntakeStateField): ReadinessState = states[f] ?: f.default
    return NewApplicantIntake(
        code = code.trim(),
        displayName = displayName.trim(),
        intakeState = field(IntakeStateField.Intake),
        sourceChannelState = field(IntakeStateField.SourceChannel),
        consentPreconditionState = field(IntakeStateField.ConsentPrecondition),
        dataMinimizationState = field(IntakeStateField.DataMinimization),
        duplicateHandlingState = field(IntakeStateField.DuplicateHandling),
        retentionPolicyState = field(IntakeStateField.RetentionPolicy),
        evidencePolicyState = field(IntakeStateField.EvidencePolicy),
        applicantIdentityBoundaryState = field(IntakeStateField.ApplicantIdentityBoundary),
        publicUxBoundaryState = field(IntakeStateField.PublicUxBoundary),
        documentDependencyState = field(IntakeStateField.DocumentDependency),
        notificationDependencyState = field(IntakeStateField.NotificationDependency),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewApplicantIntake.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
