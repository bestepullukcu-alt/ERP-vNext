package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.competencyskills.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsRepository
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.NewCompetencySkills
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [CompetencySkillsRepository.create] writes a
 * PENDING row locally and returns success even with no connectivity, upon which
 * we emit [CompetencySkillsCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class CompetencySkillsCreateViewModel @Inject constructor(
    private val repository: CompetencySkillsRepository,
) : MviViewModel<CompetencySkillsCreateState, CompetencySkillsCreateEvent, CompetencySkillsCreateEffect>(
    CompetencySkillsCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewCompetencySkills.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: CompetencySkillsCreateEvent) {
        when (event) {
            is CompetencySkillsCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is CompetencySkillsCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is CompetencySkillsCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is CompetencySkillsCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is CompetencySkillsCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            CompetencySkillsCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewCompetencySkills())
        when (result) {
            is UiResult.Success -> sendEffect(CompetencySkillsCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun CompetencySkillsCreateState.toNewCompetencySkills(): NewCompetencySkills {
    fun field(f: CompetencySkillsStateField): ReadinessState = states[f] ?: f.default
    return NewCompetencySkills(
        code = code.trim(),
        displayName = displayName.trim(),
        competencySkillsReadinessState = field(CompetencySkillsStateField.CompetencySkillsReadiness),
        assessmentWorkflowBoundaryState = field(CompetencySkillsStateField.AssessmentWorkflowBoundary),
        competencyFrameworkDependencyState = field(CompetencySkillsStateField.CompetencyFrameworkDependency),
        skillTaxonomyDependencyState = field(CompetencySkillsStateField.SkillTaxonomyDependency),
        skillScoringBoundaryState = field(CompetencySkillsStateField.SkillScoringBoundary),
        ratingBoundaryState = field(CompetencySkillsStateField.RatingBoundary),
        calibrationBoundaryState = field(CompetencySkillsStateField.CalibrationBoundary),
        rankingBoundaryState = field(CompetencySkillsStateField.RankingBoundary),
        automatedDecisionBoundaryState = field(CompetencySkillsStateField.AutomatedDecisionBoundary),
        managerAssessmentUxBoundaryState = field(CompetencySkillsStateField.ManagerAssessmentUxBoundary),
        employeeAssessmentUxBoundaryState = field(CompetencySkillsStateField.EmployeeAssessmentUxBoundary),
        documentDependencyState = field(CompetencySkillsStateField.DocumentDependency),
        notificationDependencyState = field(CompetencySkillsStateField.NotificationDependency),
        consentPreconditionState = field(CompetencySkillsStateField.ConsentPrecondition),
        dataMinimizationState = field(CompetencySkillsStateField.DataMinimization),
        retentionPolicyState = field(CompetencySkillsStateField.RetentionPolicy),
        evidencePolicyState = field(CompetencySkillsStateField.EvidencePolicy),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewCompetencySkills.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
