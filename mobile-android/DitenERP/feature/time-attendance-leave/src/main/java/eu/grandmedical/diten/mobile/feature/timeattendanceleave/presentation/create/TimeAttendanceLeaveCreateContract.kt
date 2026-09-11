package eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.create

import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.ReadinessState

/**
 * The editable facet fields on the create form. [Readiness] is the top-level
 * readiness state; the rest are the fourteen readiness facets. Keeping them as an
 * enum lets the screen render one
 * [DitenDropdownField][eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField]
 * per field from a single map, and lets the ViewModel reduce a single event.
 *
 * Defaults mirror the backend create request: Blocked for the six boundary
 * facets, Deferred for the dependency/policy facets, Draft top-level readiness.
 */
enum class TimeAttendanceLeaveStateField(val label: String, val default: ReadinessState) {
    Readiness("Genel hazırlık durumu", ReadinessState.Draft),
    TimesheetIntakeBoundary("Zaman çizelgesi alım sınırı", ReadinessState.Blocked),
    AttendanceSyncBoundary("Devam senkron sınırı", ReadinessState.Blocked),
    LeaveRequestBoundary("İzin talep sınırı", ReadinessState.Blocked),
    LeaveBalanceBoundary("İzin bakiye sınırı", ReadinessState.Blocked),
    ScheduleConsumptionBoundary("Çizelge tüketim sınırı", ReadinessState.Blocked),
    AutomatedDecisionBoundary("Otomatik karar sınırı", ReadinessState.Blocked),
    TimeAttendanceSourceDependency("Zaman/devam kaynak bağımlılığı", ReadinessState.Deferred),
    LeaveSourceDependency("İzin kaynak bağımlılığı", ReadinessState.Deferred),
    DocumentDependency("Belge bağımlılığı", ReadinessState.Deferred),
    NotificationDependency("Bildirim bağımlılığı", ReadinessState.Deferred),
    ConsentPrecondition("Rıza ön koşulu", ReadinessState.Deferred),
    DataMinimization("Veri minimizasyonu", ReadinessState.Deferred),
    RetentionPolicy("Saklama politikası", ReadinessState.Deferred),
    EvidencePolicy("Kanıt politikası", ReadinessState.Deferred),
    ;

    companion object {
        /** The backend-aligned default state for every field. */
        fun defaults(): Map<TimeAttendanceLeaveStateField, ReadinessState> = entries.associateWith { it.default }
    }
}

data class TimeAttendanceLeaveCreateState(
    val code: String = "",
    val displayName: String = "",
    val sourceContractVersion: String = "v1",
    val deferredReason: String = "",
    val states: Map<TimeAttendanceLeaveStateField, ReadinessState> = TimeAttendanceLeaveStateField.defaults(),
    val isSubmitting: Boolean = false,
    val errorMessage: String? = null,
) : UiState {
    /** Submission is allowed only with a non-blank code and display name. */
    val canSubmit: Boolean
        get() = code.isNotBlank() && displayName.isNotBlank() && !isSubmitting
}

sealed interface TimeAttendanceLeaveCreateEvent : UiEvent {
    data class CodeChanged(val value: String) : TimeAttendanceLeaveCreateEvent
    data class DisplayNameChanged(val value: String) : TimeAttendanceLeaveCreateEvent
    data class SourceContractVersionChanged(val value: String) : TimeAttendanceLeaveCreateEvent
    data class DeferredReasonChanged(val value: String) : TimeAttendanceLeaveCreateEvent
    data class StateChanged(val field: TimeAttendanceLeaveStateField, val value: ReadinessState) : TimeAttendanceLeaveCreateEvent
    data object Submit : TimeAttendanceLeaveCreateEvent
}

sealed interface TimeAttendanceLeaveCreateEffect : UiEffect {
    /** The optimistic local write succeeded; leave the create screen. */
    data object NavigateBack : TimeAttendanceLeaveCreateEffect
}
