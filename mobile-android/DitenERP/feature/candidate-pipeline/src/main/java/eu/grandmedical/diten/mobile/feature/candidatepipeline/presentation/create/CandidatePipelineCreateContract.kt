package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.ReadinessState

/**
 * The editable facet fields on the create form. [PipelineReadiness] is the
 * top-level readiness state; the rest are the thirteen readiness facets. Keeping
 * them as an enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 */
enum class PipelineStateField(val label: String, val default: ReadinessState) {
    PipelineReadiness("Havuz durumu", ReadinessState.Draft),
    PipelineStageGovernance("Aşama yönetişimi", ReadinessState.Deferred),
    InterviewSchedulingReadiness("Mülakat planlama hazırlığı", ReadinessState.Deferred),
    InterviewerAssignmentReadiness("Mülakatçı atama hazırlığı", ReadinessState.Deferred),
    EvaluationGovernance("Değerlendirme yönetişimi", ReadinessState.Deferred),
    CandidateCommunicationBoundary("Aday iletişim sınırı", ReadinessState.Blocked),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    CalendarDependency("Takvim bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    AutomatedDecisionBoundary("Otomatik karar sınırı", ReadinessState.Blocked),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<PipelineStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class CandidatePipelineCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<PipelineStateField, ReadinessState> = PipelineStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface CandidatePipelineCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : CandidatePipelineCreateEvent
    data class DisplayNameChanged(val value: String) : CandidatePipelineCreateEvent
    data class SourceContractVersionChanged(val value: String) : CandidatePipelineCreateEvent
    data class DeferredReasonChanged(val value: String) : CandidatePipelineCreateEvent
    data class StateChanged(val field: PipelineStateField, val value: ReadinessState) : CandidatePipelineCreateEvent
    data object Submit : CandidatePipelineCreateEvent
}

sealed interface CandidatePipelineCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : CandidatePipelineCreateEffect
}
