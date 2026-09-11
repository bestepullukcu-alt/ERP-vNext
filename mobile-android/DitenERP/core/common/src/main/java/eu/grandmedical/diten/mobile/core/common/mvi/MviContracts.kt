package eu.grandmedical.diten.mobile.core.common.mvi

/**
 * The three marker contracts of the Model-View-Intent presentation pattern used
 * across every feature screen.
 *
 * A screen renders a single immutable [UiState], reacts to user/system [UiEvent]s
 * and emits one-shot [UiEffect]s (navigation, toasts, snackbars) that must fire
 * exactly once. Keeping them as empty marker interfaces lets [MviViewModel] stay
 * fully generic while each feature declares its own concrete state/event/effect
 * types.
 */

/** Immutable, renderable state of a screen. Implementations are typically data classes. */
interface UiState

/** A user intent or system signal fed into a [MviViewModel] via `onEvent`. */
interface UiEvent

/**
 * A one-shot side effect (navigation, toast, snackbar). Delivered once and never
 * replayed — see [MviViewModel.effect].
 */
interface UiEffect
