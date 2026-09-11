package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.learningtraining.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingRepository
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.NewLearningTraining
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [LearningTrainingRepository.create] writes a
 * PENDING row locally and returns success even with no connectivity, upon which
 * we emit [LearningTrainingCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class LearningTrainingCreateViewModel @Inject constructor(
    private val repository: LearningTrainingRepository,
) : MviViewModel<LearningTrainingCreateState, LearningTrainingCreateEvent, LearningTrainingCreateEffect>(
    LearningTrainingCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewLearningTraining.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: LearningTrainingCreateEvent) {
        when (event) {
            is LearningTrainingCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is LearningTrainingCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is LearningTrainingCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is LearningTrainingCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is LearningTrainingCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            LearningTrainingCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewLearningTraining())
        when (result) {
            is UiResult.Success -> sendEffect(LearningTrainingCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun LearningTrainingCreateState.toNewLearningTraining(): NewLearningTraining {
    fun field(f: LearningTrainingStateField): ReadinessState = states[f] ?: f.default
    return NewLearningTraining(
        code = code.trim(),
        displayName = displayName.trim(),
        learningTrainingReadinessState = field(LearningTrainingStateField.LearningTrainingReadiness),
        courseCatalogBoundaryState = field(LearningTrainingStateField.CourseCatalogBoundary),
        enrollmentWorkflowBoundaryState = field(LearningTrainingStateField.EnrollmentWorkflowBoundary),
        completionTrackingBoundaryState = field(LearningTrainingStateField.CompletionTrackingBoundary),
        certificationBoundaryState = field(LearningTrainingStateField.CertificationBoundary),
        assessmentScoringBoundaryState = field(LearningTrainingStateField.AssessmentScoringBoundary),
        automatedDecisionBoundaryState = field(LearningTrainingStateField.AutomatedDecisionBoundary),
        learningContentDependencyState = field(LearningTrainingStateField.LearningContentDependency),
        skillTaxonomyDependencyState = field(LearningTrainingStateField.SkillTaxonomyDependency),
        documentDependencyState = field(LearningTrainingStateField.DocumentDependency),
        notificationDependencyState = field(LearningTrainingStateField.NotificationDependency),
        consentPreconditionState = field(LearningTrainingStateField.ConsentPrecondition),
        dataMinimizationState = field(LearningTrainingStateField.DataMinimization),
        retentionPolicyState = field(LearningTrainingStateField.RetentionPolicy),
        evidencePolicyState = field(LearningTrainingStateField.EvidencePolicy),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewLearningTraining.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
