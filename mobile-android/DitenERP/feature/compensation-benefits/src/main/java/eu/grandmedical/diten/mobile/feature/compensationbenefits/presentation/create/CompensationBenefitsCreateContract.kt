package eu.grandmedical.diten.mobile.feature.compensationbenefits.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.compensationbenefits.domain.ReadinessState

/**
 * The editable facet fields on the create form. [Readiness] is the top-level
 * state; the rest are the fourteen readiness facets. Keeping them as an enum lets
 * the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 *
 * Defaults mirror the backend: Draft top-level, Blocked boundary facets, Deferred
 * dependency/policy facets (in THIS module Deferred=2, Blocked=3).
 */
enum class CompensationBenefitsStateField(val label: String, val default: ReadinessState) {
    Readiness("Ücret & yan haklar durumu", ReadinessState.Draft),
    CompensationPlanBoundary("Ücret planı sınırı", ReadinessState.Blocked),
    BenefitProgramBoundary("Yan hak programı sınırı", ReadinessState.Blocked),
    PayGradeMappingBoundary("Ücret kademesi eşleme sınırı", ReadinessState.Blocked),
    BenefitEnrollmentBoundary("Yan hak kaydı sınırı", ReadinessState.Blocked),
    CompensationReviewBoundary("Ücret değerlendirme sınırı", ReadinessState.Blocked),
    AutomatedDecisionBoundary("Otomatik karar sınırı", ReadinessState.Blocked),
    CompensationSourceDependency("Ücret kaynağı bağımlılığı", ReadinessState.Deferred),
    BenefitProviderSourceDependency("Yan hak sağlayıcı bağımlılığı", ReadinessState.Deferred),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<CompensationBenefitsStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class CompensationBenefitsCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<CompensationBenefitsStateField, ReadinessState> = CompensationBenefitsStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface CompensationBenefitsCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : CompensationBenefitsCreateEvent
    data class DisplayNameChanged(val value: String) : CompensationBenefitsCreateEvent
    data class SourceContractVersionChanged(val value: String) : CompensationBenefitsCreateEvent
    data class DeferredReasonChanged(val value: String) : CompensationBenefitsCreateEvent
    data class StateChanged(
        val field: CompensationBenefitsStateField,
        val value: ReadinessState,
    ) : CompensationBenefitsCreateEvent
    data object Submit : CompensationBenefitsCreateEvent
}

sealed interface CompensationBenefitsCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : CompensationBenefitsCreateEffect
}
