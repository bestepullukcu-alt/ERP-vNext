package eu.grandmedical.diten.mobile.feature.performancereviews.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.ReadinessState

/**
 * The editable facet fields on the create form. [Readiness] is the top-level
 * performance-review readiness state; the rest are the eighteen readiness facets.
 * Keeping them as an enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 *
 * Defaults MIRROR the measured backend defaults (Draft readiness; Deferred for
 * goal/dependency/policy/consent facets; Blocked for every runtime/UX/comp
 * boundary).
 */
enum class ReviewStateField(val label: String, val default: ReadinessState) {
    Readiness("Değerlendirme durumu", ReadinessState.Draft),
    ReviewCycleBoundary("Değerlendirme döngüsü sınırı", ReadinessState.Blocked),
    GoalDependency("Hedef bağımlılığı", ReadinessState.Deferred),
    ScoringBoundary("Puanlama sınırı", ReadinessState.Blocked),
    RatingBoundary("Derecelendirme sınırı", ReadinessState.Blocked),
    CalibrationBoundary("Kalibrasyon sınırı", ReadinessState.Blocked),
    RankingBoundary("Sıralama sınırı", ReadinessState.Blocked),
    AutomatedDecisionBoundary("Otomatik karar sınırı", ReadinessState.Blocked),
    ManagerReviewUxBoundary("Yönetici değerlendirme UX sınırı", ReadinessState.Blocked),
    EmployeeReviewUxBoundary("Çalışan değerlendirme UX sınırı", ReadinessState.Blocked),
    CompensationDataBoundary("Ücret verisi sınırı", ReadinessState.Blocked),
    BenefitsDataBoundary("Yan haklar verisi sınırı", ReadinessState.Blocked),
    PayrollDataBoundary("Bordro verisi sınırı", ReadinessState.Blocked),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<ReviewStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class PerformanceReviewsCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<ReviewStateField, ReadinessState> = ReviewStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface PerformanceReviewsCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : PerformanceReviewsCreateEvent
    data class DisplayNameChanged(val value: String) : PerformanceReviewsCreateEvent
    data class SourceContractVersionChanged(val value: String) : PerformanceReviewsCreateEvent
    data class DeferredReasonChanged(val value: String) : PerformanceReviewsCreateEvent
    data class StateChanged(val field: ReviewStateField, val value: ReadinessState) : PerformanceReviewsCreateEvent
    data object Submit : PerformanceReviewsCreateEvent
}

sealed interface PerformanceReviewsCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : PerformanceReviewsCreateEffect
}
