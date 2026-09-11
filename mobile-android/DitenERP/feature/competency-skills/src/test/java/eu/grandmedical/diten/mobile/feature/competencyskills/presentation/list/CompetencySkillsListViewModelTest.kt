package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.list

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.feature.competencyskills.FakeCompetencySkillsRepository
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsListItem
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.ReadinessState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/** Unit-tests the list MVI flow against a fake repository. */
@OptIn(ExperimentalCoroutinesApi::class)
class CompetencySkillsListViewModelTest {

    private val mainDispatcher = UnconfinedTestDispatcher()
    private lateinit var repository: FakeCompetencySkillsRepository

    @Before
    fun setUp() {
        Dispatchers.setMain(mainDispatcher)
        repository = FakeCompetencySkillsRepository()
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun item(id: String) = CompetencySkillsListItem(
        id = id,
        code = id,
        displayName = "Name $id",
        competencySkillsReadinessState = ReadinessState.Ready,
        sourceContractVersion = "v1",
    )

    @Test
    fun startsLoading_thenRendersContentFromTheCache() = runTest(mainDispatcher) {
        val vm = CompetencySkillsListViewModel(repository)
        // Before any cache emission, the screen is Loading.
        assertEquals(UiResult.Loading, vm.state.value.items)

        repository.listFlow.emit(listOf(item("a1"), item("a2")))

        val items = vm.state.value.items
        assertTrue(items is UiResult.Success)
        assertEquals(listOf("a1", "a2"), (items as UiResult.Success).data.map { it.id })
    }

    @Test
    fun refresh_triggersRepositoryRefresh() = runTest(mainDispatcher) {
        val vm = CompetencySkillsListViewModel(repository)

        vm.onEvent(CompetencySkillsListEvent.Refresh)

        assertEquals(1, repository.refreshCount)
        assertEquals(false, vm.state.value.isRefreshing)
    }
}
