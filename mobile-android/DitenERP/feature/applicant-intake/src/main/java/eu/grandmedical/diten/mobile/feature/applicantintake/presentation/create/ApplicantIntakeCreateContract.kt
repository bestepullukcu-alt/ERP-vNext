package eu.grandmedical.diten.mobile.feature.applicantintake.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ReadinessState

/**
 * The editable facet fields on the create form. [Intake] is the top-level intake
 * state; the rest are the ten readiness facets. Keeping them as an enum lets the
 * screen render one [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 */
enum class IntakeStateField(val label: String, val default: ReadinessState) {
    Intake("Alım durumu", ReadinessState.Draft),
    SourceChannel("Kaynak kanalı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    DuplicateHandling("Yinelenen kayıt", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ApplicantIdentityBoundary("Aday kimlik sınırı", ReadinessState.Deferred),
    PublicUxBoundary("Genel UX sınırı", ReadinessState.Blocked),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<IntakeStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class ApplicantIntakeCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<IntakeStateField, ReadinessState> = IntakeStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface ApplicantIntakeCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : ApplicantIntakeCreateEvent
    data class DisplayNameChanged(val value: String) : ApplicantIntakeCreateEvent
    data class SourceContractVersionChanged(val value: String) : ApplicantIntakeCreateEvent
    data class DeferredReasonChanged(val value: String) : ApplicantIntakeCreateEvent
    data class StateChanged(val field: IntakeStateField, val value: ReadinessState) : ApplicantIntakeCreateEvent
    data object Submit : ApplicantIntakeCreateEvent
}

sealed interface ApplicantIntakeCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : ApplicantIntakeCreateEffect
}
