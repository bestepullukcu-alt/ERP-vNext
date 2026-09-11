package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.candidatepipeline.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineRepository
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.NewCandidatePipeline
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [CandidatePipelineRepository.create] writes a
 * PENDING row locally and returns success even with no connectivity, upon which
 * we emit [CandidatePipelineCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class CandidatePipelineCreateViewModel @Inject constructor(
    private val repository: CandidatePipelineRepository,
) : MviViewModel<CandidatePipelineCreateState, CandidatePipelineCreateEvent, CandidatePipelineCreateEffect>(
    CandidatePipelineCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewCandidatePipeline.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: CandidatePipelineCreateEvent) {
        when (event) {
            is CandidatePipelineCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is CandidatePipelineCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is CandidatePipelineCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is CandidatePipelineCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is CandidatePipelineCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            CandidatePipelineCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewCandidatePipeline())
        when (result) {
            is UiResult.Success -> sendEffect(CandidatePipelineCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun CandidatePipelineCreateState.toNewCandidatePipeline(): NewCandidatePipeline {
    fun field(f: PipelineStateField): ReadinessState = states[f] ?: f.default
    return NewCandidatePipeline(
        code = code.trim(),
        displayName = displayName.trim(),
        pipelineReadinessState = field(PipelineStateField.PipelineReadiness),
        pipelineStageGovernanceState = field(PipelineStateField.PipelineStageGovernance),
        interviewSchedulingReadinessState = field(PipelineStateField.InterviewSchedulingReadiness),
        interviewerAssignmentReadinessState = field(PipelineStateField.InterviewerAssignmentReadiness),
        evaluationGovernanceState = field(PipelineStateField.EvaluationGovernance),
        candidateCommunicationBoundaryState = field(PipelineStateField.CandidateCommunicationBoundary),
        consentPreconditionState = field(PipelineStateField.ConsentPrecondition),
        dataMinimizationState = field(PipelineStateField.DataMinimization),
        retentionPolicyState = field(PipelineStateField.RetentionPolicy),
        evidencePolicyState = field(PipelineStateField.EvidencePolicy),
        calendarDependencyState = field(PipelineStateField.CalendarDependency),
        notificationDependencyState = field(PipelineStateField.NotificationDependency),
        documentDependencyState = field(PipelineStateField.DocumentDependency),
        automatedDecisionBoundaryState = field(PipelineStateField.AutomatedDecisionBoundary),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewCandidatePipeline.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
