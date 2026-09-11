package eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.ReadinessState

/**
 * The editable facet fields on the create form. [OnboardingReadiness] is the
 * top-level onboarding readiness state; the rest are the fourteen readiness
 * facets. Keeping them as an enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 */
enum class OnboardingStateField(val label: String, val default: ReadinessState) {
    OnboardingReadiness("Oryantasyon durumu", ReadinessState.Draft),
    LifecycleBoundary("Yaşam döngüsü sınırı", ReadinessState.Blocked),
    ChecklistBoundary("Kontrol listesi sınırı", ReadinessState.Blocked),
    ManagerActionBoundary("Yönetici eylem sınırı", ReadinessState.Blocked),
    EmployeeActionBoundary("Çalışan eylem sınırı", ReadinessState.Blocked),
    CandidateTransitionBoundary("Aday geçiş sınırı", ReadinessState.Deferred),
    IdentityProvisioningBoundary("Kimlik sağlama sınırı", ReadinessState.Deferred),
    AccessProvisioningBoundary("Erişim sağlama sınırı", ReadinessState.Deferred),
    DeviceEquipmentProvisioningBoundary("Cihaz/ekipman sağlama sınırı", ReadinessState.Deferred),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<OnboardingStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class EmployeeOnboardingCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<OnboardingStateField, ReadinessState> = OnboardingStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface EmployeeOnboardingCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : EmployeeOnboardingCreateEvent
    data class DisplayNameChanged(val value: String) : EmployeeOnboardingCreateEvent
    data class SourceContractVersionChanged(val value: String) : EmployeeOnboardingCreateEvent
    data class DeferredReasonChanged(val value: String) : EmployeeOnboardingCreateEvent
    data class StateChanged(val field: OnboardingStateField, val value: ReadinessState) : EmployeeOnboardingCreateEvent
    data object Submit : EmployeeOnboardingCreateEvent
}

sealed interface EmployeeOnboardingCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : EmployeeOnboardingCreateEffect
}
