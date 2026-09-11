package eu.grandmedical.diten.mobile.core.common.mvi

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test

/**
 * Verifies the generic MVI base: reducer-driven state updates and, crucially,
 * that effects fire exactly once and are NOT replayed to a later collector.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class MviViewModelTest {

    // --- A concrete ViewModel under test. -------------------------------------

    private data class CounterState(val count: Int = 0) : UiState

    private sealed interface CounterEvent : UiEvent {
        data object Increment : CounterEvent
    }

    private sealed interface CounterEffect : UiEffect {
        data class Notify(val message: String) : CounterEffect
    }

    private class CounterViewModel :
        MviViewModel<CounterState, CounterEvent, CounterEffect>(CounterState()) {
        override suspend fun handleEvent(event: CounterEvent) {
            when (event) {
                CounterEvent.Increment -> {
                    setState { copy(count = count + 1) }
                    sendEffect(CounterEffect.Notify("incremented"))
                }
            }
        }
    }

    // viewModelScope dispatches on Dispatchers.Main; back it with a test
    // dispatcher that shares each test's scheduler.
    private val mainDispatcher = UnconfinedTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(mainDispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun initial_state_is_exposed() = runTest(mainDispatcher) {
        val vm = CounterViewModel()
        assertEquals(CounterState(count = 0), vm.state.value)
    }

    @Test
    fun onEvent_runs_reducer_and_updates_state() = runTest(mainDispatcher) {
        val vm = CounterViewModel()

        vm.onEvent(CounterEvent.Increment)
        vm.onEvent(CounterEvent.Increment)

        assertEquals(2, vm.state.value.count)
    }

    @Test
    fun effect_fires_once_and_is_not_replayed_to_a_second_collector() = runTest(mainDispatcher) {
        val vm = CounterViewModel()

        val first = mutableListOf<CounterEffect>()
        val firstJob = backgroundScope.launch { vm.effect.toList(first) }

        vm.onEvent(CounterEvent.Increment)

        // Exactly one effect delivered to the single active collector.
        assertEquals(listOf(CounterEffect.Notify("incremented")), first)

        // A collector that subscribes afterwards must NOT replay the past effect.
        val second = mutableListOf<CounterEffect>()
        val secondJob = backgroundScope.launch { vm.effect.toList(second) }

        assertEquals(emptyList<CounterEffect>(), second)

        firstJob.cancel()
        secondJob.cancel()
    }
}
