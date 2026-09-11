package eu.grandmedical.diten.mobile.core.common.mvi

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * Generic Model-View-Intent base every feature ViewModel extends.
 *
 * Design:
 *  - **State** is exposed as a [StateFlow] of an immutable [UiState]; the UI always
 *    has a current value to render and updates go through [setState]'s reducer.
 *  - **Effects** are one-shot: they flow through a [Channel] with a small buffer,
 *    consumed as [receiveAsFlow]. This is deliberately NOT a replaying
 *    `SharedFlow` — each effect is delivered to exactly one collector exactly
 *    once, so a re-subscribing collector (e.g. after a config change) never
 *    replays a stale navigation/toast.
 *  - **Memory safety**: all work runs on [viewModelScope], which is cancelled when
 *    the ViewModel clears. No Activity/Context/View reference is held here, so a
 *    subclass cannot leak the UI through this base.
 *
 * @param initialState the state rendered before the first event is handled.
 */
abstract class MviViewModel<S : UiState, E : UiEvent, F : UiEffect>(
    initialState: S,
) : ViewModel() {

    private val _state = MutableStateFlow(initialState)

    /** The current, always-available screen state. */
    val state: StateFlow<S> = _state.asStateFlow()

    // BUFFERED so a produced effect is never dropped if the collector is briefly
    // absent (e.g. mid recomposition); receiveAsFlow guarantees single delivery.
    private val _effect = Channel<F>(Channel.BUFFERED)

    /** One-shot side effects. Each value is delivered once and never replayed. */
    val effect: Flow<F> = _effect.receiveAsFlow()

    /**
     * Entry point for the view layer. Dispatches [event] to [handleEvent] on
     * [viewModelScope]; override [handleEvent] rather than this method.
     */
    fun onEvent(event: E) {
        viewModelScope.launch { handleEvent(event) }
    }

    /** Reduces a single [event] into state changes and/or effects. */
    protected abstract suspend fun handleEvent(event: E)

    /** Atomically updates the current state via [reducer]. */
    protected fun setState(reducer: S.() -> S) {
        _state.update(reducer)
    }

    /** Enqueues a one-shot [effect] for the single active collector. */
    protected fun sendEffect(effect: F) {
        viewModelScope.launch { _effect.send(effect) }
    }
}
