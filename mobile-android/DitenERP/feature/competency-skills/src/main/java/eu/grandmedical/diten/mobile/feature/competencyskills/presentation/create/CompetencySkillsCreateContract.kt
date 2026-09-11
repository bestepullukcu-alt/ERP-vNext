package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.ReadinessState

/**
 * The editable facet fields on the create form. [CompetencySkillsReadiness] is the
 * top-level readiness state; the rest are the sixteen readiness facets. Keeping
 * them as an enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 */
enum class CompetencySkillsStateField(val label: String, val default: ReadinessState) {
    CompetencySkillsReadiness("Yetkinlik & beceri durumu", ReadinessState.Draft),
    AssessmentWorkflowBoundary("Değerlendirme akışı sınırı", ReadinessState.Blocked),
    CompetencyFrameworkDependency("Yetkinlik çerçevesi bağımlılığı", ReadinessState.Deferred),
    SkillTaxonomyDependency("Beceri taksonomisi bağımlılığı", ReadinessState.Deferred),
    SkillScoringBoundary("Beceri puanlama sınırı", ReadinessState.Blocked),
    RatingBoundary("Derecelendirme sınırı", ReadinessState.Blocked),
    CalibrationBoundary("Kalibrasyon sınırı", ReadinessState.Blocked),
    RankingBoundary("Sıralama sınırı", ReadinessState.Blocked),
    AutomatedDecisionBoundary("Otomatik karar sınırı", ReadinessState.Blocked),
    ManagerAssessmentUxBoundary("Yönetici değerlendirme arayüzü sınırı", ReadinessState.Blocked),
    EmployeeAssessmentUxBoundary("Çalışan değerlendirme arayüzü sınırı", ReadinessState.Blocked),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<CompetencySkillsStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class CompetencySkillsCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<CompetencySkillsStateField, ReadinessState> = CompetencySkillsStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface CompetencySkillsCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : CompetencySkillsCreateEvent
    data class DisplayNameChanged(val value: String) : CompetencySkillsCreateEvent
    data class SourceContractVersionChanged(val value: String) : CompetencySkillsCreateEvent
    data class DeferredReasonChanged(val value: String) : CompetencySkillsCreateEvent
    data class StateChanged(
        val field: CompetencySkillsStateField,
        val value: ReadinessState,
    ) : CompetencySkillsCreateEvent
    data object Submit : CompetencySkillsCreateEvent
}

sealed interface CompetencySkillsCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : CompetencySkillsCreateEffect
}
