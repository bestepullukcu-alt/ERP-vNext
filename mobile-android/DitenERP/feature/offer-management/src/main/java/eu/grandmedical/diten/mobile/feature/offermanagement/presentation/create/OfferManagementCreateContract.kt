package eu.grandmedical.diten.mobile.feature.offermanagement.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.offermanagement.domain.ReadinessState

/**
 * The editable facet fields on the create form. [OfferReadiness] is the top-level
 * offer-readiness state; the rest are the thirteen readiness facets. Keeping them
 * as an enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 *
 * Defaults mirror the measured backend defaults: Draft top-level; Blocked for the
 * offer/approval/candidate/document workflow boundaries; Deferred elsewhere.
 */
enum class OfferStateField(val label: String, val default: ReadinessState) {
    OfferReadiness("Teklif hazırlık durumu", ReadinessState.Draft),
    OfferWorkflowBoundary("Teklif iş akışı sınırı", ReadinessState.Blocked),
    ApprovalWorkflowBoundary("Onay iş akışı sınırı", ReadinessState.Blocked),
    CandidateAcceptanceBoundary("Aday kabul sınırı", ReadinessState.Blocked),
    OfferDocumentBoundary("Teklif belgesi sınırı", ReadinessState.Blocked),
    CompensationDataBoundary("Ücret verisi sınırı", ReadinessState.Deferred),
    BenefitsDataBoundary("Yan haklar verisi sınırı", ReadinessState.Deferred),
    PayrollDataBoundary("Bordro verisi sınırı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<OfferStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class OfferManagementCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<OfferStateField, ReadinessState> = OfferStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface OfferManagementCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : OfferManagementCreateEvent
    data class DisplayNameChanged(val value: String) : OfferManagementCreateEvent
    data class SourceContractVersionChanged(val value: String) : OfferManagementCreateEvent
    data class DeferredReasonChanged(val value: String) : OfferManagementCreateEvent
    data class StateChanged(val field: OfferStateField, val value: ReadinessState) : OfferManagementCreateEvent
    data object Submit : OfferManagementCreateEvent
}

sealed interface OfferManagementCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : OfferManagementCreateEffect
}
