package eu.grandmedical.diten.mobile.feature.performancereviews.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.performancereviews.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.NewPerformanceReviews
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsRepository
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.ReadinessState
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [PerformanceReviewsRepository.create] writes
 * a PENDING row locally and returns success even with no connectivity, upon which
 * we emit [PerformanceReviewsCreateEffect.NavigateBack]. A failure surfaces a
 * message.
 */
@HiltViewModel
class PerformanceReviewsCreateViewModel @Inject constructor(
    private val repository: PerformanceReviewsRepository,
) : MviViewModel<PerformanceReviewsCreateState, PerformanceReviewsCreateEvent, PerformanceReviewsCreateEffect>(
    PerformanceReviewsCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewPerformanceReviews.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: PerformanceReviewsCreateEvent) {
        when (event) {
            is PerformanceReviewsCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is PerformanceReviewsCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is PerformanceReviewsCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is PerformanceReviewsCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is PerformanceReviewsCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            PerformanceReviewsCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewPerformanceReviews())
        when (result) {
            is UiResult.Success -> sendEffect(PerformanceReviewsCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun PerformanceReviewsCreateState.toNewPerformanceReviews(): NewPerformanceReviews {
    fun field(f: ReviewStateField): ReadinessState = states[f] ?: f.default
    return NewPerformanceReviews(
        code = code.trim(),
        displayName = displayName.trim(),
        performanceReviewReadinessState = field(ReviewStateField.Readiness),
        reviewCycleBoundaryState = field(ReviewStateField.ReviewCycleBoundary),
        goalDependencyState = field(ReviewStateField.GoalDependency),
        scoringBoundaryState = field(ReviewStateField.ScoringBoundary),
        ratingBoundaryState = field(ReviewStateField.RatingBoundary),
        calibrationBoundaryState = field(ReviewStateField.CalibrationBoundary),
        rankingBoundaryState = field(ReviewStateField.RankingBoundary),
        automatedDecisionBoundaryState = field(ReviewStateField.AutomatedDecisionBoundary),
        managerReviewUxBoundaryState = field(ReviewStateField.ManagerReviewUxBoundary),
        employeeReviewUxBoundaryState = field(ReviewStateField.EmployeeReviewUxBoundary),
        compensationDataBoundaryState = field(ReviewStateField.CompensationDataBoundary),
        benefitsDataBoundaryState = field(ReviewStateField.BenefitsDataBoundary),
        payrollDataBoundaryState = field(ReviewStateField.PayrollDataBoundary),
        documentDependencyState = field(ReviewStateField.DocumentDependency),
        notificationDependencyState = field(ReviewStateField.NotificationDependency),
        consentPreconditionState = field(ReviewStateField.ConsentPrecondition),
        dataMinimizationState = field(ReviewStateField.DataMinimization),
        retentionPolicyState = field(ReviewStateField.RetentionPolicy),
        evidencePolicyState = field(ReviewStateField.EvidencePolicy),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewPerformanceReviews.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
