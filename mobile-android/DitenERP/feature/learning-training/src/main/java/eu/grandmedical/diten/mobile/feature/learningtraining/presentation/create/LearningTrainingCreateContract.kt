package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.ReadinessState

/**
 * The editable facet fields on the create form. [LearningTrainingReadiness] is
 * the top-level readiness state; the rest are the fourteen readiness facets.
 * Keeping them as an enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 */
enum class LearningTrainingStateField(val label: String, val default: ReadinessState) {
    LearningTrainingReadiness("Genel hazırlık durumu", ReadinessState.Draft),
    CourseCatalogBoundary("Kurs kataloğu sınırı", ReadinessState.Blocked),
    EnrollmentWorkflowBoundary("Kayıt iş akışı sınırı", ReadinessState.Blocked),
    CompletionTrackingBoundary("Tamamlama takibi sınırı", ReadinessState.Blocked),
    CertificationBoundary("Sertifikasyon sınırı", ReadinessState.Blocked),
    AssessmentScoringBoundary("Değerlendirme puanlama sınırı", ReadinessState.Blocked),
    AutomatedDecisionBoundary("Otomatik karar sınırı", ReadinessState.Blocked),
    LearningContentDependency("Öğrenme içeriği bağımlılığı", ReadinessState.Deferred),
    SkillTaxonomyDependency("Yetkinlik taksonomisi bağımlılığı", ReadinessState.Deferred),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<LearningTrainingStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class LearningTrainingCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<LearningTrainingStateField, ReadinessState> = LearningTrainingStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface LearningTrainingCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : LearningTrainingCreateEvent
    data class DisplayNameChanged(val value: String) : LearningTrainingCreateEvent
    data class SourceContractVersionChanged(val value: String) : LearningTrainingCreateEvent
    data class DeferredReasonChanged(val value: String) : LearningTrainingCreateEvent
    data class StateChanged(val field: LearningTrainingStateField, val value: ReadinessState) : LearningTrainingCreateEvent
    data object Submit : LearningTrainingCreateEvent
}

sealed interface LearningTrainingCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : LearningTrainingCreateEffect
}
