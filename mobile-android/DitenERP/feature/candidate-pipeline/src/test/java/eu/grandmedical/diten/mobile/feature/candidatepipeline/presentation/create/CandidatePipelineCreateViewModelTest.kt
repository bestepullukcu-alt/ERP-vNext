package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.create

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.feature.candidatepipeline.FakeCandidatePipelineRepository
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
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/** Unit-tests the create MVI flow against a fake repository. */
@OptIn(ExperimentalCoroutinesApi::class)
class CandidatePipelineCreateViewModelTest {

    private val mainDispatcher = UnconfinedTestDispatcher()
    private lateinit var repository: FakeCandidatePipelineRepository

    @Before
    fun setUp() {
        Dispatchers.setMain(mainDispatcher)
        repository = FakeCandidatePipelineRepository()
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun defaultCode_isPrefilledAtInit() {
        val vm = CandidatePipelineCreateViewModel(repository)
        assertTrue(vm.state.value.code.isNotBlank())
        assertTrue(vm.state.value.code.startsWith("CP-"))
        assertEquals("v1", vm.state.value.sourceContractVersion)
    }

    @Test
    fun submit_success_emitsNavigateBackOnce() = runTest(mainDispatcher) {
        repository.createResult = UiResult.Success("local-1")
        val vm = CandidatePipelineCreateViewModel(repository)
        val effects = mutableListOf<CandidatePipelineCreateEffect>()
        val job = backgroundScope.launch { vm.effect.toList(effects) }

        vm.onEvent(CandidatePipelineCreateEvent.DisplayNameChanged("Local only"))
        vm.onEvent(CandidatePipelineCreateEvent.Submit)

        assertEquals(listOf(CandidatePipelineCreateEffect.NavigateBack), effects)
        assertEquals("Local only", repository.lastCreated?.displayName)
        job.cancel()
    }

    @Test
    fun submit_error_setsMessage_andDoesNotNavigate() = runTest(mainDispatcher) {
        repository.createResult = UiResult.Error("Rejected by server")
        val vm = CandidatePipelineCreateViewModel(repository)
        val effects = mutableListOf<CandidatePipelineCreateEffect>()
        val job = backgroundScope.launch { vm.effect.toList(effects) }

        vm.onEvent(CandidatePipelineCreateEvent.DisplayNameChanged("Local only"))
        vm.onEvent(CandidatePipelineCreateEvent.Submit)

        assertTrue(effects.isEmpty())
        assertEquals("Rejected by server", vm.state.value.errorMessage)
        assertEquals(false, vm.state.value.isSubmitting)
        job.cancel()
    }

    @Test
    fun submit_withoutDisplayName_isIgnored() = runTest(mainDispatcher) {
        val vm = CandidatePipelineCreateViewModel(repository)

        vm.onEvent(CandidatePipelineCreateEvent.Submit)

        // canSubmit is false (blank display name), so create is never called.
        assertNull(repository.lastCreated)
    }
}
